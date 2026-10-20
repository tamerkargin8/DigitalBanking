using System.Globalization;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Transaction;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace DigitalBanking.Infrastructure.Services;

/// <summary>
/// Transaction service implementation. All money values are decimal; every
/// operation runs in one database transaction together with its audit record.
/// </summary>
public class TransactionService : ITransactionService
{
    public static decimal DefaultDirectTransferLimit => PaymentPolicy.AutoApprovalLimit;

    private readonly BankDbContext _db;
    private readonly IAuditService _auditService;
    private readonly decimal _directTransferLimit;
    private readonly ILogger _logger;

    public TransactionService(BankDbContext db, IAuditService auditService, IConfiguration configuration)
    {
        _db = db;
        _auditService = auditService;
        _logger = Log.ForContext<TransactionService>();

        var configured = configuration["Banking:DirectTransferLimit"];
        _directTransferLimit = decimal.TryParse(configured, NumberStyles.Number, CultureInfo.InvariantCulture, out var limit)
            ? limit
            : DefaultDirectTransferLimit;
    }

    public async Task<TransactionDto> DepositAsync(
        DepositRequest request,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(actor, "Deposit");
        Money.EnsurePositive(request.Amount);

        await using var dbTransaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var account = await _db.Accounts
                .FirstOrDefaultAsync(a => a.Id == request.AccountId, cancellationToken);

            if (account is null)
                throw new NotFoundException("Account", request.AccountId);

            var transaction = LedgerOperations.ApplyDeposit(
                _db, account, request.Amount, request.Description, DateTime.UtcNow, "Deposit");

            _auditService.Add(
                action: "DEPOSIT_SUCCESS",
                entityName: "Transaction",
                entityId: transaction.Id.ToString(),
                username: actor.Username,
                isSuccess: true,
                details: $"Deposit completed. AccountId: {account.Id}, Amount: {request.Amount}, BalanceAfter: {account.Balance}");

            await _db.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);

            _logger.Information(
                "Deposit completed. TransactionId: {TransactionId}, AccountId: {AccountId}, Amount: {Amount}, BalanceAfter: {BalanceAfter}",
                transaction.Id, account.Id, request.Amount, account.Balance);

