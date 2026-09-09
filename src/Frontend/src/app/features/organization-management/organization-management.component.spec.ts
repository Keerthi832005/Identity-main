import { ConfirmationService } from "../../shared/ui/confirmation/confirmation.service";
import {
  provideRouter,
  ActivatedRoute,
  convertToParamMap,
  Router,
} from "@angular/router";
import { of } from "rxjs";
import { OrganizationEditorComponent } from "./organization-editor.component";
import { TestBed } from "@angular/core/testing";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { OrganizationManagementComponent } from "./organization-management.component";
import { OrganizationService } from "./organization.service";
import { OrganizationUnit } from "./organization.models";
import { provideHttpClient } from "@angular/common/http";
import { RUNTIME_CONFIG } from "../../core/config/runtime-config";

const testRuntimeConfig = {
  identityBaseUrl: "/identity",
  identityClientId: "identity-admin-web",
  applicationName: "Identity Administration",
  devExtremeLicenseKey: "",
};
const root: OrganizationUnit = {
  organizationId: 10,
  organizationUnitId: 100,
  parentOrganizationUnitId: null,
  unitType: "Organization",
  unitCode: "ORG",
  unitName: "Example organization",
  description: "Head office",
  hierarchyPath: "/100/",
  isActive: true,
  createdAt: "2026-08-30T00:00:00Z",
  updatedAt: null,
  rowVersion: "AAAAAAAAAAE=",
  address: {
    addressLine1: null,
    addressLine2: null,
    addressLine3: null,
    city: "Chennai",
    district: null,
    stateName: null,
    postalCode: null,
    countryCode: "IND",
    latitude: 13,
    longitude: 80,
  },
};
const country: OrganizationUnit = {
  ...root,
  organizationUnitId: 101,
  parentOrganizationUnitId: 100,
  unitType: "Country",
  unitCode: "IN",
  unitName: "India",
  hierarchyPath: "/100/101/",
};
describe("Organization browsing", () => {
  const service = { search: vi.fn(), get: vi.fn() };
  beforeEach(() => {
    vi.resetAllMocks();
    service.search.mockResolvedValue({
      items: [root],
      skip: 0,
      take: 20,
      totalCount: 1,
    });
    service.get.mockImplementation((_org: number, id: number) =>
      Promise.resolve(id === 100 ? root : country),
    );
    TestBed.configureTestingModule({
      imports: [OrganizationManagementComponent],
      providers: [
        provideRouter([]),
        {
          provide: ConfirmationService,
          useValue: { ask: vi.fn().mockResolvedValue(true) },
        },
        { provide: OrganizationService, useValue: service },
        provideHttpClient(),
        // The bulk toolbar resolves BulkDataService, which reads the deployment base URL.
        { provide: RUNTIME_CONFIG, useValue: testRuntimeConfig },
      ],
    });
  });
  function component() {
    return TestBed.createComponent(OrganizationManagementComponent)
      .componentInstance;
  }
  it("configures the organization unit directory and details", async () => {
    const page = component();
    await page["load"]();
    expect(page["unitColumns"]).toHaveLength(5);
    await page["select"](root);
    expect(page["detail"]()).toEqual(root);
  });
  it("scopes child browsing and clears the parent filter on type changes", async () => {
    const page = component();
    await page["select"](country);
    page["browse"](country);
    await Promise.resolve();
    expect(service.search).toHaveBeenLastCalledWith(
      expect.objectContaining({
        organizationId: 10,
        parentOrganizationUnitId: 101,
        unitType: undefined,
        skip: 0,
        take: 20,
      }),
    );
    page["chooseType"]("Team");
    await Promise.resolve();
    expect(service.search).toHaveBeenLastCalledWith(
      expect.objectContaining({
        organizationId: 10,
        parentOrganizationUnitId: undefined,
        unitType: "Team",
        skip: 0,
      }),
    );
    page["reset"]();
    expect(page["organization"]()).toBeNull();
  });
  it("ignores superseded lists and preserves server total and paging", async () => {
    const page = component();
    let resolve!: (value: unknown) => void;
    service.search.mockReturnValueOnce(new Promise((done) => (resolve = done)));
    const old = page["load"]();
    service.search.mockResolvedValueOnce({
      items: [country],
      skip: 20,
      take: 20,
      totalCount: 41,
    });
    await page["load"](20);
    resolve({ items: [root], skip: 0, take: 20, totalCount: 1 });
    await old;
    expect(page["page"]().items).toEqual([country]);
    expect(page["pageNumber"]()).toBe(2);
    expect(page["pageCount"]()).toBe(3);
  });
  it("ignores delayed detail selections and reports failures", async () => {
    const page = component();
    let resolve!: (value: OrganizationUnit) => void;
    service.get.mockReturnValueOnce(new Promise((done) => (resolve = done)));
    const old = page["select"](root);
    await page["select"](country);
    resolve(root);
    await old;
    expect(page["detail"]()?.organizationUnitId).toBe(101);
    service.get.mockRejectedValueOnce(new Error("offline"));
    await page["select"](root);
    expect(page["detail"]()).toBeNull();
    expect(page["detailError"]()).toContain("Retry");
    await page["select"](root);
    expect(page["detailError"]()).toBeNull();
  });
  it("clears failed search results but retains filters for retry", async () => {
    const page = component();
    await page["load"]();
    page["search"].set("west");
    page["state"].set("false");
    service.search.mockRejectedValueOnce(new Error("offline"));
    await page["load"]();
    expect(page["page"]().items).toEqual([]);
    expect(page["error"]()).toContain("Retry");
    expect(page["search"]()).toBe("west");
    expect(service.search).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: "west", isActive: false }),
    );
  });
  it("refreshes every type count after a unit is saved", async () => {
    const page = component();
    await Promise.resolve();
    vi.clearAllMocks();
    service.search.mockResolvedValue({
      items: [country],
      skip: 0,
      take: 20,
      totalCount: 1,
    });

    await page["refreshSaved"](country);
    await Promise.resolve();

    expect(service.search).toHaveBeenCalledWith(
      expect.objectContaining({ unitType: "Country", skip: 0, take: 1 }),
    );
    expect(service.search).toHaveBeenCalledWith(
      expect.objectContaining({ unitType: "State", skip: 0, take: 1 }),
    );
  });
});

