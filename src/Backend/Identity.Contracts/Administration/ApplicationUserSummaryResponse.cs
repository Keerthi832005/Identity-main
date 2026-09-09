namespace Identity.Contracts.Administration;

public sealed record ApplicationUserSummaryResponse(
    long UserId,
    string EmployeeCode,
    string DisplayName,
    string? Email,
    bool IsActive,
    DateTime AssignedAt,
    DateTime? RevokedAt,
    IReadOnlyList<string> Roles);
