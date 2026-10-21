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
            .WithMessage("Gönderen hesap zorunludur.");

        RuleFor(x => x.ToAccountId)
            .NotEmpty()
            .WithMessage("Alıcı hesap zorunludur.");

        RuleFor(x => x.ToAccountId)
            .NotEqual(x => x.FromAccountId)
            .WithMessage("Gönderen ve alıcı hesap aynı olamaz.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Tutar sıfırdan büyük olmalıdır.")
            .Must(amount => decimal.Round(amount, 2) == amount)
            .WithMessage("Tutar en fazla 2 ondalık basamak içerebilir.");

        RuleFor(x => x.Description)
            .MaximumLength(250)
            .WithMessage("Açıklama 250 karakteri aşamaz.");
    }
}
