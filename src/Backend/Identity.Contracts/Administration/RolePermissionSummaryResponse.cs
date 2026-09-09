namespace Identity.Contracts.Administration;

public sealed record RolePermissionSummaryResponse(
    long RolePermissionId,
    long ApplicationId,
    long RoleId,
    long ModuleCapabilityId,
    string CapabilityCode,
    DateTime GrantedAt,
    DateTime? RevokedAt);
