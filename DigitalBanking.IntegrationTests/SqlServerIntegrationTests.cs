using System.Data;
using System.Data.Common;
using System.Security.Claims;
using System.Text;
using DigitalBanking.API.Filters;
using DigitalBanking.Application.DTOs.ApprovalRequests;
using DigitalBanking.Application.DTOs.Reconciliation;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using DigitalBanking.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DigitalBanking.IntegrationTests;

[Collection("SqlServerIntegration")]
public class SqlServerIntegrationTests
{
    private readonly SqlServerDatabaseFixture _db;

    public SqlServerIntegrationTests(SqlServerDatabaseFixture db) => _db = db;

    [SqlServerFact]
    public async Task MigrationsApplyOnAnEmptyDatabaseAndAccountMetadataDefaultsExist()
    {
        await using var context = _db.NewContext();
        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();

        Assert.Contains("20261020203345_PaymentApprovalGuidApproversAndIdempotency", applied);
        Assert.Contains("20261020212050_AddCustomerOwnershipTimelineReconciliation", applied);
        Assert.Contains("20261020213624_AddOwnerDelegationAndApprovalRequests", applied);
        Assert.Contains("20261021011500_EnforceSingleOwner", applied);
        Assert.Contains("20261021023000_AddAccountMetadata", applied);
        Assert.Contains("20261022191101_AddDoubleEntryJournalAndApprovalAssignment", applied);
        Assert.Contains("20261023164046_RepairMissingAccountsCurrency", applied);

        Assert.True(await TableExistsAsync(context, "LegacyPaymentApprovalIds"));
        Assert.True(await TableExistsAsync(context, "JournalEntries"));
        Assert.True(await TableExistsAsync(context, "JournalLines"));
        Assert.Equal(1, await ScalarAsync(context,
            "SELECT COUNT(*) FROM sys.indexes WHERE name='IX_Users_Role_OwnerUnique' AND is_unique=1"));
        Assert.Equal(1, await ScalarAsync(context,
            "SELECT COUNT(*) FROM sys.indexes WHERE name='IX_JournalEntries_SourceKey' AND is_unique=1"));
        Assert.Equal(1, await ScalarAsync(context,
            "SELECT COUNT(*) FROM sys.indexes WHERE name='IX_PaymentApprovals_AssignedToUserId_Status'"));
        Assert.Equal(1, await ScalarAsync(context,
            "SELECT COUNT(*) FROM sys.foreign_keys WHERE name='FK_ReconciliationExceptions_ReconciliationRows_RowId' AND delete_referential_action_desc='NO_ACTION'"));

        var customer = await _db.AddCustomerAsync(context);
        await using (var command = context.Database.GetDbConnection().CreateCommand())
        {
            if (command.Connection!.State != ConnectionState.Open) await command.Connection.OpenAsync();
            command.CommandText =
                "INSERT INTO [Accounts] ([Id],[AccountNumber],[Balance],[Currency],[CustomerId],[IsDemo],[CreatedDate]) " +
                "VALUES (NEWID(),'IT-DEFAULT-1',0.00,'TRY',@customerId,0,SYSUTCDATETIME());";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@customerId";
            parameter.Value = customer.Id;
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync();
        }
        context.ChangeTracker.Clear();
        var defaulted = await context.Accounts.SingleAsync(x => x.AccountNumber == "IT-DEFAULT-1");
        Assert.Equal("Vadesiz Hesap", defaulted.AccountName);
        Assert.Equal(AccountType.Checking, defaulted.AccountType);

        var legacyAdmin = new User
        {
            Id = Guid.NewGuid(), Username = "it-legacy-admin-" + Guid.NewGuid().ToString("N")[..8],
            Email = "it-legacy-admin@demo.invalid", FirstName = "Legacy", LastName = "Admin",
            PasswordHash = "synthetic", Role = UserRole.Admin, Status = UserStatus.Active,
            CreatedDate = DateTime.UtcNow, ActivatedAt = DateTime.UtcNow
        };
        var legacyUser = new User
        {
            Id = Guid.NewGuid(), Username = "it-legacy-user-" + Guid.NewGuid().ToString("N")[..8],
            Email = "it-legacy-user@demo.invalid", FirstName = "Legacy", LastName = "User",
            PasswordHash = "synthetic", Role = UserRole.User, Status = UserStatus.Active,
            CreatedDate = DateTime.UtcNow, ActivatedAt = DateTime.UtcNow
        };
        context.Users.AddRange(legacyAdmin, legacyUser);
        await context.SaveChangesAsync();
        Assert.Equal((int)UserRole.Admin, (int)(await context.Users.AsNoTracking().SingleAsync(x => x.Id == legacyAdmin.Id)).Role);
        Assert.Equal((int)UserRole.User, (int)(await context.Users.AsNoTracking().SingleAsync(x => x.Id == legacyUser.Id)).Role);

        var columns = await ColumnsAsync(context, "Accounts");
        Assert.True(columns.TryGetValue("AccountName", out var accountName));
        Assert.Equal("NO", accountName.Nullable);
        Assert.NotNull(accountName.Default);
        Assert.True(columns.TryGetValue("AccountType", out var accountType));
        Assert.Equal("NO", accountType.Nullable);
        Assert.NotNull(accountType.Default);
        Assert.True(columns.TryGetValue("Currency", out var currencyColumn));
        Assert.Equal("NO", currencyColumn.Nullable);
        Assert.NotNull(currencyColumn.Default);

        var approvalColumns = await ColumnsAsync(context, "PaymentApprovals");
        Assert.True(approvalColumns.TryGetValue("RequestedAccountName", out _));
        Assert.True(approvalColumns.TryGetValue("RequestedAccountType", out _));
        Assert.True(approvalColumns.TryGetValue("AssignedToUserId", out _));
        Assert.True(approvalColumns.TryGetValue("AssignedToUsername", out _));
        Assert.True(approvalColumns.TryGetValue("AssignedAt", out _));
    }

