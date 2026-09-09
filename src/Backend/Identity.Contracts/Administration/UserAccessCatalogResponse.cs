namespace Identity.Contracts.Administration;

public sealed record UserAccessCatalogResponse(
    UserSummaryResponse User,
    IReadOnlyList<UserApplicationSummaryResponse> Applications,
    IReadOnlyList<UserRoleSummaryResponse> RoleAssignments,
    IReadOnlyList<UserOverrideSummaryResponse> Overrides,
    DateTime EvaluatedAt);
