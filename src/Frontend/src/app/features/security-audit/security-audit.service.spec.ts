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
import { SecurityAuditService } from "./security-audit.service";

const config: RuntimeConfig = {
  identityBaseUrl: "/identity",
  identityClientId: "web",
  applicationName: "Identity",
  devExtremeLicenseKey: "",
};
describe("SecurityAuditService", () => {
  let service: SecurityAuditService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RUNTIME_CONFIG, useValue: config },
      ],
    });
    service = TestBed.inject(SecurityAuditService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it("requests bounded privacy-safe operations and carries the caller's page", async () => {
    const operation = service.load({
      skip: 50,
      take: 50,
      userId: 42,
      applicationId: null,
      eventType: "LoginFailed",
      succeeded: false,
      correlationId: "",
      from: "",
      to: "",
    });
    const request = http.expectOne(
      (value) => value.url === "/identity/api/v1/admin/security/operations",
    );
    expect(request.request.params.get("skip")).toBe("50");
    expect(request.request.params.get("take")).toBe("50");
    expect(request.request.params.get("userId")).toBe("42");
    /* An empty filter must not become an empty query value the server has to interpret. */
    expect(request.request.params.has("correlationId")).toBe(false);
    request.flush({
      skip: 0,
      take: 100,
      totalAuditCount: 0,
      audits: [],
      sessions: [],
      generatedAt: "2026-08-29T00:00:00Z",
    });
    await operation;
  });
});
