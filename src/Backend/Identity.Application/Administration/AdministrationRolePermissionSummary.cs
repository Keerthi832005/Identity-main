namespace Identity.Application.Administration;

public sealed record AdministrationRolePermissionSummary(
    long RolePermissionId,
    long ApplicationId,
    long RoleId,
    long ModuleCapabilityId,
    string CapabilityCode,
    DateTime GrantedAt,
    DateTime? RevokedAt);
