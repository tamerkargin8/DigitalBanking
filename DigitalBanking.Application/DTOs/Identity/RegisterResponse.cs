namespace DigitalBanking.Application.DTOs.Identity;

public class RegisterResponse
{
    public Guid UserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
