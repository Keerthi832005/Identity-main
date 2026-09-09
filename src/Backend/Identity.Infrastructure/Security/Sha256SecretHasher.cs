using System.Security.Cryptography;
using System.Text;
using Identity.Application.Authentication;

namespace Identity.Infrastructure.Security;

internal sealed class Sha256SecretHasher : ISecretHasher
{
    public byte[] Hash(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        return SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    public bool Verify(string secret, byte[] expectedHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentNullException.ThrowIfNull(expectedHash);
        var actual = Hash(secret);
        return CryptographicOperations.FixedTimeEquals(actual, expectedHash);
    }
}
