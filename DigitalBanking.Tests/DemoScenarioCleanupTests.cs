using DigitalBanking.Application.Common;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace DigitalBanking.Tests;

/// <summary>
/// The reconciliation row -> exception FK is NO ACTION on SQL Server to avoid a
/// multiple-cascade-path failure. Demo cleanup must therefore remove exceptions
/// before rows/batches, and existing SQLite/FK behavior must remain clean.
/// </summary>
public class DemoScenarioCleanupTests
{
    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "DigitalBanking.Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public async Task DemoResetRemovesReconciliationExceptionsRowsAndBatchInOrder()
    {
        var db = await TestDb.CreateAsync();
        await using var _ = db;
        await using var context = db.NewContext();
        var (owner, ownerActor) = await TestSupport.AddActorAsync(context, "demo-reset-owner", UserRole.Owner);
        var audit = new AuditService(context);
        var authorization = new ApprovalAuthorizationService(context);
        var service = new DemoScenarioService(
            context,
            new PaymentApprovalService(context, TestSupport.CreateMapper(), audit,
                new CustomerAccessService(context), authorization),
            audit,
            new DelegationService(context, audit, authorization),
            authorization,
            new DevelopmentEnvironment());

        var batch = new ReconciliationBatch
        {
            Id = Guid.NewGuid(),
            FileName = "demo-reset.csv",
            FileHash = Guid.NewGuid().ToString("N"),
            ScopeKey = Guid.NewGuid().ToString("N"),
            RowCount = 1,
            Status = ReconciliationBatchStatus.Imported,
            IsDemo = true,
            ImportedByUserId = owner.Id,
            ImportedByUsername = owner.Username,
            CreatedAt = DateTime.UtcNow,
            ImportedAt = DateTime.UtcNow,
            OnlyExternalCount = 1
        };
        var row = new ReconciliationRow
        {
            Id = Guid.NewGuid(),
            BatchId = batch.Id,
            LineNumber = 1,
            ExternalReference = "DEMO-RESET-EXT",
            InternalReference = Guid.NewGuid().ToString("D"),
            Amount = 1m,
            Currency = "TRY",
            TransactionDate = DateTime.UtcNow,
            Direction = ReconciliationDirection.Debit,
            MatchStatus = ReconciliationMatchStatus.OnlyExternal
        };
        var exception = new ReconciliationException
        {
            Id = Guid.NewGuid(),
            BatchId = batch.Id,
            RowId = row.Id,
            ExternalReference = row.ExternalReference,
            InternalReference = row.InternalReference,
            Status = ReconciliationExceptionStatus.Open,
            Description = "Synthetic demo cleanup exception",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ReconciliationBatches.Add(batch);
        context.ReconciliationRows.Add(row);
        context.ReconciliationExceptions.Add(exception);
        await context.SaveChangesAsync();

        var result = await service.ResetAsync(ownerActor);

        Assert.Equal(1, result.RemovedBatches);
        await using var verify = db.NewContext();
        Assert.Empty(await verify.ReconciliationExceptions.ToListAsync());
        Assert.Empty(await verify.ReconciliationRows.ToListAsync());
        Assert.Empty(await verify.ReconciliationBatches.ToListAsync());
        Assert.Contains(await verify.AuditLogs.ToListAsync(),
            x => x.Action == "DEMO_RESET" && x.Details != null
                 && x.Details.Contains("RemovedReconciliationRows: 1")
                 && x.Details.Contains("RemovedReconciliationExceptions: 1"));
    }
}
