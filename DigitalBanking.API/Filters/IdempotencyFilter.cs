using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DigitalBanking.API.Filters;

/// <summary>
/// Durable Idempotency-Key handling. The reservation is written before the action
/// runs, so two concurrent requests cannot both move money. A completed key
/// replays the original status/body; an in-progress key returns 409.
/// </summary>
public sealed class IdempotencyFilter : IAsyncActionFilter
{
    public const string HeaderName = "Idempotency-Key";
    private const int MaxKeyLength = 128;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IIdempotencyStore _store;
    private readonly ILogger<IdempotencyFilter> _logger;

    public IdempotencyFilter(IIdempotencyStore store, ILogger<IdempotencyFilter> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var rawKey = context.HttpContext.Request.Headers[HeaderName].ToString().Trim();
        if (rawKey.Length == 0)
        {
            await next();
            return;
        }

        if (rawKey.Length > MaxKeyLength)
        {
            context.Result = new BadRequestObjectResult(
                ApiResponse.Failure($"Idempotency-Key cannot exceed {MaxKeyLength} characters."));
            return;
        }

        var endpoint = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
        var requestHash = ComputeRequestHash(context.ActionArguments);
        var userId = TryGetUserId(context.HttpContext.User);

        var existing = await _store.FindAsync(endpoint, rawKey, context.HttpContext.RequestAborted);
        if (existing is not null)
        {
            ReplayOrConflict(context, existing, requestHash);
            return;
        }

        var reservation = new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Key = rawKey,
            Endpoint = endpoint,
            UserId = userId,
            RequestHash = requestHash,
            CreatedAt = DateTime.UtcNow
        };

        if (!await _store.TryReserveAsync(reservation, context.HttpContext.RequestAborted))
        {
            // Another request won the unique-index race; read its state.
            var winner = await _store.FindAsync(endpoint, rawKey, context.HttpContext.RequestAborted);
            if (winner is null)
            {
                context.Result = new ConflictObjectResult(
                    ApiResponse.Failure("A request with this Idempotency-Key is already being processed."));
                return;
            }

            ReplayOrConflict(context, winner, requestHash);
            return;
        }

        var executed = await next();

        if (executed.Exception is not null && !executed.ExceptionHandled || executed.Canceled)
        {
            try
            {
                await _store.RemoveAsync(endpoint, rawKey, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to release an idempotency reservation.");
            }

            return;
        }

        var (statusCode, body) = DescribeResult(executed.Result);
        try
        {
            await _store.CompleteAsync(endpoint, rawKey, statusCode, body, CancellationToken.None);
            context.HttpContext.Response.Headers["Idempotency-Key"] = rawKey;
        }
        catch (Exception ex)
        {
            // The action already succeeded. Keep the reservation in-progress so a
            // retry cannot apply the money movement a second time.
            _logger.LogError(ex, "Failed to complete an idempotency reservation.");
        }
    }

    private static void ReplayOrConflict(ActionExecutingContext context, IdempotencyRecord record, string requestHash)
    {
        if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal))
        {
            context.Result = new ConflictObjectResult(
                ApiResponse.Failure("Idempotency-Key was already used with a different request payload."));
            return;
        }

        if (record.StatusCode is null)
        {
            context.Result = new ConflictObjectResult(
                ApiResponse.Failure("A request with this Idempotency-Key is still in progress."));
            return;
        }

        context.HttpContext.Response.Headers["Idempotency-Replayed"] = "true";
        context.Result = record.ResponseBody is null
            ? new StatusCodeResult(record.StatusCode.Value)
            : new ContentResult
            {
                StatusCode = record.StatusCode.Value,
                Content = record.ResponseBody,
                ContentType = "application/json; charset=utf-8"
            };
    }

    private static (int StatusCode, string? Body) DescribeResult(IActionResult? result)
    {
        switch (result)
        {
            case ObjectResult objectResult:
                var status = objectResult.StatusCode ?? StatusCodes.Status200OK;
                var body = objectResult.Value is null ? null : JsonSerializer.Serialize(objectResult.Value, SerializerOptions);
                return (status, body);

            case JsonResult jsonResult:
                return (jsonResult.StatusCode ?? StatusCodes.Status200OK,
                    jsonResult.Value is null ? null : JsonSerializer.Serialize(jsonResult.Value, SerializerOptions));

            case ContentResult contentResult:
                return (contentResult.StatusCode ?? StatusCodes.Status200OK, contentResult.Content);

            case StatusCodeResult statusCodeResult:
                return (statusCodeResult.StatusCode, null);

            case null:
                return (StatusCodes.Status200OK, null);

            default:
                return (StatusCodes.Status200OK, null);
        }
    }

    private static string ComputeRequestHash(IDictionary<string, object?> arguments)
    {
        var normalized = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, value) in arguments)
        {
            if (value is CancellationToken) continue;
            normalized[name] = value;
        }

        var json = JsonSerializer.Serialize(normalized, SerializerOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private static Guid? TryGetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