describe("Organization editing", () => {
  const service = {
    search: vi.fn(),
    get: vi.fn(),
    create: vi.fn(),
    createChild: vi.fn(),
    update: vi.fn(),
    setActive: vi.fn(),
  };
  beforeEach(() => {
    vi.resetAllMocks();
    service.search.mockResolvedValue({
      items: [root],
      skip: 0,
      take: 20,
      totalCount: 1,
    });
    service.get.mockResolvedValue(root);
    service.create.mockResolvedValue(root);
    service.createChild.mockResolvedValue(country);
    service.update.mockResolvedValue(root);
    service.setActive.mockResolvedValue({ ...root, isActive: false });
    TestBed.configureTestingModule({
      imports: [OrganizationManagementComponent, OrganizationEditorComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        { provide: RUNTIME_CONFIG, useValue: testRuntimeConfig },
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(convertToParamMap({})),
            snapshot: {
              data: { mode: "create" },
              paramMap: convertToParamMap({}),
            },
          },
        },
        {
          provide: ConfirmationService,
          useValue: { ask: vi.fn().mockResolvedValue(true) },
        },
        { provide: OrganizationService, useValue: service },
      ],
    });
    vi.spyOn(TestBed.inject(Router), "navigate").mockResolvedValue(true);
  });
  function component() {
    return TestBed.createComponent(OrganizationEditorComponent)
      .componentInstance;
  }
  it("shows address fields only for organizations and branches", async () => {
    const fixture = TestBed.createComponent(OrganizationEditorComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.componentInstance["openEdit"](root);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(
      element.querySelector<HTMLInputElement>(
        '.editor input[aria-label="Unit code"]',
      )?.value,
    ).toBe("ORG");
    expect(
      element.querySelector<HTMLInputElement>(
        '.editor input[aria-label="City"]',
      )?.value,
    ).toBe("Chennai");
    expect(
      element.querySelector('.editor input[aria-label="Parent"]'),
    ).toBeNull();
    expect(element.querySelector(".editor")?.textContent).toContain(
      "Organization, type and parent stay unchanged",
    );

    fixture.componentInstance["openCreate"]("Country", root);
    fixture.detectChanges();
    expect(
      element.querySelector<HTMLInputElement>(
        '.editor input[aria-label="City"]',
      ),
    ).toBeNull();

    fixture.componentInstance["openCreate"]("Branch", {
      ...country,
      unitType: "State",
    });
    fixture.detectChanges();
    expect(
      element.querySelector<HTMLInputElement>(
        '.editor input[aria-label="City"]',
      ),
    ).not.toBeNull();
    fixture.destroy();
  });
  it("snapshots edit identity/version and sends explicit nulls to clear optional fields", async () => {
    const page = component();
    page["openEdit"](root);
    service.get.mockResolvedValue(country);
    page["editField"]("description", "");
    page["editAddress"]("city", "");
    await page["save"]();
    expect(service.update).toHaveBeenCalledWith(
      root,
      expect.objectContaining({
        rowVersion: root.rowVersion,
        description: null,
        address: expect.objectContaining({ city: null }),
      }),
    );
    expect(page["completed"]()).toBe(true);
  });
  it("creates each typed child only under a compatible active parent", async () => {
    const page = component();
    page["openCreate"]("Country", root);
    page["editField"]("unitCode", "NEW");
    page["editField"]("unitName", "New child");
    await page["save"]();
    expect(service.createChild).toHaveBeenCalledWith(
      10,
      expect.objectContaining({
        parentOrganizationUnitId: 100,
        unitType: "Country",
        unitCode: "NEW",
      }),
    );
    page["editor"].set(null);
    page["openCreate"]("Team", root);
    expect(page["editor"]()).toBeNull();
    page["openCreate"]("Country", { ...root, isActive: false });
    expect(page["editor"]()).toBeNull();
  });
  it("keeps drafts for errors and requires explicit reload after concurrency conflicts", async () => {
    const page = component();
    page["openEdit"](root);
    page["editField"]("unitName", "Draft name");
    service.update.mockRejectedValueOnce({
      error: {
        code: "organization_concurrency_conflict",
        title: "Changed elsewhere",
      },
    });
    await page["save"]();
    expect(page["conflict"]()).toBe(true);
    expect(page["draft"]().unitName).toBe("Draft name");
    await page["save"]();
    expect(service.update).toHaveBeenCalledTimes(1);
    service.get.mockResolvedValueOnce({
      ...root,
      unitName: "Latest name",
      rowVersion: "AAAAAAAAAAI=",
    });
    await page["reloadEditor"]();
    expect(page["draft"]().unitName).toBe("Latest name");
    expect(page["conflict"]()).toBe(false);
    await page["save"]();
    expect(service.update).toHaveBeenLastCalledWith(
      expect.objectContaining({ rowVersion: "AAAAAAAAAAI=" }),
      expect.objectContaining({ rowVersion: "AAAAAAAAAAI=" }),
    );
  });
  it("validates names/coordinates before HTTP and prevents duplicate submits", async () => {
    const page = component();
    page["openCreate"]();
    await page["save"]();
    expect(service.create).not.toHaveBeenCalled();
    expect(page["saveError"]()).toContain("required");
    page["editField"]("unitCode", "NEW");
    page["editField"]("unitName", "New org");
    page["editField"]("latitude", "91");
    await page["save"]();
    expect(service.create).not.toHaveBeenCalled();
    expect(page["saveError"]()).toContain("Latitude");
    page["editField"]("latitude", "");
    let resolve!: (unit: OrganizationUnit) => void;
    service.create.mockReturnValueOnce(new Promise((done) => (resolve = done)));
    const first = page["save"]();
    await page["save"]();
    expect(service.create).toHaveBeenCalledTimes(1);
    resolve(root);
    await first;
  });
  it("loads route targets, rejects invalid relationships and ignores superseded responses", async () => {
    const page = component();
    const params = (id: string, type = "Country") =>
      convertToParamMap({ organizationId: "10", unitId: id, unitType: type });
    await page["loadEditor"](params("0"));
    expect(service.get).not.toHaveBeenCalled();
    expect(page["loadError"]()).toContain("invalid");
    await page["loadEditor"](params("100", "Team"));
    expect(page["editor"]()).toBeNull();
    expect(page["loadError"]()).toContain("parent");
    service.get.mockResolvedValueOnce({ ...root, organizationId: 11 });
    await page["loadEditor"](params("100"));
    expect(page["loadError"]()).toContain("does not belong");
    service.get.mockRejectedValueOnce(new Error("Offline"));
    await page["loadEditor"](params("100"));
    expect(page["loadError"]()).toBe("Offline");
    await page["loadEditor"](params("100"));
    expect(page["editor"]()?.type).toBe("Country");
    let resolve!: (unit: OrganizationUnit) => void;
    service.get.mockReturnValueOnce(new Promise((done) => (resolve = done)));
    const old = page["loadEditor"](params("100"));
    await page["loadEditor"](convertToParamMap({}));
    resolve(root);
    await old;
    expect(page["editor"]()?.type).toBe("Organization");
    expect(page["loading"]()).toBe(false);
  });
  it("guards dirty drafts and in-flight saves, then allows departure without duplicate writes", async () => {
    const page = component();
    page["openCreate"]();
    expect(await page.canLeave()).toBe(true);
    page["editField"]("unitCode", "NEW");
    page["editField"]("unitName", "New organization");
    vi.mocked(TestBed.inject(ConfirmationService).ask).mockResolvedValueOnce(
      false,
    );
    expect(await page.canLeave()).toBe(false);
    expect(await page.canLeave()).toBe(true);
    let resolve!: (unit: OrganizationUnit) => void;
    service.create.mockReturnValueOnce(new Promise((done) => (resolve = done)));
    const pending = page["save"]();
    expect(await page.canLeave()).toBe(false);
    resolve(root);
    await pending;
    expect(await page.canLeave()).toBe(true);
    await page["save"]();
    expect(service.create).toHaveBeenCalledTimes(1);
    expect(TestBed.inject(Router).navigate).toHaveBeenCalledWith(
      ["/organizations"],
      { replaceUrl: true },
    );
  });
  it("confirms active changes and exposes explicit stale-state recovery", async () => {
    const page = TestBed.createComponent(
      OrganizationManagementComponent,
    ).componentInstance;
    await page["select"](root);
    vi.mocked(TestBed.inject(ConfirmationService).ask).mockResolvedValueOnce(
      false,
    );
    await page["toggleActive"]();
    expect(service.setActive).not.toHaveBeenCalled();
    service.setActive.mockRejectedValueOnce({
      error: {
        code: "organization_concurrency_conflict",
        title: "Changed elsewhere",
      },
    });
    await page["toggleActive"]();
    expect(service.setActive).toHaveBeenCalledWith(root, false);
    expect(page["hasStateConflict"]()).toBe(true);
    await page["reloadState"]();
    expect(page["hasStateConflict"]()).toBe(false);
    await page["toggleActive"]();
    expect(page["notice"]()).toContain("Descendants are unchanged");
  });
});

