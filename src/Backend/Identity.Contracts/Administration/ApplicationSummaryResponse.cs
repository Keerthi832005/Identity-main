namespace Identity.Contracts.Administration;

public sealed record ApplicationSummaryResponse(
    long ApplicationId,
    string ApplicationCode,
    string ApplicationName,
    string TokenAudience,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? Description = null,
    int ClientCount = 0,
    int ModuleCount = 0,
    int RoleCount = 0,
    int UserCount = 0);
