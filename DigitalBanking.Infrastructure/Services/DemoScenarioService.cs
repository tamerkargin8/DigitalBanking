using System.Globalization;
using System.Text;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Demo;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DigitalBanking.Infrastructure.Services;

/// <summary>
/// Deterministic Development-only scenario seeder. Re-running is idempotent and
/// reset is scoped to records explicitly marked IsDemo; users/customers/accounts
/// and non-demo UAT data are preserved.
/// </summary>
public sealed class DemoScenarioService : IDemoScenarioService
{
    private const string SuccessfulKey = "DEMO:successful-transfer";
    private const string HighValueKey = "DEMO:high-value-maker-checker";
    private const string RejectedKey = "DEMO:rejected-payment";

    private readonly BankDbContext _context;
    private readonly IPaymentApprovalService _payments;
    private readonly IAuditService _audit;
    private readonly IDelegationService _delegations;
    private readonly IApprovalAuthorizationService _authorization;
    private readonly IHostEnvironment _environment;
    private readonly ILogger _logger;

    public DemoScenarioService(
        BankDbContext context,
        IPaymentApprovalService payments,
        IAuditService audit,
        IDelegationService delegations,
        IApprovalAuthorizationService authorization,
        IHostEnvironment environment)
    {
        _context = context;
        _payments = payments;
        _audit = audit;
        _delegations = delegations;
        _authorization = authorization;
        _environment = environment;
        _logger = Log.ForContext<DemoScenarioService>();
    }

    public async Task<List<DemoScenarioDto>> GetScenariosAsync(CancellationToken cancellationToken = default)
    {
        EnsureDevelopment();

        var payments = await _context.PaymentApprovals.AsNoTracking()
            .Where(x => x.IsDemo && (x.Description == SuccessfulKey || x.Description == HighValueKey || x.Description == RejectedKey))
            .ToListAsync(cancellationToken);

        var successful = payments.FirstOrDefault(x => x.Description == SuccessfulKey);
        var highValue = payments.FirstOrDefault(x => x.Description == HighValueKey);
        var rejected = payments.FirstOrDefault(x => x.Description == RejectedKey);

        return BuildScenarios(successful, highValue, rejected);
    }

    public async Task<DemoSeedResultDto> SeedAsync(ActorContext actor, CancellationToken cancellationToken = default)
    {
        EnsureDevelopment();
        await EnsureActiveOwnerAsync(actor, cancellationToken);

        var owner = await RequireOwnerAsync(cancellationToken);
        var customerA = await RequireUserAsync("customer.a", UserRole.Customer, cancellationToken);
        var approver = await RequireUserAsync("approver.100k", UserRole.DelegatedApprover, cancellationToken);
        var lowApprover = await RequireUserAsync("approver.low", UserRole.DelegatedApprover, cancellationToken);
        var accountA = await RequireAccountAsync("1000000001", cancellationToken);
        var accountB = await RequireAccountAsync("2000000001", cancellationToken);

        var ownerActor = new ActorContext(owner.Id, owner.Username, owner.Role);
        var customerActor = new ActorContext(customerA.Id, customerA.Username, customerA.Role);
        var approverActor = new ActorContext(approver.Id, approver.Username, approver.Role);
        var lowApproverActor = new ActorContext(lowApprover.Id, lowApprover.Username, lowApprover.Role);

        await EnsureDelegationAsync(ownerActor, approver.Id, "Transfer", "TRY", 100_000m, null, cancellationToken);
        await EnsureDelegationAsync(ownerActor, approver.Id, "Deposit", "TRY", 100_000m, null, cancellationToken);
        await EnsureDelegationAsync(ownerActor, approver.Id, "Withdrawal", "TRY", 100_000m, null, cancellationToken);
        await EnsureDelegationAsync(ownerActor, approver.Id, "AccountOpening", "TRY", 100_000m, null, cancellationToken);
        await EnsureDelegationAsync(ownerActor, approver.Id, "ReconciliationException", null, null, null, cancellationToken);
        await EnsureDelegationAsync(ownerActor, lowApprover.Id, "Transfer", "TRY", 10_000m, null, cancellationToken);

        var created = 0;

        var successful = await EnsurePaymentAsync(
            customerActor, accountA.Id, accountB.Id, 125m, SuccessfulKey, cancellationToken);
        if (successful.Created)
        {
            created++;
            await _payments.ApprovePaymentAsync(successful.Payment!.Id, approverActor, cancellationToken);
        }

        var highValue = await EnsureOperationalPaymentAsync(
            ownerActor, accountA.Id, accountB.Id, 75_000m, HighValueKey, cancellationToken);
        if (highValue.Created) created++;

        var rejected = await EnsurePaymentAsync(
            customerActor, accountA.Id, accountB.Id, 60_000m, RejectedKey, cancellationToken);
        if (rejected.Created) created++;

        if (rejected.Payment is not null && rejected.Payment.Status == PaymentApprovalStatus.Pending)
        {
            await _payments.RejectPaymentAsync(rejected.Payment.Id, approverActor,
                "DEMO: sentetik red senaryosu", cancellationToken);
        }

        _audit.Add("DEMO_SEEDED", "DemoScenario", null, actor.Username, true,
            $"CreatedPayments: {created}, Successful: {successful.Payment?.Id}, " +
            $"HighValue: {highValue.Payment?.Id}, Rejected: {rejected.Payment?.Id}");
        await _context.SaveChangesAsync(cancellationToken);

        var scenarios = await GetScenariosAsync(cancellationToken);
        return new DemoSeedResultDto
        {
            AlreadySeeded = created == 0,
            CreatedPayments = created,
            Scenarios = scenarios
        };
    }

