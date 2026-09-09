namespace Identity.Application.Administration;

public sealed record AdministrationApplicationUserSummary(
    long UserId,
    string EmployeeCode,
    string DisplayName,
    string? Email,
    bool IsActive,
    DateTime AssignedAt,
    DateTime? RevokedAt,
    IReadOnlyList<string> Roles);
