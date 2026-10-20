using AutoMapper;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Account;
using DigitalBanking.Application.DTOs.CustomerPortal;
using DigitalBanking.Application.DTOs.PaymentApprovals;
using DigitalBanking.Application.DTOs.Transaction;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Infrastructure.Services;

/// <summary>
/// Customer self-service queries. Ownership is always derived from the JWT user id;
/// account/payment ids from the browser are only accepted after an ownership join.
/// </summary>
public sealed class CustomerPortalService : ICustomerPortalService
{
    private readonly BankDbContext _context;
    private readonly ICustomerAccessService _access;
    private readonly IAccountService _accountService;
    private readonly ITransactionService _transactionService;
    private readonly IPaymentApprovalService _paymentApprovalService;
    private readonly IMapper _mapper;

    public CustomerPortalService(
        BankDbContext context,
        ICustomerAccessService access,
        IAccountService accountService,
        ITransactionService transactionService,
        IPaymentApprovalService paymentApprovalService,
        IMapper mapper)
    {
        _context = context;
        _access = access;
        _accountService = accountService;
        _transactionService = transactionService;
        _paymentApprovalService = paymentApprovalService;
        _mapper = mapper;
    }

    public async Task<CustomerProfileDto> GetProfileAsync(ActorContext actor, CancellationToken cancellationToken = default)
    {
        var customer = await GetOwnedCustomerAsync(actor, cancellationToken);
        return new CustomerProfileDto
        {
            CustomerId = customer.Id,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            DisplayName = $"{customer.FirstName} {customer.LastName}",
            MaskedIdentityNumber = Masking.IdentityNumber(customer.IdentityNumber),
            Role = actor.Role.ToString()
        };
    }

    public async Task<List<AccountDto>> GetAccountsAsync(ActorContext actor, CancellationToken cancellationToken = default)
    {
        var customerId = await RequireCustomerIdAsync(actor, cancellationToken);
        var accounts = await _context.Accounts
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderBy(x => x.Currency)
            .ThenBy(x => x.CreatedDate)
            .ToListAsync(cancellationToken);

        return accounts.Select(x => new AccountDto
        {
            Id = x.Id,
            AccountNumber = x.AccountNumber,
            Balance = x.Balance,
            Currency = x.Currency,
            CustomerId = x.CustomerId,
            CreatedDate = x.CreatedDate
        }).ToList();
    }

    public async Task<AccountDetailDto> GetAccountAsync(Guid accountId, ActorContext actor, CancellationToken cancellationToken = default)
    {
        await RequireOwnedAccountAsync(actor, accountId, cancellationToken);
        return await _accountService.GetAccountDetailAsync(accountId, 20, cancellationToken);
    }

    public async Task<TransactionHistoryDto> GetTransactionsAsync(Guid accountId, int page, int pageSize, ActorContext actor, CancellationToken cancellationToken = default)
    {
        await RequireOwnedAccountAsync(actor, accountId, cancellationToken);
        return await _transactionService.GetTransactionHistoryAsync(accountId, page, pageSize, cancellationToken);
    }

    public async Task<List<BeneficiaryDto>> GetBeneficiariesAsync(ActorContext actor, CancellationToken cancellationToken = default)
    {
        var customerId = await RequireCustomerIdAsync(actor, cancellationToken);

        var rows = await _context.Accounts
            .AsNoTracking()
            .Where(x => x.CustomerId != customerId)
            .Join(
                _context.Customers.AsNoTracking(),
                account => account.CustomerId,
                customer => customer.Id,
                (account, customer) => new
                {
                    account.Id,
                    account.AccountNumber,
                    account.Currency,
                    customer.FirstName,
                    customer.LastName
                })
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.Currency)
            .Take(100)
            .ToListAsync(cancellationToken);

        return rows.Select(x => new BeneficiaryDto
        {
            AccountId = x.Id,
            DisplayName = $"{x.FirstName} {x.LastName}",
            MaskedAccountNumber = Masking.AccountNumber(x.AccountNumber),
            Currency = x.Currency
        }).ToList();
    }

    public async Task<List<PaymentApprovalResponse>> GetPaymentsAsync(ActorContext actor, CancellationToken cancellationToken = default)
    {
        var customerId = await RequireCustomerIdAsync(actor, cancellationToken);

        var payments = await _context.PaymentApprovals
            .AsNoTracking()
            .Where(payment => _context.Accounts.Any(account =>
                account.Id == payment.FromAccountId && account.CustomerId == customerId))
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<PaymentApprovalResponse>>(payments);
    }

    public async Task<PaymentApprovalResponse> GetPaymentAsync(int paymentId, ActorContext actor, CancellationToken cancellationToken = default)
    {
        if (!await _access.UserOwnsPaymentAsync(actor.UserId, paymentId, cancellationToken))
            throw new NotFoundException("Payment", paymentId.ToString());

        var payment = await _context.PaymentApprovals
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

        if (payment is null)
            throw new NotFoundException("Payment", paymentId.ToString());

        return _mapper.Map<PaymentApprovalResponse>(payment);
    }

    public async Task<PaymentApprovalResponse> CreateTransferRequestAsync(
        CreateCustomerTransferRequest request,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        await RequireOwnedAccountAsync(actor, request.FromAccountId, cancellationToken);
        return await _paymentApprovalService.CreateCustomerTransferRequestAsync(
            request.FromAccountId,
            request.ToAccountId,
            request.Amount,
            request.Description,
            actor,
            cancellationToken);
    }

    private async Task<Domain.Entities.Customer> GetOwnedCustomerAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        var customerId = await RequireCustomerIdAsync(actor, cancellationToken);
        var customer = await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);

        if (customer is null)
            throw new ForbiddenException("Bu kullanıcıya bağlı bir müşteri profili bulunamadı.");

        return customer;
    }

    private async Task<Guid> RequireCustomerIdAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        var customerId = await _access.GetCustomerIdAsync(actor.UserId, cancellationToken);
        if (customerId is null)
            throw new ForbiddenException("Bu kullanıcıya bağlı bir müşteri profili bulunamadı.");
        return customerId.Value;
    }

    private async Task RequireOwnedAccountAsync(ActorContext actor, Guid accountId, CancellationToken cancellationToken)
    {
        if (!await _access.UserOwnsAccountAsync(actor.UserId, accountId, cancellationToken))
            throw new NotFoundException("Account", accountId);
    }
}
