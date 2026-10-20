using System.Security.Cryptography;
using System.Text;

namespace DigitalBanking.Infrastructure.Security;

public class PasswordHasher
{
    private const int Iterations = 600_000;
    private const string Prefix = "PBKDF2-SHA256";

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        try
        {
            var parts = passwordHash.Split('$');
            if (parts.Length == 4 && parts[0] == Prefix)
            {
                if (!int.TryParse(parts[1], out var iterations) || iterations < 100_000 || iterations > 1_000_000)
                    return false;
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                if (salt.Length != 16 || expected.Length != 32) return false;
                var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }

            // Existing SHA-256 records are upgraded after a successful login.
            var legacy = Convert.FromBase64String(passwordHash);
            return legacy.Length == 32 && CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(password)), legacy);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public bool NeedsRehash(string passwordHash) => !passwordHash.StartsWith($"{Prefix}${Iterations}$", StringComparison.Ordinal);
}
