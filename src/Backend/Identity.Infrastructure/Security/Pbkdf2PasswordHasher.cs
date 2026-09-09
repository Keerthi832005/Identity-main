using System.Security.Cryptography;
using Identity.Application.Authentication;
using Identity.Domain.Entities;

namespace Identity.Infrastructure.Security;

internal sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int IterationCount = 600000;
    private const int SaltLength = 32;
    private const int HashLength = 32;
    private const string Algorithm = "PBKDF2-SHA256";
    private static readonly byte[] DummySalt = new byte[SaltLength];
    private static readonly byte[] DummyHash = new byte[HashLength];

    public PasswordHash Hash(string password)
    {
        ValidatePassword(password);
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = Derive(password, salt, IterationCount);
        return new PasswordHash(Algorithm, IterationCount, salt, hash);
    }

    public bool Verify(string password, UserCredential? credential)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = credential?.Salt ?? DummySalt;
        var iterations = credential?.IterationCount ?? IterationCount;
        var expected = credential?.SecretHash ?? DummyHash;
        var actual = Derive(password, salt, iterations, expected.Length);
        return credential is not null && CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(
        string password,
        byte[] salt,
        int iterations,
        int outputLength = HashLength) => Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            outputLength);

    private static void ValidatePassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        if (password.Length is < 12 or > 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(password),
                "Passwords must contain between 12 and 1024 characters.");
        }
    }
}
