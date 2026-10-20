using DigitalBanking.Application.Exceptions;

namespace DigitalBanking.Application.Common;

/// <summary>
/// Password policy for newly created local users. Existing demo hashes and legacy
/// SHA-256 records remain verifiable/rehashable; this only gates new passwords.
/// </summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 8;
    public const int MaximumLength = 128;

    public static IReadOnlyList<string> Validate(string? password)
    {
        var errors = new List<string>();
        var value = password ?? string.Empty;

        if (value.Length < MinimumLength)
            errors.Add($"Password must be at least {MinimumLength} characters.");
        if (value.Length > MaximumLength)
            errors.Add($"Password cannot exceed {MaximumLength} characters.");
        if (!value.Any(char.IsLetter))
            errors.Add("Password must contain at least one letter.");
        if (!value.Any(char.IsDigit))
            errors.Add("Password must contain at least one digit.");

        return errors;
    }

    public static void EnsureValid(string? password)
    {
        var errors = Validate(password);
        if (errors.Count > 0)
            throw new BusinessRuleException(string.Join(" ", errors));
    }
}
