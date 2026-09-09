namespace Identity.Application.Administration;

public sealed record AdministrationDeviceSummary(
    long DeviceId,
    string DeviceName,
    string DeviceType,
    bool IsTrusted,
    DateTime? TrustedUntil,
    bool IsActive,
    DateTime FirstSeenAt,
    DateTime? LastSeenAt,
    DateTime? RevokedAt);
