using System.Security.Cryptography;
using Identity.Application.Authentication;
using Identity.Domain.Entities;

namespace Identity.Infrastructure.Security;

internal sealed class Pbkdf2PinHasher : IPinHasher
{
    private const int IterationCount = 600000;
    private const int SaltLength = 32;
    private const int HashLength = 32;
    private const string Algorithm = "PBKDF2-SHA256";
    private static readonly byte[] DummySalt = new byte[SaltLength];
    private static readonly byte[] DummyHash = new byte[HashLength];

    public PasswordHash Hash(string pin)
    {
        Validate(pin);
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = Derive(pin, salt, IterationCount);
        return new PasswordHash(Algorithm, IterationCount, salt, hash);
    }

    public bool Verify(string pin, UserCredential? credential)
    {
        ArgumentNullException.ThrowIfNull(pin);
        var salt = credential?.Salt ?? DummySalt;
        var iterations = credential?.IterationCount ?? IterationCount;
        var expected = credential?.SecretHash ?? DummyHash;
        var actual = Derive(pin, salt, iterations, expected.Length);
        return credential is not null && CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(string pin, byte[] salt, int iterations, int length = HashLength) =>
        Rfc2898DeriveBytes.Pbkdf2(
            pin, salt, iterations, HashAlgorithmName.SHA256, length);

    private static void Validate(string pin)
    {
        ArgumentNullException.ThrowIfNull(pin);
        if (pin.Length != 4 || pin.Any(character => character is < '0' or > '9'))
            throw new ArgumentException("PIN must contain exactly four digits.", nameof(pin));
    }
}
