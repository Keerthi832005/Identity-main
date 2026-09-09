namespace Identity.Application.Administration;

public sealed record AdministrationApplicationAccessCatalog(
    long ApplicationId,
    IReadOnlyList<AdministrationRoleSummary> Roles,
    IReadOnlyList<AdministrationRolePermissionSummary> RolePermissions,
    IReadOnlyList<AdministrationCapabilitySummary> Capabilities);
