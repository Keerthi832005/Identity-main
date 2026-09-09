namespace Identity.Domain.Entities;

public sealed class UserApplicationAccess
{
    private UserApplicationAccess()
    {
    }

    public static UserApplicationAccess Create(
        long userId,
        long applicationId,
        long? assignedByUserId,
        DateTime assignedAt) => new()
        {
            UserId = DomainRules.Positive(userId, nameof(userId)),
            ApplicationId = DomainRules.Positive(applicationId, nameof(applicationId)),
            IsActive = true,
            AuthorizationVersion = 1,
            AssignedAt = assignedAt,
            AssignedByUserId = assignedByUserId,
        };

    public void InvalidateAuthorization(long? updatedByUserId, DateTime updatedAt)
    {
        AuthorizationVersion++;
        UpdatedAt = updatedAt;
        UpdatedByUserId = updatedByUserId;
    }

    public void Revoke(long? revokedByUserId, DateTime revokedAt)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        RevokedAt = revokedAt;
        RevokedByUserId = revokedByUserId;
        InvalidateAuthorization(revokedByUserId, revokedAt);
    }

    public long UserId { get; private set; }
    public long ApplicationId { get; private set; }
    public bool IsActive { get; private set; }
    public int AuthorizationVersion { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public long? AssignedByUserId { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public long? UpdatedByUserId { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public long? RevokedByUserId { get; private set; }
}
