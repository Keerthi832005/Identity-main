using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

public sealed class UserPermissionOverride
{
    private UserPermissionOverride()
    {
    }

    public static UserPermissionOverride Create(
        long userId,
        long applicationId,
        long moduleCapabilityId,
        PermissionEffect effect,
        string reason,
        long assignedByUserId,
        DateTime assignedAt,
        DateTime? expiresAt) => new()
        {
            UserId = DomainRules.Positive(userId, nameof(userId)),
            ApplicationId = DomainRules.Positive(applicationId, nameof(applicationId)),
            ModuleCapabilityId = DomainRules.Positive(moduleCapabilityId, nameof(moduleCapabilityId)),
            Effect = effect,
            Reason = DomainRules.Required(reason, nameof(reason), 500),
            AssignedAt = assignedAt,
            AssignedByUserId = DomainRules.Positive(assignedByUserId, nameof(assignedByUserId)),
            ExpiresAt = expiresAt,
        };

    public void Revoke(long? revokedByUserId, DateTime revokedAt)
    {
        RevokedAt ??= revokedAt;
        RevokedByUserId ??= revokedByUserId;
    }

    public long UserPermissionOverrideId { get; private set; }
    public long UserId { get; private set; }
    public long ApplicationId { get; private set; }
    public long ModuleCapabilityId { get; private set; }
    public PermissionEffect Effect { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime AssignedAt { get; private set; }
    public long AssignedByUserId { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public long? RevokedByUserId { get; private set; }
}
