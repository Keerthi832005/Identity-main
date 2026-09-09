using System.Security.Cryptography;
using System.Text;
using Identity.Application.Security;

namespace Identity.Infrastructure.Security;

internal sealed class HmacIdentifierHasher : IIdentifierHasher
{
    private readonly byte[] key;

    public HmacIdentifierHasher(SecurityProtectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.IdentifierHashKey);
        if (options.IdentifierHashKey.Length != 32)
        {
            throw new ArgumentException("Identifier hash key must contain 32 bytes.", nameof(options));
        }

        key = [.. options.IdentifierHashKey];
    }

    public byte[] Hash(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        var normalized = identifier.Trim().ToUpperInvariant();
        return HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(normalized));
    }
}
