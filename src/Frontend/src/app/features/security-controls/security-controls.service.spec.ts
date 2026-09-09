import { provideHttpClient } from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { TestBed } from "@angular/core/testing";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import {
  RUNTIME_CONFIG,
  RuntimeConfig,
} from "../../core/config/runtime-config";
import { SecurityControlsService } from "./security-controls.service";

const config: RuntimeConfig = {
  identityBaseUrl: "/identity",
  identityClientId: "web",
  applicationName: "Identity",
  devExtremeLicenseKey: "",
};

describe("SecurityControlsService", () => {
  let service: SecurityControlsService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RUNTIME_CONFIG, useValue: config },
      ],
    });
    service = TestBed.inject(SecurityControlsService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it("requests the selected page of matching users", async () => {
    const operation = service.searchUsers("  Anand  ", 50);
    const request = http.expectOne(
      (item) => item.url === "/identity/api/v1/admin/users",
    );
    expect(request.request.params.get("search")).toBe("Anand");
    expect(request.request.params.get("skip")).toBe("50");
    expect(request.request.params.get("take")).toBe("50");
    request.flush({ items: [], totalCount: 50 });
    await operation;
  });

  it("loads only non-sensitive security metadata", async () => {
    const operation = service.getSecurity(42);
    http.expectOne("/identity/api/v1/admin/users/42/security").flush({
      user: { userId: 42, securityVersion: 3 },
      credentials: [],
      devices: [],
      mfaMethods: [],
    });
    await expect(operation).resolves.toMatchObject({
      user: { securityVersion: 3 },
    });
  });

  it("sends the typed TOTP verification request", async () => {
    const operation = service.verifyTotp(80, { code: "123456" });
    const request = http.expectOne("/identity/api/v1/admin/mfa/80/verify");
    expect(request.request.body).toEqual({ code: "123456" });
    request.flush({ succeeded: true, failureCode: null });
    await operation;
  });
});
