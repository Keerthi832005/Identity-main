namespace Identity.Application.Administration;

public sealed record AdministrationUserRoleSummary(
    long UserRoleId,
    long UserId,
    long ApplicationId,
    long RoleId,
    string RoleCode,
    string RoleName,
    DateTime AssignedAt,
    DateTime? RevokedAt);
