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
import { ApplicationCatalogService } from "./application-catalog.service";

const config: RuntimeConfig = {
  identityBaseUrl: "/identity",
  identityClientId: "identity-admin-web",
  applicationName: "Identity Administration",
  devExtremeLicenseKey: "",
};

describe("ApplicationCatalogService", () => {
  let service: ApplicationCatalogService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RUNTIME_CONFIG, useValue: config },
      ],
    });
    service = TestBed.inject(ApplicationCatalogService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("loads a typed application catalog", async () => {
    const operation = service.getCatalog(12);
    const request = http.expectOne(
      "/identity/api/v1/admin/applications/12/catalog",
    );
    request.flush({
      application: { applicationId: 12 },
      clients: [],
      modules: [],
      capabilities: [],
    });
    await expect(operation).resolves.toMatchObject({
      application: { applicationId: 12 },
    });
  });

  it("loads the application access catalog", async () => {
    const operation = service.getAccess(12);
    const request = http.expectOne(
      "/identity/api/v1/admin/applications/12/access",
    );
    request.flush({
      applicationId: 12,
      roles: [],
      rolePermissions: [],
      capabilities: [],
    });
    await expect(operation).resolves.toMatchObject({ applicationId: 12 });
  });

  it("sends a public client without a secret", async () => {
    const operation = service.createClient(12, {
      clientId: "web",
      clientName: "Web",
      clientType: "Public",
      clientSecret: null,
      expiresAt: null,
    });
    const request = http.expectOne(
      "/identity/api/v1/admin/applications/12/clients",
    );
    expect(request.request.body.clientSecret).toBeNull();
    request.flush({
      resourceType: "ApplicationClient",
      resourceId: 22,
      userId: null,
      applicationId: 12,
      authorizationVersion: null,
    });
    await operation;
  });
});
