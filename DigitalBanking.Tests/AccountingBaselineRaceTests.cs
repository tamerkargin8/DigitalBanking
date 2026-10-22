using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using DigitalBanking.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Tests;

/// <summary>
/// Baseline and an approval must not double-count the same customer liability.
/// SQLite is not SQL Server proof, but this exercises the real race path with
/// separate contexts and a serializable baseline transaction.
/// </summary>
public class AccountingBaselineRaceTests
{
    [Fact]
    public async Task BaselineAndApprovalRaceLeaveExactlyOneBaselineGap()
    {
        var path = Path.Combine(Path.GetTempPath(), $"digitalbanking-baseline-race-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<BankDbContext>()
            .UseSqlite($"Data Source={path};Default Timeout=15")
            .Options;

        try
        {
            Guid ownerId;
            Guid customerUserId;
            Guid accountId;

            await using (var setup = new BankDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                var owner = new User
                {
                    Id = Guid.NewGuid(), Username = "race-owner", Email = "race-owner@test.invalid",
                    FirstName = "Race", LastName = "Owner", PasswordHash = "synthetic",
                    Role = UserRole.Owner, Status = UserStatus.Active, CreatedDate = DateTime.UtcNow
                };
                var customerUser = new User
                {
                    Id = Guid.NewGuid(), Username = "race-customer", Email = "race-customer@test.invalid",
                    FirstName = "Race", LastName = "Customer", PasswordHash = "synthetic",
                    Role = UserRole.Customer, Status = UserStatus.Active, CreatedDate = DateTime.UtcNow
                };
                var customer = new Customer
                {
                    Id = Guid.NewGuid(), FirstName = "Race", LastName = "Customer",
                    IdentityNumber = "SYNTHETIC-RACE", UserId = customerUser.Id, CreatedDate = DateTime.UtcNow
                };
                var legacyAccount = new Account
                {
                    Id = Guid.NewGuid(), AccountNumber = "RACE-1000000001", CustomerId = customer.Id,
                    AccountName = "Legacy Race", AccountType = AccountType.Checking,
                    Currency = "TRY", Balance = 2_500m, IsDemo = true, CreatedDate = DateTime.UtcNow
                };
                setup.Users.AddRange(owner, customerUser);
                setup.Customers.Add(customer);
                setup.Accounts.Add(legacyAccount);
                setup.PaymentApprovals.Add(new PaymentApproval
                {
                    FromAccountId = legacyAccount.Id,
                    ToAccountId = legacyAccount.Id,
                    RequestType = ApprovalRequestType.Deposit,
                    RequestedCustomerId = customer.Id,
                    Amount = 500m,
                    Currency = "TRY",
                    Description = "race deposit",
                    Status = PaymentApprovalStatus.Pending,
                    RiskLevel = PaymentRiskLevel.Low,
                    RequestedByUserId = customerUser.Id,
                    CorrelationId = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow
                });
                await setup.SaveChangesAsync();

                ownerId = owner.Id;
                customerUserId = customerUser.Id;
                accountId = legacyAccount.Id;
            }

            var ownerActor = new ActorContext(ownerId, "race-owner", UserRole.Owner);
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            async Task<Exception?> PostBaselineAsync()
            {
                await gate.Task;
                await using var context = new BankDbContext(options);
                var service = new AccountingService(
                    context, new ApprovalAuthorizationService(context), new AuditService(context));
                try
                {
                    await service.PostBaselineAsync(ownerActor);
                    return null;
                }
                catch (Exception ex)
                {
                    return ex;
                }
            }

            async Task<Exception?> ApproveAsync()
            {
                await gate.Task;
                await using var context = new BankDbContext(options);
                var service = new PaymentApprovalService(
                    context, TestSupport.CreateMapper(), new AuditService(context),
                    new CustomerAccessService(context), new ApprovalAuthorizationService(context));
                try
                {
                    var pending = await context.PaymentApprovals.SingleAsync();
                    await service.ApprovePaymentAsync(pending.Id, ownerActor);
                    return null;
                }
                catch (Exception ex)
                {
                    return ex;
                }
            }

            var baselineTask = PostBaselineAsync();
            var approvalTask = ApproveAsync();
            gate.SetResult();
            await Task.WhenAll(baselineTask, approvalTask);

            // Concurrency can surface a safe busy/conflict error; repair must still
            // produce exactly one gap, never a duplicate baseline.
            await using (var repair = new BankDbContext(options))
            {
                var approval = await repair.PaymentApprovals.SingleAsync();
                if (approval.Status == PaymentApprovalStatus.Pending)
                {
                    var paymentService = new PaymentApprovalService(
                        repair, TestSupport.CreateMapper(), new AuditService(repair),
                        new CustomerAccessService(repair), new ApprovalAuthorizationService(repair));
                    await paymentService.ApprovePaymentAsync(approval.Id, ownerActor);
                }

                if (!await repair.JournalEntries.AnyAsync(x => x.SourceOperation == JournalSourceOperation.Baseline))
                {
                    var accounting = new AccountingService(
                        repair, new ApprovalAuthorizationService(repair), new AuditService(repair));
                    await accounting.PostBaselineAsync(ownerActor);
                }
            }

            await using var verify = new BankDbContext(options);
            var account = await verify.Accounts.SingleAsync(x => x.Id == accountId);
            Assert.Equal(3_000m, account.Balance);

            var entries = await verify.JournalEntries.Include(x => x.Lines).ToListAsync();
            var liability = entries.SelectMany(x => x.Lines)
                .Where(x => x.AccountId == accountId && x.AccountType == JournalAccountType.CustomerLiability)
                .Sum(x => x.Side == JournalSide.Credit ? x.Amount : -x.Amount);
            Assert.Equal(3_000m, liability);

            var baseline = entries.Where(x => x.SourceOperation == JournalSourceOperation.Baseline).ToList();
            Assert.Single(baseline);
            Assert.Equal(2_500m, baseline.Single().Lines
                .Where(x => x.AccountType == JournalAccountType.CustomerLiability)
                .Sum(x => x.Side == JournalSide.Credit ? x.Amount : -x.Amount));
            Assert.Equal(1, entries.Count(x => x.SourceOperation == JournalSourceOperation.Deposit));

            await using var trialContext = new BankDbContext(options);
            var trial = await new AccountingService(
                    trialContext, new ApprovalAuthorizationService(trialContext), new AuditService(trialContext))
                .GetTrialBalanceAsync("TRY", ownerActor);
            Assert.Equal(0m, trial.Currencies.Single().LiabilityDifference);
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                var file = path + suffix;
                if (File.Exists(file)) File.Delete(file);
            }
        }
    }
}
