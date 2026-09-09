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
export interface SessionSummary {
  readonly userDisplayName?: string | null;
  readonly employeeCode?: string | null;
  readonly applicationName?: string | null;
  readonly clientName?: string | null;
  readonly tokenFamilyId: string;
  readonly userId: number;
  readonly applicationId: number;
  readonly applicationClientId: number;
  readonly deviceId: number | null;
  readonly issuedAt: string;
  readonly expiresAt: string;
  readonly lastConsumedAt: string | null;
  readonly revokedAt: string | null;
  readonly isActive: boolean;
}
export interface SecurityOperations {
  readonly skip: number;
  readonly take: number;
  readonly totalAuditCount: number;
  readonly audits: readonly AuditSummary[];
  readonly sessions: readonly SessionSummary[];
  readonly generatedAt: string;
}
export interface SecurityOperationsFilter {
  readonly skip: number;
  readonly take: number;
  readonly userId: number | null;
  readonly applicationId: number | null;
  readonly eventType: string;
  readonly succeeded: boolean | null;
  readonly correlationId: string;
  readonly from: string;
  readonly to: string;
}
