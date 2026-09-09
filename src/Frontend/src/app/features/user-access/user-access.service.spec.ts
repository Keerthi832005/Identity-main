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
import { UserAccessService } from "./user-access.service";
const config: RuntimeConfig = {
  identityBaseUrl: "/identity",
  identityClientId: "web",
  applicationName: "Identity",
  devExtremeLicenseKey: "",
};
describe("UserAccessService", () => {
  let service: UserAccessService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RUNTIME_CONFIG, useValue: config },
      ],
    });
    service = TestBed.inject(UserAccessService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it("creates a user with contact email and manager", async () => {
    const body = {
      employeeCode: "EMP-7",
      displayName: "Employee",
      email: "employee@example.com",
      managerUserId: 42,
    };
    const operation = service.createUser(body);
    const request = http.expectOne("/identity/api/v1/admin/users");
    expect(request.request.method).toBe("POST");
    expect(request.request.body).toEqual(body);
    request.flush({ resourceId: 7 });
    await operation;
  });
  it("updates a profile and explicitly clears optional values", async () => {
    const body = {
      displayName: "Employee updated",
      email: null,
      managerUserId: null,
    };
    const operation = service.updateUserProfile(7, body);
    const request = http.expectOne("/identity/api/v1/admin/users/7/profile");
    expect(request.request.method).toBe("PUT");
    expect(request.request.body).toEqual(body);
    request.flush({ resourceId: 7 });
    await operation;
  });
  it("loads an explainable user access catalog", async () => {
    const operation = service.getUserAccess(42);
    http.expectOne("/identity/api/v1/admin/users/42/access").flush({
      user: { userId: 42 },
      applications: [{ effectiveCapabilities: ["iam.admin"] }],
      roleAssignments: [],
      overrides: [],
      evaluatedAt: "2026-08-29T12:00:00Z",
    });
    await expect(operation).resolves.toMatchObject({
      applications: [{ effectiveCapabilities: ["iam.admin"] }],
    });
  });
  it("sends a typed Deny override with a reason", async () => {
    const operation = service.setOverride(42, 10, 22, {
      effect: "Deny",
      reason: "Incident containment",
      expiresAt: null,
    });
    const request = http.expectOne(
      "/identity/api/v1/admin/users/42/applications/10/capabilities/22/override",
    );
    expect(request.request.body).toEqual({
      effect: "Deny",
      reason: "Incident containment",
      expiresAt: null,
    });
    request.flush({
      resourceType: "UserPermissionOverride",
      resourceId: 50,
      userId: 42,
      applicationId: 10,
      authorizationVersion: 4,
    });
    await operation;
  });
});
