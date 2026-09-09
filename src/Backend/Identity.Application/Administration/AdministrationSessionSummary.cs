namespace Identity.Application.Administration;

public sealed record AdministrationSessionSummary(
    Guid TokenFamilyId,
    long UserId,
    long ApplicationId,
    long ApplicationClientId,
    long? DeviceId,
    DateTime IssuedAt,
    DateTime ExpiresAt,
    DateTime? LastConsumedAt,
    DateTime? RevokedAt,
    bool IsActive,
    string? UserDisplayName = null,
    string? EmployeeCode = null,
    string? ApplicationName = null,
    string? ClientName = null);
