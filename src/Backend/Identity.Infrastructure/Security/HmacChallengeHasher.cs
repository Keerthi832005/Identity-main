using System.Security.Cryptography;
using Identity.Application.Security;

namespace Identity.Infrastructure.Security;

internal sealed class HmacChallengeHasher : IChallengeHasher
{
    private readonly byte[] key;

    public HmacChallengeHasher(SecurityProtectionOptions options)
    {
        ValidateKey(options.ChallengeKey, nameof(options.ChallengeKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.KeyId);
        KeyId = options.KeyId;
        key = [.. options.ChallengeKey];
    }

    public string KeyId { get; }

    public byte[] Hash(Guid challengeId) => HMACSHA256.HashData(key, challengeId.ToByteArray());

    public bool Verify(Guid challengeId, byte[] expectedHash, string keyId) =>
        string.Equals(KeyId, keyId, StringComparison.Ordinal)
        && CryptographicOperations.FixedTimeEquals(Hash(challengeId), expectedHash);

    private static void ValidateKey(byte[] value, string name)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (value.Length != 32)
        {
            throw new ArgumentException("Security keys must contain 32 bytes.", name);
        }
    }
}
