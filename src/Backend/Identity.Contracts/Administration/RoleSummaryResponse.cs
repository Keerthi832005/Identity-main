namespace Identity.Contracts.Administration;

public sealed record RoleSummaryResponse(
    long RoleId,
    long ApplicationId,
    string RoleCode,
    string RoleName,
    string? Description,
    bool IsSystem,
    bool IsActive,
    int MemberCount = 0);
