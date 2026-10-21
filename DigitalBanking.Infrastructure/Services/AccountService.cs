using System.Security.Cryptography;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Account;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DigitalBanking.Infrastructure.Services;

/// <summary>
/// Account service implementation. Currency and opening balance are honored and
/// recorded as an auditable opening deposit; amounts stay decimal throughout.
/// </summary>
public class AccountService : IAccountService
{
    private readonly BankDbContext _db;
    private readonly IAuditService _auditService;
    private readonly ILogger _logger;

    public AccountService(BankDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
        _logger = Log.ForContext<AccountService>();
    }

    public async Task<AccountDto> OpenAccountAsync(CreateAccountRequest request, ActorContext actor, CancellationToken cancellationToken = default)
    {
        if (!actor.IsAdmin)
            throw new BusinessRuleException("Hesabı yalnız Owner açabilir.");

        var currency = CurrencyCodes.Normalize(request.Currency);
        Money.EnsureNonNegative(request.InitialBalance, "Initial balance");

        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);

        if (customer is null)
            throw new NotFoundException("Customer", request.CustomerId);

        await using var dbTransaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var account = new Account
            {
                Id = Guid.NewGuid(),
                AccountNumber = await GenerateUniqueAccountNumberAsync(cancellationToken),
                CustomerId = request.CustomerId,
                Currency = currency,
                Balance = request.InitialBalance,
                CreatedDate = DateTime.UtcNow
            };

            _db.Accounts.Add(account);

            if (request.InitialBalance > 0)
            {
                _db.Transactions.Add(new Transaction
                {
                    Id = Guid.NewGuid(),
                    AccountId = account.Id,
                    Type = TransactionType.Deposit,
                    Amount = request.InitialBalance,
                    BalanceAfter = request.InitialBalance,
                    Description = "Opening deposit",
                    CreatedDate = DateTime.UtcNow
                });
            }

            _auditService.Add(
                action: "ACCOUNT_OPENED",
                entityName: "Account",
                entityId: account.Id.ToString(),
                username: actor.Username,
                isSuccess: true,
                details: $"Account opened. Currency: {account.Currency}, InitialBalance: {account.Balance}");

            await _db.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);

            _logger.Information(
                "Account opened. AccountId: {AccountId}, CustomerId: {CustomerId}, Currency: {Currency}, OpeningBalance: {Balance}",
                account.Id, account.CustomerId, account.Currency, account.Balance);

            return MapToDto(account);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw new ConcurrencyException("Account", request.CustomerId);
        }
        catch
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<(List<AccountDto> Accounts, int TotalCount)> GetAllAccountsAsync(
        int page = 1,
        int pageSize = 10,
        Guid? customerId = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Accounts.AsNoTracking();
        if (customerId is { } id)
            query = query.Where(a => a.CustomerId == id);

        var totalCount = await query.CountAsync(cancellationToken);

        var accounts = await query
            .OrderByDescending(a => a.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (accounts.Select(MapToDto).ToList(), totalCount);
    }

    public async Task<AccountDetailDto> GetAccountDetailAsync(Guid accountId, int recentTransactionCount = 10, CancellationToken cancellationToken = default)
    {
        recentTransactionCount = Math.Clamp(recentTransactionCount, 1, 100);

        var account = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        if (account is null)
            throw new NotFoundException("Account", accountId);

        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == account.CustomerId, cancellationToken);

        var transactions = await _db.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.CreatedDate)
            .Take(recentTransactionCount)
            .Select(t => new AccountDetailDto.TransactionSummary
            {
                Id = t.Id,
                Type = t.Type.ToString(),
                Amount = t.Amount,
                BalanceAfter = t.BalanceAfter,
                Description = t.Description,
                CreatedDate = t.CreatedDate
            })
            .ToListAsync(cancellationToken);

        return new AccountDetailDto
        {
            Id = account.Id,
            AccountNumber = account.AccountNumber,
            AccountName = account.AccountName,
            AccountType = account.AccountType.ToString(),
            Balance = account.Balance,
            Currency = account.Currency,
            CustomerId = account.CustomerId,
            CreatedDate = account.CreatedDate,
            CustomerName = customer is null ? null : $"{customer.FirstName} {customer.LastName}",
            RecentTransactions = transactions
        };
    }

    public async Task<AccountDto> GetAccountByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        if (account is null)
            throw new NotFoundException("Account", accountId);

        return MapToDto(account);
    }

    public async Task<List<AccountDto>> GetAccountsByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customerExists = await _db.Customers
            .AnyAsync(c => c.Id == customerId, cancellationToken);

        if (!customerExists)
            throw new NotFoundException("Customer", customerId);

        var accounts = await _db.Accounts
            .AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.CreatedDate)
            .ToListAsync(cancellationToken);

        return accounts.Select(MapToDto).ToList();
    }

    private static AccountDto MapToDto(Account account) => new()
    {
        Id = account.Id,
        AccountNumber = account.AccountNumber,
        AccountName = account.AccountName,
        AccountType = account.AccountType.ToString(),
        Balance = account.Balance,
        Currency = account.Currency,
        CustomerId = account.CustomerId,
        CreatedDate = account.CreatedDate
    };

    private async Task<string> GenerateUniqueAccountNumberAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var suffix = RandomNumberGenerator.GetInt32(100000, 999999);
            var candidate = $"ACC{DateTime.UtcNow:yyyyMMddHHmmss}{suffix}";
            if (!await _db.Accounts.AnyAsync(a => a.AccountNumber == candidate, cancellationToken))
                return candidate;
        }

        throw new BusinessRuleException("Benzersiz hesap numarası üretilemedi. Lütfen tekrar deneyin.");
    }
}
