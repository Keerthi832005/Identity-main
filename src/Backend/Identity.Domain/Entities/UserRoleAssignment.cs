namespace Identity.Domain.Entities;

public sealed class UserRoleAssignment
{
    private UserRoleAssignment()
    {
    }

    public static UserRoleAssignment Create(
        long userId,
        long applicationId,
        long roleId,
        long? assignedByUserId,
        DateTime assignedAt) => new()
        {
            UserId = DomainRules.Positive(userId, nameof(userId)),
            ApplicationId = DomainRules.Positive(applicationId, nameof(applicationId)),
            RoleId = DomainRules.Positive(roleId, nameof(roleId)),
            AssignedAt = assignedAt,
            AssignedByUserId = assignedByUserId,
        };

    public void Revoke(long? revokedByUserId, DateTime revokedAt)
    {
        RevokedAt ??= revokedAt;
        RevokedByUserId ??= revokedByUserId;
    }

    public long UserRoleId { get; private set; }
    public long UserId { get; private set; }
    public long ApplicationId { get; private set; }
    public long RoleId { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public long? AssignedByUserId { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public long? RevokedByUserId { get; private set; }
}
