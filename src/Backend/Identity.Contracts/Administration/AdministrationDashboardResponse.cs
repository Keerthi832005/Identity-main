namespace Identity.Contracts.Administration;

public sealed record AdministrationDashboardResponse(
    int ApplicationCount,
    int ActiveApplicationCount,
    int UserCount,
    int ActiveUserCount,
    int ActiveSessionCount,
    int FailedAuthenticationCountLast24Hours,
    IReadOnlyList<ApplicationSummaryResponse> RecentApplications,
    IReadOnlyList<UserSummaryResponse> RecentUsers,
    IReadOnlyList<AuditSummaryResponse> RecentAuditEvents,
    DateTime GeneratedAt,
    IReadOnlyList<AuthenticationTrendHourResponse>? AuthenticationTrend = null,
    int? LockedOutUserCount = null,
    int? UsersWithoutMfaCount = null,
    int? ExpiringClientSecretCount = null);