    public async Task<DemoResetResultDto> ResetAsync(ActorContext actor, CancellationToken cancellationToken = default)
    {
        EnsureDevelopment();
        await EnsureActiveOwnerAsync(actor, cancellationToken);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var demoPayments = await _context.PaymentApprovals
            .Where(x => x.IsDemo)
            .ToListAsync(cancellationToken);

        var paymentIds = demoPayments.Select(x => x.Id).ToList();
        var correlations = demoPayments.Select(x => x.CorrelationId).ToList();

        var demoTransactions = await _context.Transactions
            .Where(x => x.CorrelationId != null && correlations.Contains(x.CorrelationId.Value))
            .ToListAsync(cancellationToken);

        var affectedAccounts = demoTransactions.Select(x => x.AccountId).Distinct().ToList();
        var accounts = await _context.Accounts.Where(x => affectedAccounts.Contains(x.Id)).ToListAsync(cancellationToken);

        foreach (var tx in demoTransactions)
        {
            var account = accounts.FirstOrDefault(x => x.Id == tx.AccountId);
            if (account is null) continue;

            // Reverse only the demo transfer's ledger effect; user UAT rows are untouched.
            if (tx.Type is TransactionType.TransferOut or TransactionType.Withdraw)
                account.Balance += tx.Amount;
            else
                account.Balance -= tx.Amount;
        }

        if (accounts.Any(x => x.Balance < 0))
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new BusinessRuleException(
                "Demo reset bakiye ters çevriminde negatif bakiye üretti; işlem yapılmadı. Manuel inceleme gerekir.");
        }

        var timeline = await _context.PaymentTimelineEvents
            .Where(x => paymentIds.Contains(x.PaymentApprovalId))
            .ToListAsync(cancellationToken);

        var batches = await _context.ReconciliationBatches
            .Where(x => x.IsDemo)
            .ToListAsync(cancellationToken);

        _context.PaymentTimelineEvents.RemoveRange(timeline);
        _context.Transactions.RemoveRange(demoTransactions);
        _context.PaymentApprovals.RemoveRange(demoPayments);
        _context.ReconciliationBatches.RemoveRange(batches);

        _audit.Add("DEMO_RESET", "DemoScenario", null, actor.Username, true,
            $"RemovedPayments: {demoPayments.Count}, RemovedTransactions: {demoTransactions.Count}, " +
            $"RemovedTimelineEvents: {timeline.Count}, RemovedBatches: {batches.Count}. " +
            "Users/customers/accounts and non-demo UAT rows were preserved.");

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.Information("Demo reset by {Actor}. Payments: {Payments}, Batches: {Batches}",
            actor.Username, demoPayments.Count, batches.Count);

