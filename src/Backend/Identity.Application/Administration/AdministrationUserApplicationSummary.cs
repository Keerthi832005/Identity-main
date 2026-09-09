namespace Identity.Application.Administration;

public sealed record AdministrationUserApplicationSummary(
    long UserId,
    long ApplicationId,
    string ApplicationCode,
    string ApplicationName,
    bool IsActive,
    int AuthorizationVersion,
    DateTime AssignedAt,
    DateTime? RevokedAt,
    IReadOnlyList<string> EffectiveCapabilities);
