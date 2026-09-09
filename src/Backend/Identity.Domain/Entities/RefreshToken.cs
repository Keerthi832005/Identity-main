namespace Identity.Domain.Entities;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public static RefreshToken Issue(
        long userId,
        long applicationId,
        long applicationClientId,
        byte[] tokenHash,
        Guid tokenFamilyId,
        int securityVersion,
        int authorizationVersion,
        long? deviceId,
        DateTime issuedAt,
        DateTime expiresAt,
        byte[]? clientAddressHash,
        byte[]? userAgentHash) => new()
        {
            UserId = DomainRules.Positive(userId, nameof(userId)),
            ApplicationId = DomainRules.Positive(applicationId, nameof(applicationId)),
            ApplicationClientId = DomainRules.Positive(applicationClientId, nameof(applicationClientId)),
            TokenHash = DomainRules.Hash(tokenHash, nameof(tokenHash), 32),
            TokenFamilyId = tokenFamilyId != Guid.Empty
            ? tokenFamilyId
            : throw new ArgumentException("Token family id is required.", nameof(tokenFamilyId)),
            SecurityVersion = securityVersion > 0
            ? securityVersion
            : throw new ArgumentOutOfRangeException(nameof(securityVersion)),
            AuthorizationVersion = authorizationVersion > 0
            ? authorizationVersion
            : throw new ArgumentOutOfRangeException(nameof(authorizationVersion)),
            DeviceId = deviceId,
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt > issuedAt
            ? expiresAt
            : throw new ArgumentOutOfRangeException(nameof(expiresAt)),
            ClientAddressHash = CopyOptionalHash(clientAddressHash, nameof(clientAddressHash)),
            UserAgentHash = CopyOptionalHash(userAgentHash, nameof(userAgentHash)),
        };

    public void Consume(DateTime consumedAt)
    {
        if (ConsumedAt is not null || RevokedAt is not null)
        {
            throw new InvalidOperationException("Refresh token is not active.");
        }

        ConsumedAt = consumedAt;
    }

    public void SetReplacement(long refreshTokenId) =>
        ReplacedByRefreshTokenId = DomainRules.Positive(refreshTokenId, nameof(refreshTokenId));

    public void Revoke(DateTime revokedAt) => RevokedAt ??= revokedAt;

    public long RefreshTokenId { get; private set; }
    public long UserId { get; private set; }
    public long ApplicationId { get; private set; }
    public long ApplicationClientId { get; private set; }
    public byte[] TokenHash { get; private set; } = [];
    public Guid TokenFamilyId { get; private set; }
    public int SecurityVersion { get; private set; }
    public int AuthorizationVersion { get; private set; }
    public long? DeviceId { get; private set; }
    public DateTime IssuedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public long? ReplacedByRefreshTokenId { get; private set; }
    public byte[]? ClientAddressHash { get; private set; }
    public byte[]? UserAgentHash { get; private set; }

    private static byte[]? CopyOptionalHash(byte[]? value, string parameterName) => value is null
        ? null
        : DomainRules.Hash(value, parameterName, 32);
}
