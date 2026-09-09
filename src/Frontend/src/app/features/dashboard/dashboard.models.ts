export interface ApplicationSummary {
  readonly applicationId: number;
  readonly applicationCode: string;
  readonly applicationName: string;
  readonly tokenAudience: string;
  readonly isActive: boolean;
  readonly createdAt: string;
  readonly updatedAt: string | null;
}

export interface UserSummary {
  readonly userId: number;
  readonly employeeCode: string;
  readonly displayName: string;
  readonly isActive: boolean;
  readonly securityVersion: number;
  readonly lastLoginAt: string | null;
  readonly lockoutEndAt: string | null;
  readonly createdAt: string;
  readonly updatedAt: string | null;
}

export interface AuditSummary {
  readonly authenticationAuditId: number;
  readonly userDisplayName?: string | null;
  readonly employeeCode?: string | null;
  readonly applicationName?: string | null;
  readonly userId: number | null;
  readonly applicationId: number | null;
  readonly eventType: string;
  readonly succeeded: boolean;
  readonly failureCode: string | null;
  readonly correlationId: string;
  readonly occurredAt: string;
}

export interface AuthenticationTrendHour {
  readonly hour: string;
  readonly succeeded: number;
  readonly failed: number;
}

export interface AdministrationDashboard {
  readonly applicationCount: number;
  readonly activeApplicationCount: number;
  readonly userCount: number;
  readonly activeUserCount: number;
  readonly activeSessionCount: number;
  readonly failedAuthenticationCountLast24Hours: number;
  readonly recentApplications: readonly ApplicationSummary[];
  readonly recentUsers: readonly UserSummary[];
  readonly recentAuditEvents: readonly AuditSummary[];
  readonly generatedAt: string;
  /** Optional so a page loaded against an older API still renders; absent, not zero. */
  readonly authenticationTrend?: readonly AuthenticationTrendHour[];
  readonly lockedOutUserCount?: number | null;
  readonly usersWithoutMfaCount?: number | null;
  readonly expiringClientSecretCount?: number | null;
}

export interface PagedApplications {
  readonly skip: number;
  readonly take: number;
  readonly totalCount: number;
  readonly items: readonly ApplicationSummary[];
}

export interface PagedUsers {
  readonly skip: number;
  readonly take: number;
  readonly totalCount: number;
  readonly items: readonly UserSummary[];
}
