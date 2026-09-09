namespace Identity.Domain.Entities;

public sealed class Device
{
    private Device()
    {
    }

    public static Device Create(
        long userId,
        string deviceName,
        string deviceType,
        byte[] deviceFingerprintHash,
        byte[]? userAgentHash,
        byte[]? clientAddressHash,
        DateTime createdAt) => new()
        {
            UserId = DomainRules.Positive(userId, nameof(userId)),
            DeviceName = DomainRules.Required(deviceName, nameof(deviceName), 200),
            DeviceType = DomainRules.Required(deviceType, nameof(deviceType), 50),
            DeviceFingerprintHash = DomainRules.Hash(
            deviceFingerprintHash,
            nameof(deviceFingerprintHash),
            32),
            UserAgentHash = userAgentHash is null
            ? null
            : DomainRules.Hash(userAgentHash, nameof(userAgentHash), 32),
            ClientAddressHash = clientAddressHash is null
            ? null
            : DomainRules.Hash(clientAddressHash, nameof(clientAddressHash), 32),
            IsActive = true,
            FirstSeenAt = createdAt,
            CreatedAt = createdAt,
        };

    public void Revoke(long? revokedByUserId, DateTime revokedAt)
    {
        IsActive = false;
        IsTrusted = false;
        TrustedUntil = null;
        RevokedAt ??= revokedAt;
        RevokedByUserId ??= revokedByUserId;
    }

    public void AssignTerminalService(long? terminalServiceUserId)
    {
        if (terminalServiceUserId is null)
        {
            TerminalServiceUserId = null;
            return;
        }
        if (!IsActive || RevokedAt is not null)
        {
            throw new InvalidOperationException("Revoked devices cannot attest as a terminal.");
        }

        TerminalServiceUserId = DomainRules.Positive(terminalServiceUserId.Value, nameof(terminalServiceUserId));
    }

    // A terminal signs in with its agent key instead of a password, so it must be both trusted and
    // pointed at a dedicated account; it must never inherit the enrolling administrator's rights.
    public bool CanAttestAt(DateTime evaluatedAt) =>
        IsTrustedAt(evaluatedAt) && TerminalServiceUserId is not null;

    public void Trust(DateTime trustedAt, TimeSpan trustDuration)
    {
        if (!IsActive || RevokedAt is not null)
        {
            throw new InvalidOperationException("Revoked devices cannot be trusted.");
        }
        if (trustDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(trustDuration));
        }

        IsTrusted = true;
        TrustedUntil = trustedAt.Add(trustDuration);
        LastSeenAt = trustedAt;
    }

    public bool IsTrustedAt(DateTime evaluatedAt) =>
        IsActive && RevokedAt is null && IsTrusted && TrustedUntil > evaluatedAt;

    public long DeviceId { get; private set; }
    public long UserId { get; private set; }
    public string DeviceName { get; private set; } = string.Empty;
    public string DeviceType { get; private set; } = string.Empty;
    public byte[] DeviceFingerprintHash { get; private set; } = [];
    public byte[]? UserAgentHash { get; private set; }
    public byte[]? ClientAddressHash { get; private set; }
    public bool IsTrusted { get; private set; }
    public DateTime? TrustedUntil { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime FirstSeenAt { get; private set; }
    public DateTime? LastSeenAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public long? RevokedByUserId { get; private set; }
    public long? TerminalServiceUserId { get; private set; }
}
