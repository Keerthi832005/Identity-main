namespace Identity.Application.Administration;

public sealed record AdministrationRoleSummary(
    long RoleId,
    long ApplicationId,
    string RoleCode,
    string RoleName,
    string? Description,
    bool IsSystem,
    bool IsActive,
    int MemberCount = 0);
