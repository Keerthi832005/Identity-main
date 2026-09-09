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
import { DashboardService } from "./dashboard.service";

const config: RuntimeConfig = {
  identityBaseUrl: "/identity",
  identityClientId: "identity-admin-web",
  applicationName: "Identity Administration",
  devExtremeLicenseKey: "",
};

describe("DashboardService", () => {
  let service: DashboardService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RUNTIME_CONFIG, useValue: config },
      ],
    });
    service = TestBed.inject(DashboardService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("loads a bounded dashboard", async () => {
    const operation = service.getDashboard(6);
    const request = http.expectOne(
      (candidate) => candidate.url === "/identity/api/v1/admin/dashboard",
    );
    expect(request.request.params.get("recent")).toBe("6");
    request.flush({
      applicationCount: 0,
      activeApplicationCount: 0,
      userCount: 0,
      activeUserCount: 0,
      activeSessionCount: 0,
      failedAuthenticationCountLast24Hours: 0,
      recentApplications: [],
      recentUsers: [],
      recentAuditEvents: [],
      generatedAt: "2026-08-29T12:00:00Z",
    });
    await expect(operation).resolves.toMatchObject({ applicationCount: 0 });
  });

  it("trims application search and sends bounded paging", async () => {
    const operation = service.searchApplications("  identity ", 10, 25);
    const request = http.expectOne(
      (candidate) => candidate.url === "/identity/api/v1/admin/applications",
    );
    expect(request.request.params.get("search")).toBe("identity");
    expect(request.request.params.get("skip")).toBe("10");
    expect(request.request.params.get("take")).toBe("25");
    request.flush({ skip: 10, take: 25, totalCount: 0, items: [] });
    await operation;
  });
});
