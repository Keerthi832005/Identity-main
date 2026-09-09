namespace Identity.Domain.Entities;

public sealed class RolePermission
{
    private RolePermission()
    {
    }

    public static RolePermission Create(
        long applicationId,
        long roleId,
        long moduleCapabilityId,
        long? grantedByUserId,
        DateTime grantedAt) => new()
        {
            ApplicationId = DomainRules.Positive(applicationId, nameof(applicationId)),
            RoleId = DomainRules.Positive(roleId, nameof(roleId)),
            ModuleCapabilityId = DomainRules.Positive(moduleCapabilityId, nameof(moduleCapabilityId)),
            GrantedAt = grantedAt,
            GrantedByUserId = grantedByUserId,
        };

    public void Revoke(long? revokedByUserId, DateTime revokedAt)
    {
        RevokedAt ??= revokedAt;
        RevokedByUserId ??= revokedByUserId;
    }

    public long RolePermissionId { get; private set; }
    public long ApplicationId { get; private set; }
    public long RoleId { get; private set; }
    public long ModuleCapabilityId { get; private set; }
    public DateTime GrantedAt { get; private set; }
    public long? GrantedByUserId { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public long? RevokedByUserId { get; private set; }
}