describe("Organization drill-down", () => {
  const service = { search: vi.fn(), get: vi.fn(), setActive: vi.fn() };
  const region: OrganizationUnit = {
    ...country,
    organizationUnitId: 102,
    parentOrganizationUnitId: 101,
    unitType: "Region",
    unitCode: "IN-S",
    unitName: "South",
    hierarchyPath: "/100/101/102/",
  };

  function listOf(
    items: readonly OrganizationUnit[],
    totalCount = items.length,
  ) {
    return { items, skip: 0, take: 20, totalCount };
  }

  beforeEach(() => {
    vi.resetAllMocks();
    service.search.mockResolvedValue(listOf([root]));
    service.get.mockImplementation((_org: number, id: number) =>
      Promise.resolve(
        [root, country, region].find((unit) => unit.organizationUnitId === id)!,
      ),
    );
    TestBed.configureTestingModule({
      imports: [OrganizationManagementComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        { provide: RUNTIME_CONFIG, useValue: testRuntimeConfig },
        {
          provide: ConfirmationService,
          useValue: { ask: vi.fn().mockResolvedValue(true) },
        },
        { provide: OrganizationService, useValue: service },
      ],
    });
  });

  async function page() {
    const component = TestBed.createComponent(
      OrganizationManagementComponent,
    ).componentInstance;
    await component["load"]();
    return component;
  }

  it("loads a branch once and nests it under the row that opened it", async () => {
    const component = await page();
    service.search.mockResolvedValueOnce(listOf([country]));

    await component["toggle"](root);

    expect(service.search).toHaveBeenLastCalledWith({
      organizationId: 10,
      parentOrganizationUnitId: 100,
      skip: 0,
      /* The server refuses a larger page for this query. */
      take: 50,
    });
    expect(
      component["rows"]().map((row) => [row.unit.unitName, row.depth]),
    ).toEqual([
      ["Example organization", 0],
      ["India", 1],
    ]);

    /* Collapsing and reopening reuses the branch rather than refetching it. */
    const calls = service.search.mock.calls.length;
    await component["toggle"](root);
    expect(component["rows"]()).toHaveLength(1);
    await component["toggle"](root);
    expect(service.search).toHaveBeenCalledTimes(calls);
  });

  it("reports the children a branch page could not show", async () => {
    const component = await page();
    service.search.mockResolvedValueOnce(listOf([country], 140));

    await component["toggle"](root);

    expect(component["rows"]()[0].hiddenChildren).toBe(139);
  });

  it("closes the row again and keeps the list usable when a branch fails", async () => {
    const component = await page();
    service.search.mockRejectedValueOnce(new Error("offline"));

    await component["toggle"](root);

    expect(component["rows"]()).toHaveLength(1);
    expect(component["rows"]()[0].expanded).toBe(false);
    expect(component["error"]()).toContain("Children of Example organization");
  });

  it("names every step back up the browsing path", async () => {
    const component = await page();
    await component["select"](region);

    component["browse"](region);
    await Promise.resolve();

    expect(component["trail"]().map((unit) => unit.unitName)).toEqual([
      "Example organization",
      "India",
      "South",
    ]);
  });

  it("says what the other administrator changed instead of repeating the error", async () => {
    const component = await page();
    await component["select"](root);
    service.setActive.mockRejectedValueOnce({
      error: { code: "organization_concurrency_conflict" },
    });
    service.get.mockResolvedValueOnce({
      ...root,
      isActive: false,
      unitName: "Head office",
    });

    await component["toggleActive"]();

    expect(component["hasStateConflict"]()).toBe(true);
    expect(component["conflictMessage"]()).toContain("It is now inactive.");
    expect(component["conflictMessage"]()).toContain(
      'The name is now "Head office".',
    );
    /* The raw server message is not shown as a page error alongside the prompt. */
    expect(component["error"]()).toBeNull();
  });

  it("still offers reconciliation when the current version cannot be fetched", async () => {
    const component = await page();
    await component["select"](root);
    service.setActive.mockRejectedValueOnce({
      error: { code: "organization_concurrency_conflict" },
    });
    service.get.mockRejectedValueOnce(new Error("offline"));

    await component["toggleActive"]();

    expect(component["hasStateConflict"]()).toBe(true);
    expect(component["conflictMessage"]()).toContain(
      "Nothing on this screen was changed",
    );
  });
});
