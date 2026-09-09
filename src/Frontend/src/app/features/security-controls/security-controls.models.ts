export interface UserSummary {
  readonly userId: number;
  readonly employeeCode: string;
  readonly displayName: string;
  readonly isActive: boolean;
  readonly securityVersion: number;
}

export interface PagedUsers {
  readonly totalCount: number;
  readonly items: readonly UserSummary[];
}

export interface CredentialSummary {
  readonly userCredentialId: number;
  readonly credentialType: string;
  readonly createdAt: string;
  readonly expiresAt: string | null;
  readonly revokedAt: string | null;
}

export interface DeviceSummary {
  readonly deviceId: number;
  readonly deviceName: string;
  readonly deviceType: string;
  readonly isTrusted: boolean;
  readonly trustedUntil: string | null;
  readonly isActive: boolean;
  readonly firstSeenAt: string;
  readonly lastSeenAt: string | null;
  readonly revokedAt: string | null;
}

export interface MfaMethodSummary {
  readonly userMfaMethodId: number;
  readonly methodType: string;
  readonly methodName: string;
  readonly isPrimary: boolean;
  readonly isEnabled: boolean;
  readonly isVerified: boolean;
  readonly createdAt: string;
  readonly verifiedAt: string | null;
  readonly lastUsedAt: string | null;
  readonly revokedAt: string | null;
}

export interface UserSecurityCatalog {
  readonly user: UserSummary;
  readonly credentials: readonly CredentialSummary[];
  readonly devices: readonly DeviceSummary[];
  readonly mfaMethods: readonly MfaMethodSummary[];
}

export interface OperationResponse {
  readonly succeeded: boolean;
  readonly failureCode: string | null;
}

export interface AdministrationResponse {
  readonly resourceId: number;
}

export interface TotpEnrollmentResponse {
  readonly userMfaMethodId: number;
  readonly secret: string;
  readonly algorithm: string;
  readonly digits: number;
  readonly periodSeconds: number;
}

export interface SetPasswordRequest {
  readonly password: string;
  readonly expiresAt: string | null;
}
export interface SetPinRequest {
  readonly pin: string;
}
export interface RegisterDeviceRequest {
  readonly deviceName: string;
  readonly deviceType: string;
  readonly deviceFingerprint: string;
}
export interface EnrollTotpRequest {
  readonly methodName: string;
  readonly isPrimary: boolean;
}
export interface VerifyTotpRequest {
  readonly code: string;
}
