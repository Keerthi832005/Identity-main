namespace Identity.Contracts.Administration;

public sealed record UserOverrideSummaryResponse(
    long UserPermissionOverrideId,
    long UserId,
    long ApplicationId,
    long ModuleCapabilityId,
    string CapabilityCode,
    string Effect,
    string Reason,
    DateTime AssignedAt,
    DateTime? ExpiresAt,
    DateTime? RevokedAt);
