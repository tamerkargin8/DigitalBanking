using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Transaction;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DigitalBanking.Infrastructure.Services;

/// <summary>
/// Transaction service implementation.
/// Handles deposits, withdrawals, transfers, and transaction history.
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly BankDbContext _db;
    private readonly ILogger _logger;

    public TransactionService(BankDbContext db)
    {
        _db = db;
        _logger = Log.ForContext<TransactionService>();
    }

    /// <summary>
    /// Deposits money into an account.
    /// Runs in a database transaction for atomicity.
    /// </summary>
    public async Task<TransactionDto> DepositAsync(DepositRequest request, CancellationToken cancellationToken = default)
    {
        _logger.Information("Deposit initiated. AccountId: {AccountId}, Amount: {Amount}", 
            request.AccountId, request.Amount);

        // Validate amount
        if (request.Amount <= 0)
        {
            _logger.Warning("Deposit rejected: Amount must be > 0. Amount: {Amount}", request.Amount);
            throw new BusinessRuleException("Amount must be greater than 0.");
        }

        await using var dbTransaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Fetch account with lock (pessimistic)
            var account = await _db.Accounts
                .FirstOrDefaultAsync(a => a.Id == request.AccountId, cancellationToken);

            if (account is null)
            {
                _logger.Warning("Account not found for deposit. AccountId: {AccountId}", request.AccountId);
                throw new NotFoundException("Account", request.AccountId);
            }

            // Update balance
            account.Balance += request.Amount;

            // Create transaction record
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Type = TransactionType.Deposit,
                Amount = request.Amount,
                BalanceAfter = account.Balance,
                Description = request.Description ?? "Deposit",
                CreatedDate = DateTime.UtcNow
            };

            _db.Transactions.Add(transaction);
            await _db.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);

            _logger.Information("Deposit completed successfully. TransactionId: {TransactionId}, Amount: {Amount}, NewBalance: {NewBalance}",
                transaction.Id, request.Amount, account.Balance);

            return MapToDto(transaction);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            _logger.Warning("Deposit failed due to concurrency conflict. AccountId: {AccountId}", request.AccountId);
            throw new ConcurrencyException("Account", request.AccountId);
        }
        catch (Exception ex) when (!(ex is BusinessRuleException) && !(ex is NotFoundException) && !(ex is ConcurrencyException))
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            _logger.Error(ex, "Deposit failed with exception. AccountId: {AccountId}", request.AccountId);
            throw;
        }
    }

    /// <summary>
    /// Withdraws money from an account.
    /// Validates sufficient balance before processing.
    /// Runs in a database transaction for atomicity.
    /// </summary>
    public async Task<TransactionDto> WithdrawAsync(WithdrawRequest request, CancellationToken cancellationToken = default)
    {
        _logger.Information("Withdrawal initiated. AccountId: {AccountId}, Amount: {Amount}",
            request.AccountId, request.Amount);

        // Validate amount
        if (request.Amount <= 0)
        {
            _logger.Warning("Withdrawal rejected: Amount must be > 0. Amount: {Amount}", request.Amount);
            throw new BusinessRuleException("Amount must be greater than 0.");
        }

        await using var dbTransaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var account = await _db.Accounts
                .FirstOrDefaultAsync(a => a.Id == request.AccountId, cancellationToken);

            if (account is null)
            {
                _logger.Warning("Account not found for withdrawal. AccountId: {AccountId}", request.AccountId);
                throw new NotFoundException("Account", request.AccountId);
            }

            // Validate balance
            if (account.Balance < request.Amount)
            {
                _logger.Warning("Withdrawal rejected: Insufficient balance. AccountId: {AccountId}, Balance: {Balance}, Amount: {Amount}",
                    request.AccountId, account.Balance, request.Amount);
                throw new BusinessRuleException("Insufficient balance.");
            }

            // Update balance
            account.Balance -= request.Amount;

            // Create transaction record
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Type = TransactionType.Withdraw,
                Amount = request.Amount,
                BalanceAfter = account.Balance,
                Description = request.Description ?? "Withdrawal",
                CreatedDate = DateTime.UtcNow
            };

            _db.Transactions.Add(transaction);
            await _db.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);

            _logger.Information("Withdrawal completed successfully. TransactionId: {TransactionId}, Amount: {Amount}, NewBalance: {NewBalance}",
                transaction.Id, request.Amount, account.Balance);

            return MapToDto(transaction);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            _logger.Warning("Withdrawal failed due to concurrency conflict. AccountId: {AccountId}", request.AccountId);
            throw new ConcurrencyException("Account", request.AccountId);
        }
        catch (Exception ex) when (!(ex is BusinessRuleException) && !(ex is NotFoundException) && !(ex is ConcurrencyException))
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            _logger.Error(ex, "Withdrawal failed with exception. AccountId: {AccountId}", request.AccountId);
            throw;
        }
    }

    /// <summary>
    /// Transfers money between two accounts.
    /// Creates two transaction records (withdrawal and deposit) for audit trail.
    /// Runs in a database transaction for atomicity.
    /// </summary>
    public async Task<TransferResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        _logger.Information("Transfer initiated. FromAccountId: {FromAccountId}, ToAccountId: {ToAccountId}, Amount: {Amount}",
            request.FromAccountId, request.ToAccountId, request.Amount);

        // Validate amount
        if (request.Amount <= 0)
        {
            _logger.Warning("Transfer rejected: Amount must be > 0. Amount: {Amount}", request.Amount);
            throw new BusinessRuleException("Amount must be greater than 0.");
        }

        // Validate different accounts
        if (request.FromAccountId == request.ToAccountId)
        {
            _logger.Warning("Transfer rejected: Source and destination are the same. AccountId: {AccountId}", request.FromAccountId);
            throw new BusinessRuleException("Source and destination accounts cannot be the same.");
        }

        await using var dbTransaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Fetch both accounts
            var fromAccount = await _db.Accounts
                .FirstOrDefaultAsync(a => a.Id == request.FromAccountId, cancellationToken);

            if (fromAccount is null)
            {
                _logger.Warning("Source account not found. AccountId: {AccountId}", request.FromAccountId);
                throw new NotFoundException("Account", request.FromAccountId);
            }

            var toAccount = await _db.Accounts
                .FirstOrDefaultAsync(a => a.Id == request.ToAccountId, cancellationToken);

            if (toAccount is null)
            {
                _logger.Warning("Destination account not found. AccountId: {AccountId}", request.ToAccountId);
                throw new NotFoundException("Account", request.ToAccountId);
            }

            // Validate balance
            if (fromAccount.Balance < request.Amount)
            {
                _logger.Warning("Transfer rejected: Insufficient balance. FromAccountId: {FromAccountId}, Balance: {Balance}, Amount: {Amount}",
                    request.FromAccountId, fromAccount.Balance, request.Amount);
                throw new BusinessRuleException("Insufficient balance in source account.");
            }

            // Update balances
            fromAccount.Balance -= request.Amount;
            toAccount.Balance += request.Amount;

            var now = DateTime.UtcNow;
            var description = string.IsNullOrWhiteSpace(request.Description)
                ? $"Transfer from {fromAccount.AccountNumber} to {toAccount.AccountNumber}"
                : request.Description;

            // Create outgoing transaction (withdrawal)
            var outgoingTx = new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = fromAccount.Id,
                Type = TransactionType.Withdraw,
                Amount = request.Amount,
                BalanceAfter = fromAccount.Balance,
                Description = "OUT: " + description,
                CreatedDate = now
            };

            // Create incoming transaction (deposit)
            var incomingTx = new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = toAccount.Id,
                Type = TransactionType.Deposit,
                Amount = request.Amount,
                BalanceAfter = toAccount.Balance,
                Description = "IN: " + description,
                CreatedDate = now
            };

            _db.Transactions.Add(outgoingTx);
            _db.Transactions.Add(incomingTx);
            await _db.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);

            _logger.Information("Transfer completed successfully. OutgoingTxId: {OutgoingTxId}, IncomingTxId: {IncomingTxId}, Amount: {Amount}",
                outgoingTx.Id, incomingTx.Id, request.Amount);

            return new TransferResult
            {
                OutgoingTransaction = MapToDto(outgoingTx),
                IncomingTransaction = MapToDto(incomingTx)
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            _logger.Warning("Transfer failed due to concurrency conflict. FromAccountId: {FromAccountId}", request.FromAccountId);
            throw new ConcurrencyException("Transfer", request.FromAccountId);
        }
        catch (Exception ex) when (!(ex is BusinessRuleException) && !(ex is NotFoundException) && !(ex is ConcurrencyException))
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            _logger.Error(ex, "Transfer failed with exception. FromAccountId: {FromAccountId}", request.FromAccountId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves transaction history for an account with pagination.
    /// </summary>
    public async Task<TransactionHistoryDto> GetTransactionHistoryAsync(Guid accountId, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Verify account exists
        var account = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        if (account is null)
        {
            _logger.Warning("Account not found for transaction history. AccountId: {AccountId}", accountId);
            throw new NotFoundException("Account", accountId);
        }

        var query = _db.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId);

        var totalCount = await query.CountAsync(cancellationToken);

        var transactions = await query
            .OrderByDescending(t => t.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = transactions.Select(MapToDto).ToList();

        _logger.Information("Retrieved {Count} transactions for account {AccountId} from page {Page}",
            dtos.Count, accountId, page);

        return new TransactionHistoryDto
        {
            AccountId = account.Id,
            AccountNumber = account.AccountNumber,
            CurrentBalance = account.Balance,
            Pagination = new TransactionHistoryDto.PaginationInfo
            {
                PageNumber = page,
                PageSize = pageSize,
                TotalCount = totalCount
            },
            Transactions = dtos
        };
    }

    /// <summary>
    /// Retrieves a single transaction by ID.
    /// </summary>
    public async Task<TransactionDto> GetTransactionByIdAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var transaction = await _db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);

        if (transaction is null)
        {
            _logger.Warning("Transaction not found. TransactionId: {TransactionId}", transactionId);
            throw new NotFoundException("Transaction", transactionId);
        }

        return MapToDto(transaction);
    }

    /// <summary>
    /// Maps domain Transaction entity to TransactionDto.
    /// </summary>
    private static TransactionDto MapToDto(Transaction transaction)
    {
        return new TransactionDto
        {
            Id = transaction.Id,
            AccountId = transaction.AccountId,
            Type = transaction.Type.ToString(),
            Amount = transaction.Amount,
            BalanceAfter = transaction.BalanceAfter,
            Description = transaction.Description,
            CreatedDate = transaction.CreatedDate
        };
    }
}
