using DigitalBanking.Application.DTOs.Auth;
using FluentValidation;

namespace DigitalBanking.Application.Validators.Auth;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Kullanıcı adı zorunludur.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Şifre zorunludur.");
    }
}