    [SqlServerFact]
    public async Task DecimalBoundariesInclusiveLimitAndRepeatedCentMovementsAreExact()
    {
        await using var context = _db.NewContext();
        var (owner, ownerActor) = await _db.AddActorAsync(context, UserRole.Owner);
        var (delegated, delegatedActor) = await _db.AddActorAsync(context, UserRole.DelegatedApprover);
        var (customerUser, customerActor) = await _db.AddActorAsync(context, UserRole.Customer);
        var customer = await _db.AddCustomerAsync(context, customerUser.Id);
        var otherCustomer = await _db.AddCustomerAsync(context);
        var source = await _db.AddAccountAsync(context, customer.Id, 300_000m);
        var destination = await _db.AddAccountAsync(context, otherCustomer.Id, 0m);
        context.ApprovalDelegations.Add(new ApprovalDelegation
        {
            Id = Guid.NewGuid(), UserId = delegated.Id, Operation = ApprovalOperation.Transfer,
            Currency = "TRY", MaxAmount = 100_000m, IsActive = true,
            CreatedByOwnerId = owner.Id, CreatedByOwnerUsername = owner.Username,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var service = _db.NewApprovalService(context);

        var below = await service.CreateCustomerTransferRequestAsync(source.Id, destination.Id, 99_999.99m, "below", customerActor);
        await service.ApprovePaymentAsync(below.Id, delegatedActor);
        var atLimit = await service.CreateCustomerTransferRequestAsync(source.Id, destination.Id, 100_000m, "at", customerActor);
        await service.ApprovePaymentAsync(atLimit.Id, delegatedActor);
        var above = await service.CreateCustomerTransferRequestAsync(source.Id, destination.Id, 100_000.01m, "above", customerActor);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ApprovePaymentAsync(above.Id, delegatedActor));
        await service.ApprovePaymentAsync(above.Id, ownerActor);

        for (var i = 0; i < 10; i++)
        {
            var cent = await service.CreateCustomerOperationRequestAsync(new CreateCustomerOperationRequest
            {
                RequestType = "Deposit", AccountId = source.Id, Amount = 0.01m, Description = "cent"
            }, customerActor);
            await service.ApprovePaymentAsync(cent.Id, ownerActor);
        }

        Assert.Equal(0.10m, (await context.Accounts.SingleAsync(x => x.Id == source.Id)).Balance);
        Assert.Equal(300_000.00m, (await context.Accounts.SingleAsync(x => x.Id == destination.Id)).Balance);
        Assert.Equal(0.10m, decimal.Round((await context.Accounts.SingleAsync(x => x.Id == source.Id)).Balance, 2));

        var journals = await context.JournalEntries.Include(x => x.Lines).ToListAsync();
        Assert.NotEmpty(journals);
        Assert.All(journals, entry =>
        {
            Assert.Equal(entry.Lines.Sum(x => x.Side == JournalSide.Debit ? x.Amount : 0m),
                entry.Lines.Sum(x => x.Side == JournalSide.Credit ? x.Amount : 0m));
            Assert.Single(entry.Lines.Select(x => x.Currency).Distinct());
        });
        var atLimitJournal = journals.Single(x => x.PaymentApprovalId == atLimit.Id);
        Assert.Equal(2, atLimitJournal.Lines.Count);
        Assert.Equal(100_000m, atLimitJournal.Lines.Sum(x => x.Side == JournalSide.Debit ? x.Amount : 0m));
    }

    [SqlServerFact]
    public async Task ApprovalFailureRollsBackBalanceApprovalLedgerAndAudit()
    {
        await using var context = _db.NewContext();
        var (owner, ownerActor) = await _db.AddActorAsync(context, UserRole.Owner);
        var (customerUser, customerActor) = await _db.AddActorAsync(context, UserRole.Customer);
        var customer = await _db.AddCustomerAsync(context, customerUser.Id);
        var other = await _db.AddCustomerAsync(context);
        var source = await _db.AddAccountAsync(context, customer.Id, 100m);
        var destination = await _db.AddAccountAsync(context, other.Id, 0m);
        var service = _db.NewApprovalService(context);
        var payment = await service.CreateCustomerTransferRequestAsync(source.Id, destination.Id, 10m, "rollback", customerActor);
        context.Accounts.Remove(await context.Accounts.SingleAsync(x => x.Id == destination.Id));
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => service.ApprovePaymentAsync(payment.Id, ownerActor));
        context.ChangeTracker.Clear();
        Assert.Equal(PaymentApprovalStatus.Pending,
            (await context.PaymentApprovals.SingleAsync(x => x.Id == payment.Id)).Status);
        Assert.Equal(100m, (await context.Accounts.SingleAsync(x => x.Id == source.Id)).Balance);
        Assert.False(await context.Transactions.AnyAsync(x => x.CorrelationId == payment.CorrelationId));
        Assert.False(await context.AuditLogs.AnyAsync(x => x.Action == "APPROVAL_REQUEST_APPROVED" && x.EntityId == payment.Id.ToString()));

        // Force the journal insert to violate the unique SourceKey after balance
        // and legacy transaction writes, proving the whole approval rolls back.
        context.ChangeTracker.Clear();
        var deposit = await service.CreateCustomerOperationRequestAsync(new CreateCustomerOperationRequest
        {
            RequestType = "Deposit", AccountId = source.Id, Amount = 5m,
            Description = "journal rollback probe"
        }, customerActor);
        var sourceBalance = (await context.Accounts.SingleAsync(x => x.Id == source.Id)).Balance;
        context.JournalEntries.Add(new JournalEntry
        {
            Id = Guid.NewGuid(),
            SourceKey = JournalPoster.SourceKeyForApproval(deposit.Id),
            SourceOperation = JournalSourceOperation.Deposit,
            PaymentApprovalId = deposit.Id,
            CorrelationId = deposit.CorrelationId,
            Currency = "TRY",
            Description = "pre-existing duplicate",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<ConcurrencyException>(() => service.ApprovePaymentAsync(deposit.Id, ownerActor));
        context.ChangeTracker.Clear();
        Assert.Equal(PaymentApprovalStatus.Pending,
            (await context.PaymentApprovals.SingleAsync(x => x.Id == deposit.Id)).Status);
        Assert.Equal(sourceBalance, (await context.Accounts.SingleAsync(x => x.Id == source.Id)).Balance);
        Assert.False(await context.Transactions.AnyAsync(x => x.CorrelationId == deposit.CorrelationId));
        Assert.Equal(1, await context.JournalEntries.CountAsync(x => x.PaymentApprovalId == deposit.Id));
        Assert.False(await context.AuditLogs.AnyAsync(x => x.Action == "APPROVAL_REQUEST_APPROVED" && x.EntityId == deposit.Id.ToString()));
    }

    [SqlServerFact]
    public async Task ConcurrentApprovalsOnSeparateConnectionsMoveMoneyOnce()
    {
        await using var setup = _db.NewContext();
        var (_, ownerActor) = await _db.AddActorAsync(setup, UserRole.Owner);
        var (customerUser, customerActor) = await _db.AddActorAsync(setup, UserRole.Customer);
        var customer = await _db.AddCustomerAsync(setup, customerUser.Id);
        var other = await _db.AddCustomerAsync(setup);
        var source = await _db.AddAccountAsync(setup, customer.Id, 100m);
        var destination = await _db.AddAccountAsync(setup, other.Id, 0m);
        var service = _db.NewApprovalService(setup);
        var payment = await service.CreateCustomerTransferRequestAsync(source.Id, destination.Id, 10m, "concurrent", customerActor);

        async Task<Exception?> Attempt()
        {
            await using var context = _db.NewContext();
            try
            {
                await _db.NewApprovalService(context).ApprovePaymentAsync(payment.Id, ownerActor);
                return null;
            }
            catch (Exception ex) { return ex; }
        }

        var results = await Task.WhenAll(Attempt(), Attempt());
        Assert.Single(results, x => x is null);
        Assert.DoesNotContain(results, x => x is DbUpdateException);
        var loser = Assert.Single(results, x => x is not null);
        Assert.True(loser is BusinessRuleException or ConcurrencyException,
            $"Concurrent approval returned {loser.GetType().Name}, expected BusinessRuleException or ConcurrencyException.");

        await using var verify = _db.NewContext();
        Assert.Equal(PaymentApprovalStatus.Approved,
            (await verify.PaymentApprovals.SingleAsync(x => x.Id == payment.Id)).Status);
        Assert.Equal(90m, (await verify.Accounts.SingleAsync(x => x.Id == source.Id)).Balance);
        Assert.Equal(2, await verify.Transactions.CountAsync(x => x.CorrelationId == payment.CorrelationId));
        Assert.Equal(1, await verify.JournalEntries.CountAsync(x => x.PaymentApprovalId == payment.Id));
        Assert.Equal(1, await verify.AuditLogs.CountAsync(x =>
            x.Action == "APPROVAL_REQUEST_APPROVED" && x.EntityId == payment.Id.ToString()));
    }

    [SqlServerFact]
    public async Task ReconciliationReplayAndDifferentReviewerExceptionFlowKeepBalancesUnchanged()
    {
        await using var context = _db.NewContext();
        var (owner, ownerActor) = await _db.AddActorAsync(context, UserRole.Owner);
        var (reviewer, reviewerActor) = await _db.AddActorAsync(context, UserRole.DelegatedApprover);
        var (customerUser, customerActor) = await _db.AddActorAsync(context, UserRole.Customer);
        var customer = await _db.AddCustomerAsync(context, customerUser.Id);
        var other = await _db.AddCustomerAsync(context);
        var source = await _db.AddAccountAsync(context, customer.Id, 100m);
        var destination = await _db.AddAccountAsync(context, other.Id, 0m);
        var payments = _db.NewApprovalService(context);
        var transfer = await payments.CreateCustomerTransferRequestAsync(source.Id, destination.Id, 10m, "recon", customerActor);
        await payments.ApprovePaymentAsync(transfer.Id, ownerActor);
        context.ApprovalDelegations.Add(new ApprovalDelegation
        {
            Id = Guid.NewGuid(), UserId = reviewer.Id, Operation = ApprovalOperation.ReconciliationException,
            Currency = null, MaxAmount = null, IsActive = true,
            CreatedByOwnerId = owner.Id, CreatedByOwnerUsername = owner.Username,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var approval = await context.PaymentApprovals.SingleAsync(x => x.Id == transfer.Id);
        var csv = "external_reference,internal_reference,amount,currency,transaction_date,direction\n" +
                  $"IT-MATCH,{approval.CorrelationId},10.00,TRY,{DateTime.UtcNow:yyyy-MM-dd},Debit\n" +
                  $"IT-ONLY,{Guid.NewGuid()},5.00,TRY,{DateTime.UtcNow:yyyy-MM-dd},Debit\n";
        var recon = _db.NewReconciliationService(context);
        var preview = await recon.PreviewAsync(Encoding.UTF8.GetBytes(csv), "it-recon.csv",
            null, null, null, null, false, ownerActor);
        var imported = await recon.ImportAsync(preview.Batch.Id, ownerActor);
        Assert.True(imported.Batch.MatchedCount >= 1);
        Assert.True(imported.Batch.OnlyExternalCount >= 1);
        var replay = await recon.PreviewAsync(Encoding.UTF8.GetBytes(csv), "it-recon.csv",
            null, null, null, null, false, ownerActor);
        Assert.True(replay.Replayed);
        Assert.Equal(preview.Batch.Id, replay.Batch.Id);

        var only = imported.Rows.First(x => x.MatchStatus == "OnlyExternal");
        var balanceBefore = (await context.Accounts.SingleAsync(x => x.Id == source.Id)).Balance;
        var exception = await recon.CreateExceptionAsync(new CreateReconciliationExceptionRequest
        {
            RowId = only.Id, Description = "Synthetic integration difference"
        }, ownerActor);
        await recon.ReviewAsync(exception.Id, ownerActor);
        await recon.ProposeAsync(exception.Id, "Operational note only", ownerActor);
        await recon.ApproveAsync(exception.Id, reviewerActor);
        var closed = await recon.GetExceptionAsync(exception.Id, ownerActor);
        Assert.Equal("Closed", closed.Status);
        Assert.Equal(balanceBefore, (await context.Accounts.SingleAsync(x => x.Id == source.Id)).Balance);

        // NO ACTION on Row -> Exceptions requires explicit dependent cleanup. The
        // demo reset uses this order; verify it also works on real SQL Server.
        await using var cleanup = _db.NewContext();
        var batchId = imported.Batch.Id;
        var cleanupExceptions = await cleanup.ReconciliationExceptions
            .Where(x => x.BatchId == batchId).ToListAsync();
        var cleanupRows = await cleanup.ReconciliationRows
            .Where(x => x.BatchId == batchId).ToListAsync();
        var cleanupBatch = await cleanup.ReconciliationBatches.SingleAsync(x => x.Id == batchId);
        cleanup.ReconciliationExceptions.RemoveRange(cleanupExceptions);
        cleanup.ReconciliationRows.RemoveRange(cleanupRows);
        cleanup.ReconciliationBatches.Remove(cleanupBatch);
        await cleanup.SaveChangesAsync();
        Assert.False(await cleanup.ReconciliationExceptions.AnyAsync(x => x.BatchId == batchId));
        Assert.False(await cleanup.ReconciliationRows.AnyAsync(x => x.BatchId == batchId));
        Assert.False(await cleanup.ReconciliationBatches.AnyAsync(x => x.Id == batchId));
    }

    [SqlServerFact]
    public async Task IdempotencyFilterReplaysCompletedRequestAndRejectsDifferentPayload()
    {
        await using var context = _db.NewContext();
        var store = new IdempotencyStore(context);
        var filter = new IdempotencyFilter(store, NullLogger<IdempotencyFilter>.Instance);

        ActionExecutingContext Make(string key, int amount, ClaimsPrincipal? user = null)
        {
            var http = new DefaultHttpContext { User = user ?? new ClaimsPrincipal() };
            http.Request.Method = "POST";
            http.Request.Path = "/api/it-idempotency";
            http.Request.Headers[IdempotencyFilter.HeaderName] = key;
            return new ActionExecutingContext(
                new ActionContext(http, new RouteData(), new ActionDescriptor()),
                Array.Empty<IFilterMetadata>(),
                new Dictionary<string, object?> { ["amount"] = amount },
                controller: new object());
        }

        static ActionExecutionDelegate Next(ActionExecutingContext context) =>
            () => Task.FromResult(new ActionExecutedContext(
                context, Array.Empty<IFilterMetadata>(), new object())
            { Result = new OkObjectResult(new { ok = true }) });

        var first = Make("it-key-" + Guid.NewGuid().ToString("N"), 10);
        await filter.OnActionExecutionAsync(first, Next(first));
        Assert.Null(first.Result);

        var replay = Make(first.HttpContext.Request.Headers[IdempotencyFilter.HeaderName].ToString(), 10);
        await filter.OnActionExecutionAsync(replay, () => throw new InvalidOperationException("must replay"));
        Assert.IsType<ContentResult>(replay.Result);
        Assert.Equal("true", replay.HttpContext.Response.Headers["Idempotency-Replayed"].ToString());

        var conflict = Make(first.HttpContext.Request.Headers[IdempotencyFilter.HeaderName].ToString(), 11);
        await filter.OnActionExecutionAsync(conflict, () => throw new InvalidOperationException("must conflict"));
        Assert.IsType<ConflictObjectResult>(conflict.Result);
    }

    [SqlServerFact]
    public async Task SelfApprovalRevocationAndStaleRoleAreRejectedByLiveState()
    {
        await using var context = _db.NewContext();
        var (owner, ownerActor) = await _db.AddActorAsync(context, UserRole.Owner);
        var (delegated, delegatedActor) = await _db.AddActorAsync(context, UserRole.DelegatedApprover);
        var (customerUser, customerActor) = await _db.AddActorAsync(context, UserRole.Customer);
        var customer = await _db.AddCustomerAsync(context, customerUser.Id);
        var other = await _db.AddCustomerAsync(context);
        var source = await _db.AddAccountAsync(context, customer.Id, 50m);
        var destination = await _db.AddAccountAsync(context, other.Id, 0m);
        var delegation = new ApprovalDelegation
        {
            Id = Guid.NewGuid(), UserId = delegated.Id, Operation = ApprovalOperation.Transfer,
            Currency = "TRY", MaxAmount = 100m, IsActive = true,
            CreatedByOwnerId = owner.Id, CreatedByOwnerUsername = owner.Username,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        context.ApprovalDelegations.Add(delegation);
        await context.SaveChangesAsync();
        var service = _db.NewApprovalService(context);

        var own = await service.CreateApprovalRequestAsync(source.Id, destination.Id, 5m, "owner own", ownerActor);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ApprovePaymentAsync(own.Id, ownerActor));
        await service.CancelAsync(own.Id, ownerActor);

        delegation.IsActive = false;
        await context.SaveChangesAsync();
        var revoked = await service.CreateCustomerTransferRequestAsync(source.Id, destination.Id, 5m, "revoked", customerActor);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ApprovePaymentAsync(revoked.Id, delegatedActor));

        var user = await context.Users.SingleAsync(x => x.Id == delegated.Id);
        user.Role = UserRole.Customer;
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ApprovePaymentAsync(revoked.Id, delegatedActor));
    }

    [SqlServerFact]
    public async Task CustomerIdorAndOwnershipRenameRulesAreEnforced()
    {
        await using var context = _db.NewContext();
        var (_, actorA) = await _db.AddActorAsync(context, UserRole.Customer);
        var (_, actorB) = await _db.AddActorAsync(context, UserRole.Customer);
        var customerA = await _db.AddCustomerAsync(context, actorA.UserId);
        var customerB = await _db.AddCustomerAsync(context, actorB.UserId);
        var accountA = await _db.AddAccountAsync(context, customerA.Id, 10m);
        var accountB = await _db.AddAccountAsync(context, customerB.Id, 10m);
        var audit = new AuditService(context);
        var access = new CustomerAccessService(context);
        var payment = _db.NewApprovalService(context);
        var portal = new CustomerPortalService(
            context, access, new AccountService(context, audit),
            new TransactionService(context, audit, new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Banking:DirectTransferLimit"] = "50000" }).Build()),
            payment, _db.Mapper);

        await Assert.ThrowsAsync<NotFoundException>(() => portal.GetAccountAsync(accountB.Id, actorA));
        await Assert.ThrowsAsync<NotFoundException>(() => portal.RenameAccountAsync(accountB.Id, "IDOR", actorA));
        var renamed = await portal.RenameAccountAsync(accountA.Id, "  Birikim  ", actorA);
        Assert.Equal("Birikim", renamed.AccountName);
        await Assert.ThrowsAsync<BusinessRuleException>(() => portal.RenameAccountAsync(accountA.Id, "<script>x</script>", actorA));
        await Assert.ThrowsAsync<BusinessRuleException>(() => portal.RenameAccountAsync(accountA.Id, "   ", actorA));
    }

    private static async Task<bool> TableExistsAsync(BankDbContext context, string table)
    {
        var value = await ScalarAsync(context,
            $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='{table}'");
        return value == 1;
    }

    private static async Task<int> ScalarAsync(BankDbContext context, string sql)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<Dictionary<string, (string Nullable, string? Default)>> ColumnsAsync(
        BankDbContext context, string table)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COLUMN_NAME, IS_NULLABLE, COLUMN_DEFAULT FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='{table}'";
        var result = new Dictionary<string, (string, string?)>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetString(0)] = (reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2));
        return result;
    }
}
