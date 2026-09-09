import {
  HttpClient,
  HttpErrorResponse,
  HttpHeaders,
} from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { RUNTIME_CONFIG } from "../config/runtime-config";
import { AuthState } from "./auth-state.service";
import {
  AuthenticationResponse,
  BrowserRefreshRequest,
  CompleteMfaRequest,
  LoginRequest,
  OperationResponse,
  SignInResult,
} from "./session.models";

@Injectable({ providedIn: "root" })
export class SessionService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthState);
  private readonly config = inject(RUNTIME_CONFIG);
  private readonly browserHeaders = new HttpHeaders({
    "X-Identity-Session": "browser",
  });
  private mfaChallengeId: string | null = null;
  private refreshInFlight: Promise<string | null> | null = null;

  async signIn(employeeCode: string, password: string): Promise<SignInResult> {
    const request: LoginRequest = {
      employeeCode,
      password,
      clientId: this.config.identityClientId,
      clientSecret: null,
      deviceId: null,
    };
    const response = await firstValueFrom(
      this.http.post<AuthenticationResponse>(this.url("/login"), request, {
        headers: this.browserHeaders,
      }),
    );
    this.accept(response);
    this.mfaChallengeId = response.mfaChallengeId;
    return { requiresTwoFactor: Boolean(response.mfaChallengeId) };
  }

  async completeMfa(code: string): Promise<void> {
    if (!this.mfaChallengeId)
      throw new Error("The MFA challenge has expired. Sign in again.");
    const request: CompleteMfaRequest = {
      mfaChallengeId: this.mfaChallengeId,
      code,
    };
    const response = await firstValueFrom(
      this.http.post<AuthenticationResponse>(
        this.url("/mfa/complete"),
        request,
        {
          headers: this.browserHeaders,
        },
      ),
    );
    this.accept(response);
    this.mfaChallengeId = null;
  }

  cancelMfa(): void {
    this.mfaChallengeId = null;
  }

  async recover(): Promise<void> {
    try {
      await this.refreshSession();
    } catch {
      this.auth.clear();
    }
  }

  async refreshSession(): Promise<string | null> {
    if (this.refreshInFlight) return this.refreshInFlight;
    const operation = this.performRefresh();
    this.refreshInFlight = operation;
    try {
      return await operation;
    } finally {
      if (this.refreshInFlight === operation) this.refreshInFlight = null;
    }
  }

  async logout(): Promise<void> {
    try {
      await firstValueFrom(
        this.http.post<OperationResponse>(this.url("/logout"), null, {
          headers: this.browserHeaders,
        }),
      );
    } finally {
      this.mfaChallengeId = null;
      this.auth.clear();
    }
  }

  private async performRefresh(): Promise<string | null> {
    const request: BrowserRefreshRequest = {
      clientId: this.config.identityClientId,
    };
    try {
      const response = await firstValueFrom(
        this.http.post<AuthenticationResponse>(this.url("/refresh"), request, {
          headers: this.browserHeaders,
        }),
      );
      this.accept(response);
      return response.accessToken;
    } catch (error) {
      if (
        error instanceof HttpErrorResponse &&
        (error.status === 401 || error.status === 403)
      ) {
        this.auth.clear();
        return null;
      }
      throw error;
    }
  }

  private accept(response: AuthenticationResponse): void {
    if (typeof response.refreshToken === "string") {
      this.auth.clear();
      throw new Error("Identity returned an invalid browser-session response.");
    }
    if (response.accessToken) {
      this.auth.setToken(response.accessToken);
      return;
    }
    if (!response.mfaChallengeId)
      throw new Error(response.failureCode ?? "Identity denied access.");
  }

  private url(path: string): string {
    return `${this.config.identityBaseUrl}/api/v1/auth/browser${path}`;
  }
}
