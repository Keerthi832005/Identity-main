namespace Identity.Application.Administration;

public sealed record AdministrationMfaMethodSummary(
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
