using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

public sealed class UserCredential
{
    private UserCredential()
    {
    }

    public static UserCredential CreatePassword(
        long userId,
        string algorithm,
        int iterationCount,
        byte[] salt,
        byte[] secretHash,
        DateTime createdAt,
        DateTime? expiresAt,
        long? createdByUserId) => new()
        {
            UserId = DomainRules.Positive(userId, nameof(userId)),
            CredentialType = CredentialType.Password,
            Algorithm = DomainRules.Required(algorithm, nameof(algorithm), 30),
            IterationCount = iterationCount is >= 100000 and <= 10000000
            ? iterationCount
            : throw new ArgumentOutOfRangeException(nameof(iterationCount)),
            Salt = DomainRules.Hash(salt, nameof(salt), 32),
            SecretHash = ValidateSecretHash(secretHash),
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            CreatedByUserId = createdByUserId,
        };

    public static UserCredential CreatePin(
        long userId,
        string algorithm,
        int iterationCount,
        byte[] salt,
        byte[] secretHash,
        DateTime createdAt,
        long? createdByUserId,
        bool requiresChange = false) => new()
        {
            UserId = DomainRules.Positive(userId, nameof(userId)),
            CredentialType = CredentialType.Pin,
            RequiresChange = requiresChange,
            Algorithm = DomainRules.Required(algorithm, nameof(algorithm), 30),
            IterationCount = iterationCount is >= 100000 and <= 10000000
                ? iterationCount
                : throw new ArgumentOutOfRangeException(nameof(iterationCount)),
            Salt = DomainRules.Hash(salt, nameof(salt), 32),
            SecretHash = ValidateSecretHash(secretHash),
            CreatedAt = createdAt,
            CreatedByUserId = createdByUserId,
        };

    public void Revoke(DateTime revokedAt) => RevokedAt ??= revokedAt;

    public long UserCredentialId { get; private set; }
    public long UserId { get; private set; }
    public CredentialType CredentialType { get; private set; }
    public string Algorithm { get; private set; } = string.Empty;
    public int IterationCount { get; private set; }
    public byte[] Salt { get; private set; } = [];
    public byte[] SecretHash { get; private set; } = [];
    public DateTime CreatedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public long? CreatedByUserId { get; private set; }
    public bool RequiresChange { get; private set; }

    private static byte[] ValidateSecretHash(byte[] secretHash)
    {
        ArgumentNullException.ThrowIfNull(secretHash);
        if (secretHash.Length is < 32 or > 64)
        {
            throw new ArgumentException("Secret hashes must contain between 32 and 64 bytes.", nameof(secretHash));
        }

        return [.. secretHash];
    }
}
