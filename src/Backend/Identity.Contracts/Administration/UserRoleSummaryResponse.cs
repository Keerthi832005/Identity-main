namespace Identity.Contracts.Administration;

public sealed record UserRoleSummaryResponse(
    long UserRoleId,
    long UserId,
    long ApplicationId,
    long RoleId,
    string RoleCode,
    string RoleName,
    DateTime AssignedAt,
    DateTime? RevokedAt);
