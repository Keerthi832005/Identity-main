export interface AuthenticationResponse {
  readonly succeeded: boolean;
  readonly failureCode: string | null;
  readonly accessToken: string | null;
  readonly accessTokenExpiresAt: string | null;
  readonly refreshToken: null;
  readonly refreshTokenExpiresAt: string | null;
  readonly authorizationVersion: number | null;
  readonly mfaChallengeId: string | null;
}

export interface LoginRequest {
  readonly employeeCode: string;
  readonly password: string;
  readonly clientId: string;
  readonly clientSecret: null;
  readonly deviceId: null;
}

export interface CompleteMfaRequest {
  readonly mfaChallengeId: string;
  readonly code: string;
}

export interface BrowserRefreshRequest {
  readonly clientId: string;
}

export interface OperationResponse {
  readonly succeeded: boolean;
  readonly failureCode: string | null;
}

export interface SignInResult {
  readonly requiresTwoFactor: boolean;
}

export interface JwtPayload {
  readonly sub?: string;
  readonly employee_code?: string;
  readonly display_name?: string;
  readonly capability?: string | string[];
  readonly role?: string | string[];
  readonly exp?: number;
  readonly authorization_version?: string;
}