        return new DemoResetResultDto
        {
            RemovedPayments = demoPayments.Count,
            RemovedTransactions = demoTransactions.Count,
            RemovedTimelineEvents = timeline.Count,
            RemovedBatches = batches.Count,
            Message = "Yalnız IsDemo işaretli senaryo verisi kaldırıldı; kullanıcı/hesap ve UAT verisi korundu."
        };
    }

    public async Task<string> BuildReconciliationSampleAsync(string kind, CancellationToken cancellationToken = default)
    {
        EnsureDevelopment();

        var successful = await _context.PaymentApprovals.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsDemo && x.Description == SuccessfulKey, cancellationToken);

        var builder = new StringBuilder();
        builder.Append("external_reference,internal_reference,amount,currency,transaction_date,direction\n");

        if (successful is not null)
        {
            var transactions = await _context.Transactions.AsNoTracking()
                .Where(x => x.CorrelationId == successful.CorrelationId)
                .OrderBy(x => x.Type)
                .ToListAsync(cancellationToken);

            var mismatched = kind.Equals("mismatch", StringComparison.OrdinalIgnoreCase);
            foreach (var tx in transactions)
            {
                var direction = tx.Type is TransactionType.TransferOut or TransactionType.Withdraw ? "Debit" : "Credit";
                var amount = mismatched ? tx.Amount + 1m : tx.Amount;
                var external = mismatched
                    ? $"DEMO-MISMATCH-{tx.Id:N}"
                    : $"DEMO-MATCH-{tx.Id:N}";
                builder.Append(string.Join(',',
                    external,
                    successful.CorrelationId.ToString("D"),
                    amount.ToString("F2", CultureInfo.InvariantCulture),
                    tx.Amount == amount ? successful.Currency : successful.Currency,
                    successful.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    direction));
                builder.Append('\n');
            }
        }

        if (kind.Equals("mismatch", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append("DEMO-DUP,11111111-1111-1111-1111-111111111111,10.00,TRY," +
                           DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ",Debit\n");
            builder.Append("DEMO-DUP,22222222-2222-2222-2222-222222222222,10.00,TRY," +
                           DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ",Debit\n");
            builder.Append("DEMO-ONLY,33333333-3333-3333-3333-333333333333,5.00,TRY," +
                           DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ",Debit\n");
        }

        return builder.ToString();
    }

    private async Task<(PaymentApproval? Payment, bool Created)> EnsureOperationalPaymentAsync(
        ActorContext actor, Guid fromAccountId, Guid toAccountId, decimal amount, string description,
        CancellationToken cancellationToken)
    {
        var existing = await _context.PaymentApprovals
            .FirstOrDefaultAsync(x => x.IsDemo && x.Description == description, cancellationToken);
        if (existing is not null) return (existing, false);

        var payment = await _payments.CreateOperationalRequestAsync(new DigitalBanking.Application.DTOs.ApprovalRequests.CreateOperationalRequest
        {
            RequestType = "Transfer",
            AccountId = fromAccountId,
            ToAccountId = toAccountId,
            Amount = amount,
            Description = description
        }, actor, cancellationToken);
        var entity = await _context.PaymentApprovals.FirstAsync(x => x.Id == payment.Id, cancellationToken);
        entity.IsDemo = true;
        await _context.SaveChangesAsync(cancellationToken);
        return (entity, true);
    }

    private async Task<(PaymentApproval? Payment, bool Created)> EnsurePaymentAsync(
        ActorContext actor, Guid fromAccountId, Guid toAccountId, decimal amount, string description,
        CancellationToken cancellationToken)
    {
        var existing = await _context.PaymentApprovals
            .FirstOrDefaultAsync(x => x.IsDemo && x.Description == description, cancellationToken);
        if (existing is not null) return (existing, false);

        var payment = await _payments.CreateCustomerTransferRequestAsync(
            fromAccountId, toAccountId, amount, description, actor, cancellationToken);
        var entity = await _context.PaymentApprovals.FirstAsync(x => x.Id == payment.Id, cancellationToken);
        entity.IsDemo = true;
        await _context.SaveChangesAsync(cancellationToken);
        return (entity, true);
    }

    private async Task<User> RequireUserAsync(string username, UserRole role, CancellationToken cancellationToken)
    {
        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Username == username && x.Role == role, cancellationToken);
        return user ?? throw new BusinessRuleException($"Demo kullanıcısı bulunamadı: {username}. Uygulamayı yeniden başlatın.");
    }

    private async Task<Account> RequireAccountAsync(string accountNumber, CancellationToken cancellationToken)
    {
        var account = await _context.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AccountNumber == accountNumber, cancellationToken);
        return account ?? throw new BusinessRuleException($"Demo hesabı bulunamadı: {accountNumber}. Uygulamayı yeniden başlatın.");
    }

    private static List<DemoScenarioDto> BuildScenarios(
        PaymentApproval? successful, PaymentApproval? highValue, PaymentApproval? rejected) => new()
    {
        new DemoScenarioDto
        {
            Key = "successful-transfer",
            Title = "Başarılı transfer (otomatik onay)",
            Persona = "customer.a → customer.b",
            Description = "Düşük riskli TRY transferi tamamlanır; bakiye hareketi ve timeline gerçek kayıtlardan görünür.",
            StartState = successful is null ? "Seed bekleniyor" : $"PAY-{successful.Id:D6} tamamlandı",
            Steps = new()
            {
                "customer.a ile giriş yapın (şifre .local/credentials.json).",
                "Hesaplarım > transfer talebi: Demo A TRY → Demo B TRY, 125,00 TRY.",
                "Özet ekranını onaylayın ve makbuzu açın."
            },
            ExpectedResult = "AutoApproved; bakiye bir kez hareket eder; timeline Created → AutoApproved → Completed; dekontta DEMO ibaresi vardır.",
            Evidence = successful is null
                ? new List<string> { "/api/demo/seed" }
                : new List<string> { $"/api/me/payments/{successful.Id}", $"/api/me/payments/{successful.Id}/timeline", $"/api/me/payments/{successful.Id}/receipt" },
            RelatedPaymentId = successful?.Id
        },
        new DemoScenarioDto
        {
            Key = "insufficient-balance",
            Title = "Yetersiz bakiye",
            Persona = "customer.a",
            Description = "Bakiye üstü transfer talebi iş kuralıyla reddedilir; sistem arızası gibi sunulmaz.",
            StartState = "Demo A TRY hesabı sınırlı bakiyeli",
            Steps = new()
            {
                "customer.a ile giriş yapın.",
                "Demo A TRY hesabından 1.000.000,00 TRY transfer talebi oluşturun.",
                "Sonucu ve bakiyenin değişmediğini kontrol edin."
            },
            ExpectedResult = "400 Insufficient balance; bakiye ve ledger değişmez; kullanıcıya anlaşılır hata gösterilir.",
            Evidence = new List<string> { "/api/me/transfers" }
        },
        new DemoScenarioDto
        {
            Key = "high-value-maker-checker",
            Title = "Yüksek tutarlı maker-checker",
            Persona = "Owner → approver.100k",
            Description = "75.000 TRY talep Pending kalır; farklı Admin onaylar.",
            StartState = highValue is null ? "Seed bekleniyor" : $"PAY-{highValue.Id:D6} / {highValue.Status}",
            Steps = new()
            {
                "Owner ile talebi oluşturun veya seed'i çalıştırın.",
                "approver.100k (DelegatedApprover) ile onay kuyruğunu açın.",
                "Talebi inceleyip onaylayın; iki hesabın bakiyesini kontrol edin."
            },
            ExpectedResult = "Talep Pending → Approved; bakiye bir kez hareket eder; timeline ve audit kaydı oluşur.",
            Evidence = highValue is null
                ? new List<string> { "/api/paymentapprovals/pending" }
                : new List<string> { $"/api/paymentapprovals/{highValue.Id}", $"/api/paymentapprovals/{highValue.Id}/timeline" },
            RelatedPaymentId = highValue?.Id
        },
        new DemoScenarioDto
        {
            Key = "self-approval-blocked",
            Title = "Kendi talebini onaylama reddi",
            Persona = "Owner",
            Description = "Maker kendi talebini onaylayamaz.",
            StartState = highValue is null ? "Seed bekleniyor" : $"PAY-{highValue.Id:D6} Pending",
            Steps = new()
            {
                "Maker'ın oluşturduğu Pending talebi açın.",
                "Aynı kullanıcı ile onaylamayı deneyin."
            },
            ExpectedResult = "400 Users cannot review their own payment requests; bakiye değişmez.",
            Evidence = highValue is null
                ? new List<string> { "/api/paymentapprovals/pending" }
                : new List<string> { $"/api/paymentapprovals/{highValue.Id}" },
            RelatedPaymentId = highValue?.Id
        },
        new DemoScenarioDto
        {
            Key = "duplicate-transfer",
            Title = "Çift tıklama / tekrar istek",
            Persona = "customer.a",
            Description = "Aynı Idempotency-Key ile aynı transfer iki kez gönderilir; para ikizlenmez.",
            StartState = "Tamamlanmış başarılı transfer",
            Steps = new()
            {
                "Transfer formunu gönderin; tarayıcı aynı Idempotency-Key'i yeniden kullanır.",
                "İkinci yanıtta Idempotency-Replayed başlığını ve tek bakiye hareketini kontrol edin.",
                "Farklı tutarla aynı anahtarı deneyin."
            },
            ExpectedResult = "Aynı payload replayed; farklı payload 409; tek ledger hareketi.",
            Evidence = successful is null
                ? new List<string> { "/api/me/transfers" }
                : new List<string> { $"/api/me/payments/{successful.Id}", $"/api/me/payments/{successful.Id}/timeline" },
            RelatedPaymentId = successful?.Id
        },
        new DemoScenarioDto
        {
            Key = "rejected-payment",
            Title = "Ödeme reddi",
            Persona = "approver.100k",
            Description = "Orta riskli talep gerekçeyle reddedilir; bakiye değişmez.",
            StartState = rejected is null ? "Seed bekleniyor" : $"PAY-{rejected.Id:D6} / {rejected.Status}",
            Steps = new()
            {
                "approver.100k (DelegatedApprover) ile onay kuyruğunu açın.",
                "Red gerekçesini yazıp talebi reddedin.",
                "Müşteri timeline'ında Rejected olayını kontrol edin."
            },
            ExpectedResult = "Rejected; bakiye değişmez; red gerekçesi ve audit kaydı görünür.",
            Evidence = rejected is null
                ? new List<string> { "/api/paymentapprovals/pending" }
                : new List<string> { $"/api/paymentapprovals/{rejected.Id}", $"/api/paymentapprovals/{rejected.Id}/timeline" },
            RelatedPaymentId = rejected?.Id
        },
        new DemoScenarioDto
        {
            Key = "reconciliation-matched",
            Title = "Mutabakat — eşleşen dosya",
            Persona = "Owner",
            Description = "Dış dosya, seed edilmiş tamamlanmış transferin debit/credit satırlarıyla eşleşir.",
            StartState = "Başarılı demo transferi",
            Steps = new()
            {
                "İndirilebilir matched CSV'yi indirin.",
                "Mutabakat > yükle: para birimi TRY, yön boş bırakın.",
                "Önizleyin ve içe aktarın."
            },
            ExpectedResult = "Tüm satırlar Matched; yalnız dış/sistem farkı yok; batch kalıcı.",
            Evidence = new List<string> { "/api/reconciliation/sample?kind=matched" },
            SampleDownloadUrl = "/api/reconciliation/sample?kind=matched"
        },
        new DemoScenarioDto
        {
            Key = "reconciliation-mismatch",
            Title = "Mutabakat — uyuşmayan dosya",
            Persona = "Owner",
            Description = "Tutar/para birimi/mükerrer/yalnız dış satırlar sınıflandırılır.",
            StartState = "Seed edilmiş transferler",
            Steps = new()
            {
                "İndirilebilir mismatch CSV'yi indirin.",
                "Önizleyip içe aktarın.",
                "Satır durumlarını ve istisna açma akışını kontrol edin."
            },
            ExpectedResult = "AmountMismatch/CurrencyMismatch/DuplicateReference/OnlyExternal/OnlySystem görünür; pending onaylar only-system sayılmaz.",
            Evidence = new List<string> { "/api/reconciliation/sample?kind=mismatch" },
            SampleDownloadUrl = "/api/reconciliation/sample?kind=mismatch"
        }
    };

    private async Task EnsureDelegationAsync(
        ActorContext owner, Guid userId, string operation, string? currency, decimal? maxAmount,
        decimal? dailyLimit, CancellationToken cancellationToken)
    {
        var existing = await _delegations.GetAllAsync(userId, cancellationToken);
        if (existing.Any(x => x.IsActive && x.Operation == operation
                              && (currency is null || x.Currency == currency)
                              && (maxAmount is null || (x.MaxAmount ?? 0) >= maxAmount)))
            return;

        try
        {
            await _delegations.CreateAsync(new DigitalBanking.Application.DTOs.Delegations.CreateDelegationRequest
            {
                UserId = userId,
                Operation = operation,
                Currency = currency,
                MaxAmount = maxAmount,
                DailyLimit = dailyLimit
            }, owner, cancellationToken);
        }
        catch (DigitalBanking.Application.Exceptions.ConflictException)
        {
            // Another seed/run created it concurrently.
        }
    }

    private async Task EnsureActiveOwnerAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        var active = await _authorization.RequireActiveActorAsync(actor.UserId, cancellationToken);
        if (active.Role != UserRole.Owner)
            throw new ForbiddenException("Demo merkezi yalnız aktif Owner tarafından kullanılabilir.");
    }

    private async Task<User> RequireOwnerAsync(CancellationToken cancellationToken)
    {
        var owner = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Role == UserRole.Owner && x.Status == UserStatus.Active, cancellationToken);
        return owner ?? throw new BusinessRuleException(
            "Owner henüz kurulmadı. Önce /Setup üzerinden Owner oluşturun; demo seed Owner gerektirir.");
    }

    private void EnsureDevelopment()
    {
        if (!_environment.IsDevelopment())
            throw new ForbiddenException("Demo center yalnız Development ortamında kullanılabilir.");
    }
}
