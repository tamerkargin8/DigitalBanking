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

    public async Task<bool> UserOwnsPaymentAsync(Guid userId, int paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await _context.PaymentApprovals.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken);
        if (payment is null) return false;

        var customerId = await GetCustomerIdAsync(userId, cancellationToken);
        if (customerId is null) return false;

        if (payment.RequestType == Domain.Enums.ApprovalRequestType.AccountOpening)
            return payment.RequestedCustomerId == customerId;

        return await _context.Accounts.AsNoTracking()
            .AnyAsync(x => x.Id == payment.FromAccountId && x.CustomerId == customerId, cancellationToken);
    }
}
