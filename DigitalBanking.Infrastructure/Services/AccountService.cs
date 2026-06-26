using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Account;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DigitalBanking.Infrastructure.Services;

/// <summary>
/// Account service implementation.
/// Handles account creation, retrieval, and management.
/// </summary>
public class AccountService : IAccountService
{
    private readonly BankDbContext _db;
    private readonly ILogger _logger;

    public AccountService(BankDbContext db)
    {
        _db = db;
        _logger = Log.ForContext<AccountService>();
    }

    /// <summary>
    /// Opens a new account for a customer.
    /// Generates a unique account number.
    /// </summary>
    public async Task<AccountDto> OpenAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        _logger.Information("Opening new account for customer {CustomerId}", request.CustomerId);

        // Verify customer exists
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);

        if (customer is null)
        {
            _logger.Warning("Customer not found: {CustomerId}", request.CustomerId);
            throw new NotFoundException("Customer", request.CustomerId);
        }

        var accountNumber = GenerateUniqueAccountNumber();
        var account = new Domain.Entities.Account
        {
            Id = Guid.NewGuid(),
            AccountNumber = accountNumber,
            CustomerId = request.CustomerId,
            Balance = 0,
            CreatedDate = DateTime.UtcNow
        };

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.Information("Account opened successfully. AccountId: {AccountId}, AccountNumber: {AccountNumber}, CustomerId: {CustomerId}",
            account.Id, account.AccountNumber, request.CustomerId);

        return MapToDto(account);
    }

    /// <summary>
    /// Retrieves all accounts with pagination.
    /// </summary>
    public async Task<(List<AccountDto> Accounts, int TotalCount)> GetAllAccountsAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Accounts.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);

        var accounts = await query
            .OrderBy(a => a.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = accounts.Select(MapToDto).ToList();

        _logger.Information("Retrieved {Count} accounts from page {Page}", dtos.Count, page);

        return (dtos, totalCount);
    }

    /// <summary>
    /// Retrieves account details with recent transactions.
    /// </summary>
    public async Task<AccountDetailDto> GetAccountDetailAsync(Guid accountId, int recentTransactionCount = 10, CancellationToken cancellationToken = default)
    {
        var account = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        if (account is null)
        {
            _logger.Warning("Account not found: {AccountId}", accountId);
            throw new NotFoundException("Account", accountId);
        }

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
            Balance = account.Balance,
            CustomerId = account.CustomerId,
            CreatedDate = account.CreatedDate,
            CustomerName = customer != null ? $"{customer.FirstName} {customer.LastName}" : null,
            RecentTransactions = transactions
        };
    }

    /// <summary>
    /// Retrieves basic account information by ID.
    /// </summary>
    public async Task<AccountDto> GetAccountByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        if (account is null)
        {
            _logger.Warning("Account not found: {AccountId}", accountId);
            throw new NotFoundException("Account", accountId);
        }

        return MapToDto(account);
    }

    /// <summary>
    /// Retrieves all accounts for a specific customer.
    /// </summary>
    public async Task<List<AccountDto>> GetAccountsByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        // Verify customer exists
        var customerExists = await _db.Customers
            .AnyAsync(c => c.Id == customerId, cancellationToken);

        if (!customerExists)
        {
            _logger.Warning("Customer not found: {CustomerId}", customerId);
            throw new NotFoundException("Customer", customerId);
        }

        var accounts = await _db.Accounts
            .AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .OrderBy(a => a.CreatedDate)
            .ToListAsync(cancellationToken);

        _logger.Information("Retrieved {Count} accounts for customer {CustomerId}", accounts.Count, customerId);

        return accounts.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Maps domain Account entity to AccountDto.
    /// </summary>
    private static AccountDto MapToDto(Domain.Entities.Account account)
    {
        return new AccountDto
        {
            Id = account.Id,
            AccountNumber = account.AccountNumber,
            Balance = account.Balance,
            CustomerId = account.CustomerId,
            CreatedDate = account.CreatedDate
        };
    }

    /// <summary>
    /// Generates a unique account number.
    /// Format: ACC + timestamp + random suffix (e.g., ACC20250225123456789ABC)
    /// </summary>
    private string GenerateUniqueAccountNumber()
    {
        var timestamp = DateTime.UtcNow.Ticks.ToString("D19");
        var random = new Random().Next(10000, 99999).ToString();
        return $"ACC{timestamp.Substring(timestamp.Length - 10)}{random}";
    }
}
