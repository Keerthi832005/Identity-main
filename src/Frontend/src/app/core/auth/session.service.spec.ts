import { provideHttpClient } from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { TestBed } from "@angular/core/testing";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { RUNTIME_CONFIG, RuntimeConfig } from "../config/runtime-config";
import { AuthState } from "./auth-state.service";
import { SessionService } from "./session.service";

const config: RuntimeConfig = {
  identityBaseUrl: "/identity",
  identityClientId: "identity-admin-web",
  applicationName: "Identity Administration",
  devExtremeLicenseKey: "",
};

function token(payload: object): string {
  return `header.${btoa(JSON.stringify(payload)).replace(/=/g, "").replace(/\+/g, "-").replace(/\//g, "_")}.signature`;
}

describe("SessionService", () => {
  let service: SessionService;
  let http: HttpTestingController;
  let auth: AuthState;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RUNTIME_CONFIG, useValue: config },
      ],
    });
    service = TestBed.inject(SessionService);
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthState);
  });

  afterEach(() => http.verify());

  it("uses the browser protocol and accepts an access token without exposing refresh tokens", async () => {
    const operation = service.signIn("ADMIN001", "correct horse battery");
    const request = http.expectOne("/identity/api/v1/auth/browser/login");
    expect(request.request.headers.get("X-Identity-Session")).toBe("browser");
    expect(request.request.body).toEqual({
      employeeCode: "ADMIN001",
      password: "correct horse battery",
      clientId: "identity-admin-web",
      clientSecret: null,
      deviceId: null,
    });
    request.flush({
      succeeded: true,
      failureCode: null,
      accessToken: token({
        exp: Math.floor(Date.now() / 1000) + 300,
        capability: "iam.admin",
      }),
      accessTokenExpiresAt: null,
      refreshToken: null,
      refreshTokenExpiresAt: null,
      authorizationVersion: 1,
      mfaChallengeId: null,
    });
    await expect(operation).resolves.toEqual({ requiresTwoFactor: false });
    expect(auth.hasCapability("iam.admin")).toBe(true);
  });

  it("shares a single rotating refresh request", async () => {
    const first = service.refreshSession();
    const second = service.refreshSession();
    const request = http.expectOne("/identity/api/v1/auth/browser/refresh");
    request.flush({
      succeeded: true,
      failureCode: null,
      accessToken: token({ exp: Math.floor(Date.now() / 1000) + 300 }),
      accessTokenExpiresAt: null,
      refreshToken: null,
      refreshTokenExpiresAt: null,
      authorizationVersion: 1,
      mfaChallengeId: null,
    });
    await Promise.all([first, second]);
    http.expectNone("/identity/api/v1/auth/browser/refresh");
  });

  it("clears local state when refresh is rejected", async () => {
    const operation = service.refreshSession();
    http
      .expectOne("/identity/api/v1/auth/browser/refresh")
      .flush(null, { status: 401, statusText: "Unauthorized" });
    await expect(operation).resolves.toBeNull();
    expect(auth.isAuthenticated()).toBe(false);
  });
});
