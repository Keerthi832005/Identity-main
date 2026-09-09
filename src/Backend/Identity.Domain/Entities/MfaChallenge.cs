namespace Identity.Domain.Entities;

public sealed class MfaChallenge
{
    private MfaChallenge()
    {
    }

    public static MfaChallenge Create(
        Guid challengeId,
        long userId,
        long applicationId,
        long applicationClientId,
        long userMfaMethodId,
        long? deviceId,
        byte[] challengeHash,
        string challengeKeyId,
        DateTime createdAt,
        DateTime expiresAt,
        int maximumAttemptCount,
        Guid correlationId) => new()
        {
            MfaChallengeId = challengeId != Guid.Empty
            ? challengeId
            : throw new ArgumentException("Challenge id is required.", nameof(challengeId)),
            UserId = DomainRules.Positive(userId, nameof(userId)),
            ApplicationId = DomainRules.Positive(applicationId, nameof(applicationId)),
            ApplicationClientId = DomainRules.Positive(applicationClientId, nameof(applicationClientId)),
            UserMfaMethodId = DomainRules.Positive(userMfaMethodId, nameof(userMfaMethodId)),
            DeviceId = deviceId.HasValue
            ? DomainRules.Positive(deviceId.Value, nameof(deviceId))
            : null,
            ChallengeHash = DomainRules.Hash(challengeHash, nameof(challengeHash), 32),
            ChallengeKeyId = DomainRules.Required(challengeKeyId, nameof(challengeKeyId), 100),
            CreatedAt = createdAt,
            ExpiresAt = expiresAt > createdAt
            ? expiresAt
            : throw new ArgumentOutOfRangeException(nameof(expiresAt)),
            MaximumAttemptCount = maximumAttemptCount is >= 1 and <= 10
            ? maximumAttemptCount
            : throw new ArgumentOutOfRangeException(nameof(maximumAttemptCount)),
            CorrelationId = correlationId != Guid.Empty
            ? correlationId
            : throw new ArgumentException("Correlation id is required.", nameof(correlationId)),
        };

    public bool CanAttempt(DateTime attemptedAt) => VerifiedAt is null
        && attemptedAt <= ExpiresAt
        && AttemptCount < MaximumAttemptCount;

    public void RecordFailedAttempt(DateTime attemptedAt)
    {
        if (!CanAttempt(attemptedAt))
        {
            throw new InvalidOperationException("MFA challenge cannot accept another attempt.");
        }

        AttemptCount++;
    }

    public void Complete(DateTime verifiedAt)
    {
        if (!CanAttempt(verifiedAt))
        {
            throw new InvalidOperationException("MFA challenge cannot be completed.");
        }

        VerifiedAt = verifiedAt;
    }

    public Guid MfaChallengeId { get; private set; }
    public long UserId { get; private set; }
    public long ApplicationId { get; private set; }
    public long ApplicationClientId { get; private set; }
    public long UserMfaMethodId { get; private set; }
    public long? DeviceId { get; private set; }
    public byte[] ChallengeHash { get; private set; } = [];
    public string ChallengeKeyId { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? VerifiedAt { get; private set; }
    public int AttemptCount { get; private set; }
    public int MaximumAttemptCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid CorrelationId { get; private set; }
}
