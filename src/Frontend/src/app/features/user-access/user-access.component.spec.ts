import {
  ActivatedRoute,
  convertToParamMap,
  provideRouter,
} from "@angular/router";
import { of } from "rxjs";
import { ConfirmationService } from "../../shared/ui/confirmation/confirmation.service";
import { TestBed } from "@angular/core/testing";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { UserAccessComponent } from "./user-access.component";
import { UserAccessService } from "./user-access.service";
import { UserSummary } from "./user-access.models";
import { provideHttpClient } from "@angular/common/http";
import { RUNTIME_CONFIG } from "../../core/config/runtime-config";
import { OrganizationService } from "../organization-management/organization.service";

describe("UserAccessComponent profiles", () => {
  const user: UserSummary = {
    userId: 7,
    employeeCode: "EMP-7",
    displayName: "Employee",
    isActive: true,
    securityVersion: 1,
    lastLoginAt: null,
    lockoutEndAt: null,
    createdAt: "2026-08-30T00:00:00Z",
    updatedAt: null,
    email: "employee@example.com",
    managerUserId: 42,
    managerDisplayName: "Manager",
    departmentId: 11,
    departmentName: "Assembly",
    teamId: 12,
    teamName: "Day shift",
  };
  const manager: UserSummary = {
    ...user,
    userId: 42,
    employeeCode: "EMP-42",
    displayName: "Manager",
    managerUserId: null,
  };
  const service = {
    searchUsers: vi.fn(),
    searchApplications: vi.fn(),
    getUserAccess: vi.fn(),
    updateUserProfile: vi.fn(),
    createUser: vi.fn(),
    revoke: vi.fn(),
  };
  beforeEach(() => {
    vi.resetAllMocks();
    service.searchUsers.mockResolvedValue({
      items: [user, manager, { ...manager, userId: 88, isActive: false }],
    });
    service.searchApplications.mockResolvedValue({ items: [] });
    service.getUserAccess.mockResolvedValue({
      user,
      applications: [],
      roleAssignments: [],
      overrides: [],
      evaluatedAt: "",
    });
    service.updateUserProfile.mockResolvedValue({ resourceId: 7 });
    service.createUser.mockResolvedValue({ resourceId: 7 });
    service.revoke.mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      imports: [UserAccessComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(convertToParamMap({ userId: "7" })),
            queryParamMap: of(convertToParamMap({})),
            snapshot: { queryParams: {} },
          },
        },
        {
          provide: ConfirmationService,
          useValue: { ask: vi.fn().mockResolvedValue(true) },
        },
        { provide: UserAccessService, useValue: service },
        {
          provide: OrganizationService,
          useValue: {
            search: vi.fn().mockResolvedValue({ items: [], totalCount: 0 }),
          },
        },
        provideHttpClient(),
        {
          // The bulk toolbar resolves BulkDataService, which reads the deployment base URL.
          provide: RUNTIME_CONFIG,
          useValue: {
            identityBaseUrl: "/identity",
            identityClientId: "identity-admin-web",
            applicationName: "Identity Administration",
            devExtremeLicenseKey: "",
          },
        },
      ],
    });
  });
  function component(): UserAccessComponent {
    const page = TestBed.runInInjectionContext(() => new UserAccessComponent());
    page["directory"].id.set(7);
    return page;
  }
  it("renders profile email and manager controls with their current values", async () => {
    const fixture = TestBed.createComponent(UserAccessComponent);
    fixture.detectChanges();
    await fixture.componentInstance["load"]();
    fixture.componentInstance["open"]("profile");
    await fixture.componentInstance["searchManagers"]("");
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    /* The header states these as one line of facts rather than four labelled rows, so the
       assertions follow the values rather than the old "Label: value" wording. */
    expect(element.textContent).toContain("employee@example.com");
    expect(element.textContent).toContain("Manager: Manager");
    expect(element.textContent).toContain("Assembly");
    expect(element.textContent).toContain("Day shift");
    expect(
      element.querySelector('input[aria-label="Department"]'),
    ).not.toBeNull();
    expect(element.querySelector('input[aria-label="Team"]')).not.toBeNull();
    expect(
      element.querySelector<HTMLInputElement>('input[aria-label="Email"]')
        ?.value,
    ).toBe("employee@example.com");
    expect(element.querySelector('input[aria-label="Manager"]')).not.toBeNull();
    fixture.destroy();
  });
  it("prefills email and manager and excludes self/inactive manager choices", async () => {
    const page = component();
    await page["load"]();
    page["open"]("profile");
    await page["searchManagers"]("");
    expect(page["email"]()).toBe(user.email);
    expect(page["managerUserId"]()).toBe(42);
    expect(
      (await page["searchManagers"]("")).map((value) => value.value),
    ).toEqual([42]);
  });
  it("saves a profile without any application grant and can remove its manager", async () => {
    const page = component();
    await page["load"]();
    page["open"]("profile");
    page["email"].set("");
    page["managerUserId"].set(null);
    await page["submit"]();
    expect(service.updateUserProfile).toHaveBeenCalledWith(7, {
      displayName: "Employee",
      email: null,
      managerUserId: null,
      organizationMapping: { departmentId: 11, teamId: 12, branchId: null },
    });
    expect(page["actionMode"]()).toBeNull();
    expect(page["notice"]()).toContain("Permissions are unchanged");
  });
  it("keeps the profile target stable when a delayed selection changes the detail panel", async () => {
    const page = component();
    await page["load"]();
    page["open"]("profile");
    page["userAccess"].set({ ...page["userAccess"]()!, user: manager });
    await page["searchManagers"]("");
    expect(
      (await page["searchManagers"]("")).map((value) => value.value),
    ).toEqual([42]);
    await page["submit"]();
    expect(service.updateUserProfile).toHaveBeenCalledWith(7, {
      displayName: "Employee",
      email: "employee@example.com",
      managerUserId: 42,
      organizationMapping: { departmentId: 11, teamId: 12, branchId: null },
    });
  });
  it("keeps the editor open when the API rejects a reporting cycle", async () => {
    const page = component();
    await page["load"]();
    page["open"]("profile");
    service.updateUserProfile.mockRejectedValue({
      error: {
        detail: "Manager assignment must not contain a reporting cycle.",
      },
    });
    await page["submit"]();
    expect(page["actionMode"]()).toBe("profile");
    expect(page["error"]()).toContain("reporting cycle");
    expect(page["saving"]()).toBe(false);
  });
  it("shows the API guidance when the final application role cannot be removed", async () => {
    const page = component();
    await page["load"]();
    page["selectedApplicationId"].set(5);
    service.revoke.mockRejectedValue({
      error: {
        detail:
          "At least one application role must remain assigned. Assign another role before removing this role.",
      },
    });

    await page["revoke"]("UserRole", 31, "Field Asset Admin assignment");

    expect(page["error"]()).toBe(
      "At least one application role must remain assigned. Assign another role before removing this role.",
    );
    expect(page["notice"]()).toBeNull();
  });
});
