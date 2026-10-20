using System.Security.Cryptography;
using System.Text;
using DigitalBanking.Infrastructure.Security;

namespace DigitalBanking.Tests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void NewHashesAreSaltedAndRejectWrongPasswords()
    {
        var first = _hasher.Hash("synthetic-test-password");
        var second = _hasher.Hash("synthetic-test-password");
        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify("synthetic-test-password", first));
        Assert.False(_hasher.Verify("wrong", first));
        Assert.False(_hasher.NeedsRehash(first));
    }

    [Fact]
    public void LegacyHashesCanBeVerifiedAndRequireUpgrade()
    {
        var legacy = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("legacy-test")));
        Assert.True(_hasher.Verify("legacy-test", legacy));
        Assert.False(_hasher.Verify("wrong", legacy));
        Assert.True(_hasher.NeedsRehash(legacy));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("PBKDF2-SHA256$2147483647$bad$bad")]
    [InlineData("PBKDF2-SHA256$600000$bad$bad")]
    public void MalformedHashesFailClosed(string hash) => Assert.False(_hasher.Verify("test", hash));
}
