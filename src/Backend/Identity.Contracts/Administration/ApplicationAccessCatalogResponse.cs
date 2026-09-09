namespace Identity.Contracts.Administration;

public sealed record ApplicationAccessCatalogResponse(
    long ApplicationId,
    IReadOnlyList<RoleSummaryResponse> Roles,
    IReadOnlyList<RolePermissionSummaryResponse> RolePermissions,
    IReadOnlyList<ModuleCapabilitySummaryResponse> Capabilities);
