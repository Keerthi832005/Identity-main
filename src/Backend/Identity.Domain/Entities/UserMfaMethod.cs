using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

public sealed class UserMfaMethod
{
    private UserMfaMethod()
    {
    }

    public static UserMfaMethod EnrollTotp(
        long userId,
        string methodName,
        byte[] secretEncrypted,
        string encryptionKeyId,
        bool isPrimary,
        DateTime createdAt)
    {
        ArgumentNullException.ThrowIfNull(secretEncrypted);
        if (secretEncrypted.Length == 0)
        {
            throw new ArgumentException("Encrypted MFA secret is required.", nameof(secretEncrypted));
        }

        return new UserMfaMethod
        {
            UserId = DomainRules.Positive(userId, nameof(userId)),
            MethodType = MfaMethodType.Totp,
            MethodName = DomainRules.Required(methodName, nameof(methodName), 100),
            SecretEncrypted = [.. secretEncrypted],
            EncryptionKeyId = DomainRules.Required(encryptionKeyId, nameof(encryptionKeyId), 100),
            IsPrimary = isPrimary,
            IsEnabled = true,
            CreatedAt = createdAt,
        };
    }

    public void Verify(DateTime verifiedAt)
    {
        if (RevokedAt is not null)
        {
            throw new InvalidOperationException("Revoked MFA methods cannot be verified.");
        }

        IsVerified = true;
        VerifiedAt ??= verifiedAt;
    }

    public bool TryRecordUse(DateTime usedAt, long acceptedTimeStep)
    {
        if (!IsEnabled || !IsVerified || RevokedAt is not null)
        {
            throw new InvalidOperationException("MFA method is not available.");
        }
        if (acceptedTimeStep < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(acceptedTimeStep));
        }
        if (LastAcceptedTimeStep >= acceptedTimeStep)
        {
            return false;
        }

        LastUsedAt = usedAt;
        LastAcceptedTimeStep = acceptedTimeStep;
        return true;
    }

    public void Revoke(DateTime revokedAt)
    {
        IsEnabled = false;
        IsPrimary = false;
        RevokedAt ??= revokedAt;
    }

    public long UserMfaMethodId { get; private set; }
    public long UserId { get; private set; }
    public MfaMethodType MethodType { get; private set; }
    public string MethodName { get; private set; } = string.Empty;
    public byte[]? SecretEncrypted { get; private set; }
    public byte[]? DestinationEncrypted { get; private set; }
    public string EncryptionKeyId { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }
    public bool IsEnabled { get; private set; }
    public bool IsVerified { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? VerifiedAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }
    public long? LastAcceptedTimeStep { get; private set; }
    public DateTime? RevokedAt { get; private set; }
}
