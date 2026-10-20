namespace DigitalBanking.Domain.Enums;

/// <summary>
/// 1/2 are legacy values from the previous Admin/User demo. They are intentionally
/// not treated as Owner/Customer; operators must map them explicitly.
/// </summary>
public enum UserRole
{
    None = 0,
    Admin = 1,
    User = 2,
    Owner = 3,
    DelegatedApprover = 4,
    Customer = 5
}
