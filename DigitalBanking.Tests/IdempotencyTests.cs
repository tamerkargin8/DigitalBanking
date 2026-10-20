using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DigitalBanking.API.Filters;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Transaction;
using DigitalBanking.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DigitalBanking.Tests;

public class IdempotencyTests
{
    private static readonly Guid FixedAccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly JsonSerializerOptions HashOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public async Task DuplicateKeyReplaysOriginalResponseWithoutExecutingTwice()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var filter = new IdempotencyFilter(new IdempotencyStore(context), NullLogger<IdempotencyFilter>.Instance);
        var executions = 0;

        var first = BuildContext("dup-key-1", 10.50m);
        await filter.OnActionExecutionAsync(first, Next(first, () => executions++, new OkObjectResult(new { success = true, amount = 10.50m })));

        var second = BuildContext("dup-key-1", 10.50m);
        await filter.OnActionExecutionAsync(second, Next(second, () => executions++, new OkObjectResult(new { success = true, amount = 10.50m })));

        Assert.Equal(1, executions);
        var replay = Assert.IsType<ContentResult>(second.Result);
        Assert.Equal(200, replay.StatusCode);
        Assert.Contains("\"success\":true", replay.Content);
        Assert.Equal("true", second.HttpContext.Response.Headers["Idempotency-Replayed"]);
    }

    [Fact]
    public async Task SameKeyWithDifferentPayloadIsRejected()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var filter = new IdempotencyFilter(new IdempotencyStore(context), NullLogger<IdempotencyFilter>.Instance);
        var executions = 0;

        var first = BuildContext("dup-key-2", 10m);
        await filter.OnActionExecutionAsync(first, Next(first, () => executions++, new OkObjectResult(new { success = true })));

        var second = BuildContext("dup-key-2", 99m);
        await filter.OnActionExecutionAsync(second, Next(second, () => executions++, new OkObjectResult(new { success = true })));

        Assert.Equal(1, executions);
        var conflict = Assert.IsType<ConflictObjectResult>(second.Result);
        var body = Assert.IsType<ApiResponse>(conflict.Value);
        Assert.Contains("different request payload", body.Message);
    }

    [Fact]
    public async Task InProgressKeyReturnsConflict()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var store = new IdempotencyStore(context);
        var request = BuildContext("in-progress-key", 1m);

        await store.TryReserveAsync(new DigitalBanking.Domain.Entities.IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Key = "in-progress-key",
            Endpoint = "POST /api/transactions/deposit",
            RequestHash = ComputeHash(request),
            CreatedAt = DateTime.UtcNow
        });

        var filter = new IdempotencyFilter(store, NullLogger<IdempotencyFilter>.Instance);
        var executions = 0;
        await filter.OnActionExecutionAsync(request, Next(request, () => executions++, new OkObjectResult(new { success = true })));

        Assert.Equal(0, executions);
        var conflict = Assert.IsType<ConflictObjectResult>(request.Result);
        var body = Assert.IsType<ApiResponse>(conflict.Value);
        Assert.Contains("still in progress", body.Message);
    }

    [Fact]
    public async Task MissingHeaderExecutesWithoutReservation()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var filter = new IdempotencyFilter(new IdempotencyStore(context), NullLogger<IdempotencyFilter>.Instance);
        var executions = 0;

        var request = BuildContext(null, 1m);
        await filter.OnActionExecutionAsync(request, Next(request, () => executions++, new OkObjectResult(new { success = true })));

        Assert.Equal(1, executions);
        Assert.Equal(0, context.IdempotencyRecords.Count());
    }

    [Fact]
    public async Task StoreUniqueIndexRejectsSecondReservationAndRemoveReleasesKey()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var store = new IdempotencyStore(context);

        var record = new DigitalBanking.Domain.Entities.IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Key = "store-key",
            Endpoint = "POST /api/test",
            RequestHash = "synthetic-hash",
            CreatedAt = DateTime.UtcNow
        };
        Assert.True(await store.TryReserveAsync(record));
        Assert.False(await store.TryReserveAsync(new DigitalBanking.Domain.Entities.IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Key = "store-key",
            Endpoint = "POST /api/test",
            RequestHash = "synthetic-hash",
            CreatedAt = DateTime.UtcNow
        }));

        await store.CompleteAsync("POST /api/test", "store-key", 200, "{\"ok\":true}");
        var completed = await store.FindAsync("POST /api/test", "store-key");
        Assert.NotNull(completed);
        Assert.Equal(200, completed!.StatusCode);
        Assert.NotNull(completed.CompletedAt);
        Assert.Equal("{\"ok\":true}", completed.ResponseBody);

        await store.RemoveAsync("POST /api/test", "store-key");
        Assert.Null(await store.FindAsync("POST /api/test", "store-key"));
    }

    [Fact]
    public async Task FailedActionReleasesReservationSoClientCanRetry()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var filter = new IdempotencyFilter(new IdempotencyStore(context), NullLogger<IdempotencyFilter>.Instance);
        var request = BuildContext("retry-key", 1m);

        ActionExecutionDelegate failing = () =>
            Task.FromResult(new ActionExecutedContext(
                new ActionContext(request.HttpContext, request.RouteData, request.ActionDescriptor),
                Array.Empty<IFilterMetadata>(),
                new object())
            {
                Exception = new InvalidOperationException("synthetic failure")
            });

        await filter.OnActionExecutionAsync(request, failing);
        Assert.Null(await context.IdempotencyRecords.SingleOrDefaultAsync(x => x.Key == "retry-key"));
    }

    private static ActionExecutingContext BuildContext(string? key, decimal amount)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.Path = "/api/transactions/deposit";
        if (key is not null)
            http.Request.Headers[IdempotencyFilter.HeaderName] = key;

        var actionContext = new ActionContext(http, new RouteData(), new ControllerActionDescriptor());

        var arguments = new Dictionary<string, object?>
        {
            ["request"] = new DepositRequest { AccountId = FixedAccountId, Amount = amount },
            ["cancellationToken"] = CancellationToken.None
        };

        return new ActionExecutingContext(actionContext, Array.Empty<IFilterMetadata>(), arguments, new object());
    }

    private static ActionExecutionDelegate Next(ActionExecutingContext context, Action onExecute, IActionResult result) =>
        () =>
        {
            onExecute();
            return Task.FromResult(new ActionExecutedContext(
                new ActionContext(context.HttpContext, context.RouteData, context.ActionDescriptor),
                Array.Empty<IFilterMetadata>(),
                new object())
            {
                Result = result
            });
        };

    private static string ComputeHash(ActionExecutingContext context)
    {
        var normalized = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, value) in context.ActionArguments)
        {
            if (value is CancellationToken) continue;
            normalized[name] = value;
        }

        var json = JsonSerializer.Serialize(normalized, HashOptions);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
