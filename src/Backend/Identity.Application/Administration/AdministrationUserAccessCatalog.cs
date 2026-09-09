namespace Identity.Application.Administration;

public sealed record AdministrationUserAccessCatalog(
    AdministrationUserSummary User,
    IReadOnlyList<AdministrationUserApplicationSummary> Applications,
    IReadOnlyList<AdministrationUserRoleSummary> RoleAssignments,
    IReadOnlyList<AdministrationUserOverrideSummary> Overrides,
    DateTime EvaluatedAt);