            return MapToDto(transaction);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw new ConcurrencyException("Account", request.AccountId);
        }
        catch
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<TransactionDto> WithdrawAsync(
        WithdrawRequest request,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(actor, "Withdrawal");
        Money.EnsurePositive(request.Amount);

        await using var dbTransaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var account = await _db.Accounts
                .FirstOrDefaultAsync(a => a.Id == request.AccountId, cancellationToken);

            if (account is null)
                throw new NotFoundException("Account", request.AccountId);

            var transaction = LedgerOperations.ApplyWithdraw(
                _db, account, request.Amount, request.Description, DateTime.UtcNow, "Withdrawal");

            _auditService.Add(
                action: "WITHDRAW_SUCCESS",
                entityName: "Transaction",
                entityId: transaction.Id.ToString(),
                username: actor.Username,
                isSuccess: true,
                details: $"Withdrawal completed. AccountId: {account.Id}, Amount: {request.Amount}, BalanceAfter: {account.Balance}");

            await _db.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);

            _logger.Information(
                "Withdrawal completed. TransactionId: {TransactionId}, AccountId: {AccountId}, Amount: {Amount}, BalanceAfter: {BalanceAfter}",
                transaction.Id, account.Id, request.Amount, account.Balance);

            return MapToDto(transaction);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw new ConcurrencyException("Account", request.AccountId);
        }
        catch
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<TransferResult> TransferAsync(
        TransferRequest request,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(actor, "Direct transfer");
        Money.EnsurePositive(request.Amount);

        if (request.Amount >= _directTransferLimit)
            throw new BusinessRuleException(
                $"Direct transfers of {_directTransferLimit:N2} or more must use the payment approval (maker-checker) flow.");

        if (request.FromAccountId == request.ToAccountId)
            throw new BusinessRuleException("Source and destination accounts cannot be the same.");

        await using var dbTransaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var fromAccount = await _db.Accounts
                .FirstOrDefaultAsync(a => a.Id == request.FromAccountId, cancellationToken);
            if (fromAccount is null)
                throw new NotFoundException("Account", request.FromAccountId);

            var toAccount = await _db.Accounts
                .FirstOrDefaultAsync(a => a.Id == request.ToAccountId, cancellationToken);
            if (toAccount is null)
                throw new NotFoundException("Account", request.ToAccountId);

            var (outgoing, incoming) = LedgerOperations.ApplyTransfer(
                _db, fromAccount, toAccount, request.Amount, request.Description, DateTime.UtcNow);

            _auditService.Add(
                action: "TRANSFER_SUCCESS",
                entityName: "Transfer",
                entityId: outgoing.Id.ToString(),
                username: actor.Username,
                isSuccess: true,
                details:
                    $"Transfer completed. FromAccountId: {fromAccount.Id}, ToAccountId: {toAccount.Id}, " +
                    $"Amount: {request.Amount}, Currency: {fromAccount.Currency}, " +
                    $"FromBalanceAfter: {fromAccount.Balance}, ToBalanceAfter: {toAccount.Balance}, " +
                    $"OutgoingTransactionId: {outgoing.Id}, IncomingTransactionId: {incoming.Id}");

            await _db.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);

            _logger.Information(
                "Transfer completed. OutgoingTransactionId: {OutgoingTransactionId}, IncomingTransactionId: {IncomingTransactionId}, Amount: {Amount}",
                outgoing.Id, incoming.Id, request.Amount);

            return new TransferResult
            {
                OutgoingTransaction = MapToDto(outgoing),
                IncomingTransaction = MapToDto(incoming)
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw new ConcurrencyException("Transfer", request.FromAccountId);
        }
        catch
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<TransactionHistoryDto> GetTransactionHistoryAsync(
        Guid accountId,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var account = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        if (account is null)
            throw new NotFoundException("Account", accountId);

        var query = _db.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId);

        var totalCount = await query.CountAsync(cancellationToken);

        var transactions = await query
            .OrderByDescending(t => t.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new TransactionHistoryDto
        {
            AccountId = account.Id,
            AccountNumber = account.AccountNumber,
            Currency = account.Currency,
            CurrentBalance = account.Balance,
            Pagination = new TransactionHistoryDto.PaginationInfo
            {
                PageNumber = page,
                PageSize = pageSize,
                TotalCount = totalCount
            },
            Transactions = transactions.Select(MapToDto).ToList()
        };
    }

    public async Task<TransactionDto> GetTransactionByIdAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var transaction = await _db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);

        if (transaction is null)
            throw new NotFoundException("Transaction", transactionId);

        return MapToDto(transaction);
    }

    public async Task<TransactionStatementResponse> GetStatementAsync(
        Guid accountId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var (normalizedFrom, normalizedTo) = ReportDateRange.Normalize(from, to);

        var account = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        if (account is null)
            throw new NotFoundException("Account", accountId);

        var transactions = await _db.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId
                        && t.CreatedDate >= normalizedFrom
                        && t.CreatedDate <= normalizedTo)
            .OrderBy(t => t.CreatedDate)
            .Select(t => new TransactionDto
            {
                Id = t.Id,
                AccountId = t.AccountId,
                Type = t.Type.ToString(),
                Amount = t.Amount,
                BalanceAfter = t.BalanceAfter,
                Description = t.Description,
                CreatedDate = t.CreatedDate
            })
            .ToListAsync(cancellationToken);

        return new TransactionStatementResponse
        {
            AccountId = accountId,
            AccountNumber = account.AccountNumber,
            Currency = account.Currency,
            From = normalizedFrom,
            To = normalizedTo,
            Count = transactions.Count,
            Transactions = transactions
        };
    }

    public async Task<DailyTransactionSummaryResponse> GetDailySummaryAsync(
        Guid accountId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var (normalizedFrom, normalizedTo) = ReportDateRange.Normalize(from, to);

        var account = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        if (account is null)
            throw new NotFoundException("Account", accountId);

        var summary = await _db.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId
                        && t.CreatedDate >= normalizedFrom
                        && t.CreatedDate <= normalizedTo)
            .GroupBy(t => t.CreatedDate.Date)
            .Select(g => new DailyTransactionSummaryItem
            {
                Date = g.Key,
                DepositTotal = g.Where(x => x.Type == TransactionType.Deposit).Sum(x => x.Amount),
                WithdrawTotal = g.Where(x => x.Type == TransactionType.Withdraw).Sum(x => x.Amount),
                TransferInTotal = g.Where(x => x.Type == TransactionType.TransferIn).Sum(x => x.Amount),
                TransferOutTotal = g.Where(x => x.Type == TransactionType.TransferOut).Sum(x => x.Amount),
                Count = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

        return new DailyTransactionSummaryResponse
        {
            AccountId = accountId,
            AccountNumber = account.AccountNumber,
            Currency = account.Currency,
            From = normalizedFrom,
            To = normalizedTo,
            Days = summary.Count,
            Summary = summary
        };
    }

    public async Task<List<TopAccountTransactionResponse>> GetTopAccountsAsync(
        DateTime from,
        DateTime to,
        int take,
        string? currency,
        CancellationToken cancellationToken = default)
    {
        var (normalizedFrom, normalizedTo) = ReportDateRange.Normalize(from, to);
        var normalizedCurrency = CurrencyCodes.Normalize(currency);
        take = Math.Clamp(take, 1, 50);

        var rows = await _db.Transactions
            .AsNoTracking()
            .Join(
                _db.Accounts.AsNoTracking(),
                transaction => transaction.AccountId,
                account => account.Id,
                (transaction, account) => new { transaction, account })
            .Where(x => x.transaction.CreatedDate >= normalizedFrom
                        && x.transaction.CreatedDate <= normalizedTo
                        && x.account.Currency == normalizedCurrency)
            .GroupBy(x => new { x.account.Id, x.account.AccountNumber, x.account.Currency })
            .Select(g => new TopAccountTransactionResponse
            {
                AccountId = g.Key.Id,
                AccountNumber = g.Key.AccountNumber,
                Currency = g.Key.Currency,
                TransactionCount = g.Count(),
                DepositTotal = g.Where(x => x.transaction.Type == TransactionType.Deposit).Sum(x => x.transaction.Amount),
                WithdrawTotal = g.Where(x => x.transaction.Type == TransactionType.Withdraw).Sum(x => x.transaction.Amount),
                TransferInTotal = g.Where(x => x.transaction.Type == TransactionType.TransferIn).Sum(x => x.transaction.Amount),
                TransferOutTotal = g.Where(x => x.transaction.Type == TransactionType.TransferOut).Sum(x => x.transaction.Amount)
            })
            .OrderByDescending(x => x.TransactionCount)
            .ThenBy(x => x.AccountId)
            .Take(take)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
            row.NetFlow = row.DepositTotal + row.TransferInTotal - row.WithdrawTotal - row.TransferOutTotal;

        return rows;
    }

    private static void EnsureAdmin(ActorContext actor, string operation)
    {
        if (!actor.IsAdmin)
            throw new BusinessRuleException($"{operation} is restricted to the Admin role.");
    }

    private static TransactionDto MapToDto(Transaction transaction) => new()
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
