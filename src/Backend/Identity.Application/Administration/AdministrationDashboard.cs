namespace Identity.Application.Administration;

public sealed record AdministrationDashboard(
    int ApplicationCount,
    int ActiveApplicationCount,
    int UserCount,
    int ActiveUserCount,
    int ActiveSessionCount,
    int FailedAuthenticationCountLast24Hours,
    IReadOnlyList<AdministrationApplicationSummary> RecentApplications,
    IReadOnlyList<AdministrationUserSummary> RecentUsers,
    IReadOnlyList<AdministrationAuditSummary> RecentAuditEvents,
    DateTime GeneratedAt,
    IReadOnlyList<AdministrationAuthenticationTrendHour>? AuthenticationTrend = null,
    int LockedOutUserCount = 0,
    int UsersWithoutMfaCount = 0,
    int ExpiringClientSecretCount = 0);
