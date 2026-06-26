using System.Security.Cryptography;
using System.Text;

namespace DigitalBanking.Infrastructure.Security;

public class PasswordHasher
{
    public string Hash(string password)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public bool Verify(string password, string passwordHash)
    {
        var hashOfInput = Hash(password);
        return hashOfInput == passwordHash;
    }
}