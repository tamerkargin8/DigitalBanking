namespace DigitalBanking.Domain.Entities;

public class Customer
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty;

    /// <summary>Optional link to the login that owns this customer profile.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Marks deterministic synthetic demo data so scoped reset never touches UAT data.</summary>
    public bool IsDemo { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public List<Account> Accounts { get; set; } = new();
}
