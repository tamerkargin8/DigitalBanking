using DigitalBanking.Application.Common;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;

namespace DigitalBanking.Infrastructure.Services;

/// <summary>
/// Single place where balances and ledger rows are mutated. Callers own the
/// database transaction and the final SaveChanges, so money movement and audit
/// records commit atomically.
/// </summary>
internal static class LedgerOperations
{
    public static Transaction ApplyDeposit(
        BankDbContext db,
        Account account,
        decimal amount,
        string? description,
        DateTime now,
        string defaultDescription)
    {
        Money.EnsurePositive(amount);

        account.Balance += amount;
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            Type = TransactionType.Deposit,
            Amount = amount,
            BalanceAfter = account.Balance,
            Description = string.IsNullOrWhiteSpace(description) ? defaultDescription : description,
            CreatedDate = now
        };
        db.Transactions.Add(transaction);
        return transaction;
    }

    public static Transaction ApplyWithdraw(
        BankDbContext db,
        Account account,
        decimal amount,
        string? description,
        DateTime now,
        string defaultDescription)
    {
        Money.EnsurePositive(amount);

        if (account.Balance < amount)
            throw new BusinessRuleException("Insufficient balance.");

        account.Balance -= amount;
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            Type = TransactionType.Withdraw,
            Amount = amount,
            BalanceAfter = account.Balance,
            Description = string.IsNullOrWhiteSpace(description) ? defaultDescription : description,
            CreatedDate = now
        };
        db.Transactions.Add(transaction);
        return transaction;
    }

    public static (Transaction Debit, Transaction Credit) ApplyTransfer(
        BankDbContext db,
        Account fromAccount,
        Account toAccount,
        decimal amount,
        string? description,
        DateTime now)
    {
        Money.EnsurePositive(amount);

        if (fromAccount.Id == toAccount.Id)
            throw new BusinessRuleException("Source and destination accounts cannot be the same.");

        if (!string.Equals(fromAccount.Currency, toAccount.Currency, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Source and destination accounts must use the same currency.");

        if (fromAccount.Balance < amount)
            throw new BusinessRuleException("Insufficient balance in source account.");

        fromAccount.Balance -= amount;
        toAccount.Balance += amount;

        var resolvedDescription = string.IsNullOrWhiteSpace(description)
            ? $"Transfer from {fromAccount.AccountNumber} to {toAccount.AccountNumber}"
            : description;

        var debit = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = fromAccount.Id,
            Type = TransactionType.TransferOut,
            Amount = amount,
            BalanceAfter = fromAccount.Balance,
            Description = "OUT: " + resolvedDescription,
            CreatedDate = now
        };

        var credit = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = toAccount.Id,
            Type = TransactionType.TransferIn,
            Amount = amount,
            BalanceAfter = toAccount.Balance,
            Description = "IN: " + resolvedDescription,
            CreatedDate = now
        };

        db.Transactions.Add(debit);
        db.Transactions.Add(credit);
        return (debit, credit);
    }
}
