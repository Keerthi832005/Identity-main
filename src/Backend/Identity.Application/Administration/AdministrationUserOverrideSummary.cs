namespace Identity.Application.Administration;

public sealed record AdministrationUserOverrideSummary(
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
