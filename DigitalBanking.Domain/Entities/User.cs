using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }

        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public UserRole Role { get; set; }
        public UserStatus Status { get; set; } = UserStatus.PendingActivation;

        public DateTime CreatedDate { get; set; }
        public DateTime? ActivatedAt { get; set; }
        public Guid? ActivatedByUserId { get; set; }
    }
}