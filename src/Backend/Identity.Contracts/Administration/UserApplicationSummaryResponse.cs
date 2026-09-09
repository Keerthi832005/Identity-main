namespace Identity.Contracts.Administration;

public sealed record UserApplicationSummaryResponse(
    long UserId,
    long ApplicationId,
    string ApplicationCode,
    string ApplicationName,
    bool IsActive,
    int AuthorizationVersion,
    DateTime AssignedAt,
    DateTime? RevokedAt,
    IReadOnlyList<string> EffectiveCapabilities);
