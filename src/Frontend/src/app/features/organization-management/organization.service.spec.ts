import { provideHttpClient } from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { TestBed } from "@angular/core/testing";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { RUNTIME_CONFIG } from "../../core/config/runtime-config";
import { OrganizationService } from "./organization.service";
describe("OrganizationService", () => {
  let service: OrganizationService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RUNTIME_CONFIG, useValue: { identityBaseUrl: "/identity" } },
      ],
    });
    service = TestBed.inject(OrganizationService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it("sends bounded typed search and preserves false active filter", async () => {
    const operation = service.search({
      organizationId: 10,
      parentOrganizationUnitId: 100,
      unitType: "Country",
      search: " India ",
      isActive: false,
      skip: 20,
    });
    const request = http.expectOne(
      (r) => r.url === "/identity/api/v1/admin/organization-units",
    );
    for (const [key, value] of Object.entries({
      take: "20",
      skip: "20",
      organizationId: "10",
      parentOrganizationUnitId: "100",
      unitType: "Country",
      isActive: "false",
      search: "India",
    }))
      expect(request.request.params.get(key)).toBe(value);
    request.flush({ items: [], skip: 20, take: 20, totalCount: 0 });
    await operation;
  });
  it("reads details from the organization scoped route", async () => {
    const operation = service.get(10, 101);
    const request = http.expectOne(
      "/identity/api/v1/admin/organizations/10/units/101",
    );
    expect(request.request.method).toBe("GET");
    request.flush({ organizationUnitId: 101 });
    expect((await operation).organizationUnitId).toBe(101);
  });
  it("posts root/child fields without copying hierarchy or identity", async () => {
    const fields = {
      unitCode: "ORG",
      unitName: "Organization",
      description: null,
      address: {
        addressLine1: null,
        addressLine2: null,
        addressLine3: null,
        city: null,
        district: null,
        stateName: null,
        postalCode: null,
        countryCode: null,
        latitude: null,
        longitude: null,
      },
    };
    const root = service.create(fields);
    const create = http.expectOne("/identity/api/v1/admin/organizations");
    expect(create.request.method).toBe("POST");
    expect(create.request.body).toEqual(fields);
    create.flush({ organizationId: 10 });
    await root;
    const child = service.createChild(10, {
      ...fields,
      unitType: "Country",
      parentOrganizationUnitId: 100,
    });
    const childRequest = http.expectOne(
      "/identity/api/v1/admin/organizations/10/units",
    );
    expect(childRequest.request.body.unitType).toBe("Country");
    expect(childRequest.request.body.parentOrganizationUnitId).toBe(100);
    childRequest.flush({ organizationUnitId: 101 });
    await child;
  });
});
