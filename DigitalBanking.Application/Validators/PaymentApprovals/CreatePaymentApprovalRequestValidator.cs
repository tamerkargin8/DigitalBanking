using DigitalBanking.Application.DTOs.PaymentApprovals;
using FluentValidation;

namespace DigitalBanking.Application.Validators.PaymentApprovals;

public class CreatePaymentApprovalRequestValidator
    : AbstractValidator<CreatePaymentApprovalRequest>
{
    public CreatePaymentApprovalRequestValidator()
    {
        RuleFor(x => x.FromAccountId)
            .NotEmpty()
            .WithMessage("Sender account id is required.");

        RuleFor(x => x.ToAccountId)
            .NotEmpty()
            .WithMessage("Receiver account id is required.");

        RuleFor(x => x.ToAccountId)
            .NotEqual(x => x.FromAccountId)
            .WithMessage("Sender and receiver accounts cannot be the same.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.Description)
            .MaximumLength(250)
            .WithMessage("Description cannot exceed 250 characters.");

        RuleFor(x => x.RequestedByUserId)
            .GreaterThan(0)
            .WithMessage("Requested user id is required.");
    }
}