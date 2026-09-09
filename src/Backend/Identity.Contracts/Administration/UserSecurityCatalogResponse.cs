namespace Identity.Contracts.Administration;

public sealed record CredentialSummaryResponse(
    long UserCredentialId,
    string CredentialType,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    DateTime? RevokedAt);

public sealed record DeviceSummaryResponse(
    long DeviceId,
    string DeviceName,
    string DeviceType,
    bool IsTrusted,
    DateTime? TrustedUntil,
    bool IsActive,
    DateTime FirstSeenAt,
    DateTime? LastSeenAt,
    DateTime? RevokedAt);

public sealed record MfaMethodSummaryResponse(
    long UserMfaMethodId,
    string MethodType,
    string MethodName,
    bool IsPrimary,
    bool IsEnabled,
    bool IsVerified,
    DateTime CreatedAt,
    DateTime? VerifiedAt,
    DateTime? LastUsedAt,
    DateTime? RevokedAt);

public sealed record UserSecurityCatalogResponse(
    UserSummaryResponse User,
    IReadOnlyList<CredentialSummaryResponse> Credentials,
    IReadOnlyList<DeviceSummaryResponse> Devices,
    IReadOnlyList<MfaMethodSummaryResponse> MfaMethods);
