import { TestBed } from "@angular/core/testing";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { OrganizationService } from "../organization-management/organization.service";
import { UserOrganizationMappingComponent } from "./user-organization-mapping.component";

describe("User department and team dropdowns", () => {
  const search = vi.fn();
  const get = vi.fn();
  beforeEach(() => {
    search.mockReset().mockResolvedValue({ items: [], totalCount: 0 });
    get.mockReset();
    TestBed.configureTestingModule({
      imports: [UserOrganizationMappingComponent],
      providers: [{ provide: OrganizationService, useValue: { search, get } }],
    });
  });
  const component = () =>
    TestBed.runInInjectionContext(() => new UserOrganizationMappingComponent());
  it("prefills mappings without emitting changes and queries teams only within Department", async () => {
    const page = component();
    page.initialMapping = { departmentId: 11, teamId: 12, branchId: null };
    page.departmentName = "Assembly";
    page.teamName = "Day";
    const emit = vi.spyOn(page.mappingChange, "emit");
    page.ngOnInit();
    await page["searchTeams"]("day");
    expect(page["departmentId"]()).toBe(11);
    expect(page["teamId"]()).toBe(12);
    expect(search).toHaveBeenCalledWith({
      unitType: "Team",
      isActive: true,
      parentOrganizationUnitId: 11,
      search: "day",
      take: 50,
    });
    expect(emit).not.toHaveBeenCalled();
  });
  it("clears Team on Department change or removal", async () => {
    const page = component();
    page.initialMapping = { departmentId: 11, teamId: 12, branchId: null };
    page.ngOnInit();
    const emit = vi.spyOn(page.mappingChange, "emit");
    page["chooseDepartment"](21);
    expect(emit).toHaveBeenLastCalledWith({
      departmentId: 21,
      teamId: null,
      branchId: null,
    });
    page["chooseTeam"](22);
    expect(emit).toHaveBeenLastCalledWith({
      departmentId: 21,
      teamId: 22,
      branchId: null,
    });
    page["chooseDepartment"](null);
    expect(emit).toHaveBeenLastCalledWith({
      departmentId: null,
      teamId: null,
      branchId: null,
    });
    expect(await page["searchTeams"]("")).toEqual([]);
    page["chooseTeam"](12);
    expect(page["teamId"]()).toBeNull();
  });
  it("discards teams returned for a previous department", async () => {
    let finish!: (result: unknown) => void;
    search.mockReturnValueOnce(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    const page = component();
    page["departmentId"].set(11);
    const pending = page["searchTeams"]("");
    page["chooseDepartment"](21);
    finish({
      items: [{ organizationUnitId: 12, unitName: "Stale", unitCode: "OLD" }],
    });
    expect(await pending).toEqual([]);
  });
  it("searches the server directly from the department dropdown", async () => {
    const page = component();
    await page["searchDepartments"]("remote-department");
    expect(search).toHaveBeenCalledWith({
      unitType: "Department",
      isActive: true,
      search: "remote-department",
      take: 50,
    });
  });
  it("selects Branch directly and derives State, Region and Country", async () => {
    const branch = {
      organizationId: 1,
      organizationUnitId: 44,
      parentOrganizationUnitId: 33,
      unitType: "Branch",
      unitName: "Chennai",
      unitCode: "CHN",
    };
    search.mockResolvedValueOnce({ items: [branch], totalCount: 1 });
    get
      .mockResolvedValueOnce({
        organizationUnitId: 33,
        parentOrganizationUnitId: 22,
        unitType: "State",
        unitName: "Tamil Nadu",
      })
      .mockResolvedValueOnce({
        organizationUnitId: 22,
        parentOrganizationUnitId: 11,
        unitType: "Region",
        unitName: "South",
      })
      .mockResolvedValueOnce({
        organizationUnitId: 11,
        parentOrganizationUnitId: 1,
        unitType: "Country",
        unitName: "India",
      });
    const page = component();
    page.ngOnInit();
    await page["searchBranches"]("chen");
    await page["chooseBranch"](44);
    expect(search).toHaveBeenCalledWith({
      unitType: "Branch",
      isActive: true,
      search: "chen",
      take: 50,
    });
    expect(page["selectedStateName"]()).toBe("Tamil Nadu");
    expect(page["selectedRegionName"]()).toBe("South");
    expect(page["selectedCountryName"]()).toBe("India");
  });
  it("renders Branch directly and keeps only Team dependent", async () => {
    const fixture = TestBed.createComponent(UserOrganizationMappingComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelectorAll("app-lookup")).toHaveLength(3);
    expect(element.textContent).not.toContain("Find department");
    expect(element.textContent).not.toContain("Find team");
    expect(element.textContent).toContain("do not grant access");
    expect(
      element.querySelector<HTMLInputElement>('input[aria-label="Branch"]')
        ?.disabled,
    ).toBe(false);
    expect(
      element.querySelector<HTMLInputElement>('input[aria-label="Team"]')
        ?.disabled,
    ).toBe(true);
    fixture.destroy();
  });
});
