using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Infrastructure.Services;

public sealed class CustomerAccessService : ICustomerAccessService
{
    private readonly BankDbContext _context;

    public CustomerAccessService(BankDbContext context)
    {
        _context = context;
    }

    public async Task<Guid?> GetCustomerIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> UserOwnsAccountAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default)
    {
        return _context.Accounts
            .AsNoTracking()
            .Where(x => x.Id == accountId)
            .Join(
                _context.Customers.AsNoTracking().Where(c => c.UserId == userId),
                account => account.CustomerId,
                customer => customer.Id,
                (_, _) => 1)
            .AnyAsync(cancellationToken);
    }

    public Task<bool> UserOwnsPaymentAsync(Guid userId, int paymentId, CancellationToken cancellationToken = default)
    {
        return _context.PaymentApprovals
            .AsNoTracking()
            .Where(x => x.Id == paymentId)
            .Join(
                _context.Accounts.AsNoTracking(),
                payment => payment.FromAccountId,
                account => account.Id,
                (payment, account) => new { payment, account })
            .Join(
                _context.Customers.AsNoTracking().Where(c => c.UserId == userId),
                joined => joined.account.CustomerId,
                customer => customer.Id,
                (_, _) => 1)
            .AnyAsync(cancellationToken);
    }
}
