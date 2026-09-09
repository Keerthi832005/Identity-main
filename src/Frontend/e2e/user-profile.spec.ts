import { expect, test } from "@playwright/test";
import type {
  CreateUserRequest,
  UpdateUserProfileRequest,
  UserSummary,
} from "../src/app/features/user-access/user-access.models";

for (const theme of ["light", "dark"] as const) {
  test(`user profiles and department/team mappings create, edit, clear and report errors in ${theme} theme`, async ({
    page,
  }, testInfo) => {
    await page.emulateMedia({ colorScheme: theme });
    const pageErrors: string[] = [];
    page.on("pageerror", (error) => pageErrors.push(error.message));
    const manager: UserSummary = {
      userId: 42,
      employeeCode: "MGR-42",
      displayName: "Review Manager",
      email: "manager@example.com",
      managerUserId: null,
      managerDisplayName: null,
      isActive: true,
      securityVersion: 1,
      lastLoginAt: null,
      lockoutEndAt: null,
      createdAt: "2026-08-30T00:00:00Z",
      updatedAt: null,
    };
    const users: UserSummary[] = [manager];
    const updates: UpdateUserProfileRequest[] = [];
    const managerSearches: string[] = [];
    const unitSearches: string[] = [];
    let rejectUpdate = false;
    // API fixtures stay inside this browser test; they never reach an IAM database.
    await page.route("**/identity/api/v1/auth/browser/refresh", (route) =>
      route.fulfill({ status: 401, json: {} }),
    );
    await page.route("**/identity/api/v1/auth/browser/login", (route) => {
      const payload = Buffer.from(
        JSON.stringify({
          sub: "42",
          employee_code: "MGR-42",
          capability: ["iam.admin"],
          authorization_version: "3",
          exp: Math.floor(Date.now() / 1000) + 600,
        }),
      ).toString("base64url");
      return route.fulfill({
        json: {
          succeeded: true,
          accessToken: `header.${payload}.signature`,
          authorizationVersion: 3,
        },
      });
    });
    await page.route("**/identity/api/v1/admin/applications?*", (route) =>
      route.fulfill({ json: { skip: 0, take: 50, totalCount: 0, items: [] } }),
    );
    await page.route(
      "**/identity/api/v1/admin/organization-units?*",
      (route) => {
        const query = new URL(route.request().url()).searchParams;
        const type = query.get("unitType");
        unitSearches.push(`${type}:${query.get("search") ?? ""}`);
        expect(query.get("isActive")).toBe("true");
        const items =
          type === "Department"
            ? [
                {
                  organizationUnitId: 11,
                  unitName: "Assembly",
                  unitCode: "ASSEMBLY",
                },
                {
                  organizationUnitId: 21,
                  unitName: "Maintenance",
                  unitCode: "MAINT",
                },
              ]
            : query.get("parentOrganizationUnitId") === "11"
              ? [
                  {
                    organizationUnitId: 12,
                    unitName: "Day shift",
                    unitCode: "DAY",
                  },
                ]
              : [
                  {
                    organizationUnitId: 22,
                    unitName: "Repairs",
                    unitCode: "REPAIR",
                  },
                ];
        return route.fulfill({
          json: { skip: 0, take: 50, totalCount: items.length, items },
        });
      },
    );
    await page.route("**/identity/api/v1/admin/users?*", (route) => {
      const search =
        new URL(route.request().url()).searchParams.get("search") ?? "";
      managerSearches.push(search);
      return route.fulfill({
        json: { skip: 0, take: 50, totalCount: users.length, items: users },
      });
    });
    await page.route("**/identity/api/v1/admin/users/*/access", (route) => {
      const id = Number(route.request().url().split("/").at(-2));
      return route.fulfill({
        json: {
          user: users.find((user) => user.userId === id),
          applications: [],
          roleAssignments: [],
          overrides: [],
          evaluatedAt: "2026-08-30T00:00:00Z",
        },
      });
    });
    await page.route("**/identity/api/v1/admin/users", (route) => {
      expect(route.request().method()).toBe("POST");
      const request = route.request().postDataJSON() as CreateUserRequest;
      expect(request).toEqual({
        employeeCode: "EMP-7",
        displayName: "Review Employee",
        email: "employee@example.com",
        managerUserId: 42,
        organizationMapping: { departmentId: 11, teamId: 12 },
      });
      users.push({
        ...manager,
        ...request,
        userId: 7,
        managerDisplayName: manager.displayName,
        departmentId: 11,
        departmentName: "Assembly",
        teamId: 12,
        teamName: "Day shift",
      });
      return route.fulfill({
        status: 201,
        json: { resourceType: "User", resourceId: 7, userId: 7 },
      });
    });
    await page.route("**/identity/api/v1/admin/users/7/profile", (route) => {
      expect(route.request().method()).toBe("PUT");
      if (rejectUpdate)
        return route.fulfill({
          status: 400,
          json: {
            detail: "Manager assignment must not contain a reporting cycle.",
          },
        });
      const request = route
        .request()
        .postDataJSON() as UpdateUserProfileRequest;
      updates.push(request);
      users[1] = {
        ...users[1],
        ...request,
        managerDisplayName:
          request.managerUserId === 42 ? manager.displayName : null,
        departmentId: request.organizationMapping?.departmentId ?? null,
        departmentName:
          request.organizationMapping?.departmentId === 21
            ? "Maintenance"
            : null,
        teamId: request.organizationMapping?.teamId ?? null,
        teamName: request.organizationMapping?.teamId === 22 ? "Repairs" : null,
      };
      return route.fulfill({
        json: { resourceType: "User", resourceId: 7, userId: 7 },
      });
    });

    await page.goto("/users");
    await page.getByLabel("Employee code").fill("MGR-42");
    await page
      .getByRole("textbox", { name: "Password", exact: true })
      .fill("browser-test-only-password");
    await page.getByRole("button", { name: "Sign in securely" }).click();
    await expect(
      page.getByRole("heading", { name: "Users & access" }),
    ).toBeVisible();
    await expect(page.locator("html")).toHaveAttribute("data-theme", theme);
    // Playwright aborts /favicon.ico when routing is enabled. Verify the wordmark
    // here; the favicon is checked separately in the non-intercepted browser.
    await expect
      .poll(() =>
        page
          .locator(".brand__logo")
          .evaluate((image: HTMLImageElement) => image.naturalWidth),
      )
      .toBeGreaterThan(0);

    await page.getByRole("button", { name: "New user", exact: true }).click();
    const dialog = page.locator("form.inline-editor");
    await expect(
      dialog.getByLabel("Find manager", { exact: true }),
    ).toHaveCount(0);
    await expect(
      dialog.getByLabel("Find department", { exact: true }),
    ).toHaveCount(0);
    await expect(dialog.getByLabel("Find team", { exact: true })).toHaveCount(
      0,
    );
    await expect(
      page.getByRole("button", { name: "Back to users & access" }),
    ).toBeVisible();
    await expect(page.locator(".access-layout")).toBeHidden();
    await dialog.getByLabel("Employee code", { exact: true }).fill("EMP-7");
    await dialog
      .getByLabel("Display name", { exact: true })
      .fill("Review Employee");
    await dialog
      .getByLabel("Email", { exact: true })
      .fill("employee@example.com");
    await dialog
      .locator("app-lookup")
      .filter({ has: page.getByLabel("Manager", { exact: true }) })
      .locator(".dx-dropdowneditor-button")
      .click();
    await expect(
      page.getByRole("option", {
        name: "Review Manager (MGR-42)",
        exact: true,
      }),
    ).toBeVisible();
    await dialog
      .getByLabel("Manager", { exact: true })
      .fill("manager@example.com");
    await expect
      .poll(() => managerSearches.includes("manager@example.com"))
      .toBe(true);
    await page
      .getByRole("option", { name: "Review Manager (MGR-42)", exact: true })
      .click();
    await expect(dialog.getByLabel("Team", { exact: true })).toBeDisabled();
    await dialog
      .locator("app-lookup")
      .filter({ has: page.getByLabel("Department", { exact: true }) })
      .locator(".dx-dropdowneditor-button")
      .click();
    await expect(
      page.getByRole("option", { name: "Assembly (ASSEMBLY)", exact: true }),
    ).toBeVisible();
    await dialog.getByLabel("Department", { exact: true }).fill("ASSEMBLY");
    await expect
      .poll(() => unitSearches.includes("Department:ASSEMBLY"))
      .toBe(true);
    await page
      .getByRole("option", { name: "Assembly (ASSEMBLY)", exact: true })
      .click();
    await dialog.getByLabel("Team", { exact: true }).click();
    await dialog.getByLabel("Team", { exact: true }).fill("DAY");
    await expect.poll(() => unitSearches.includes("Team:DAY")).toBe(true);
    await page
      .getByRole("option", { name: "Day shift (DAY)", exact: true })
      .click();
    await dialog.getByRole("button", { name: "Confirm", exact: true }).click();
    await expect(dialog).toHaveCount(0);
    await expect(
      page.getByText("Email: employee@example.com", { exact: true }),
    ).toBeVisible();
    await expect(
      page.getByText("Manager: Review Manager", { exact: true }),
    ).toBeVisible();

    await page
      .getByRole("button", { name: "Edit profile", exact: true })
      .click();
    await expect(dialog.getByLabel("Email", { exact: true })).toHaveValue(
      "employee@example.com",
    );
    await expect(dialog.getByLabel("Manager", { exact: true })).toHaveValue(
      /^Review Manager/,
    );
    await expect(dialog.getByLabel("Department", { exact: true })).toHaveValue(
      /^Assembly/,
    );
    await expect(dialog.getByLabel("Team", { exact: true })).toHaveValue(
      /^Day shift/,
    );
    await expect(
      dialog.getByRole("button", { name: "Confirm", exact: true }),
    ).toHaveCSS("color", "rgb(255, 255, 255)");
    await page.screenshot({
      path: testInfo.outputPath(`profile-${theme}.png`),
      fullPage: true,
      animations: "disabled",
    });
    await page.setViewportSize({ width: 390, height: 844 });
    await expect(page.locator(".sidebar")).not.toBeInViewport();
    await page.evaluate(() => window.scrollTo(0, 0));
    await expect(dialog.getByLabel("Email", { exact: true })).toBeVisible();
    const bounds = await dialog.boundingBox();
    expect(bounds).not.toBeNull();
    expect(bounds!.x).toBeGreaterThanOrEqual(0);
    expect(bounds!.x + bounds!.width).toBeLessThanOrEqual(390);
    await page.screenshot({
      path: testInfo.outputPath(`profile-${theme}-mobile.png`),
      fullPage: true,
      animations: "disabled",
    });
    await page.setViewportSize({ width: 1280, height: 720 });
    await dialog
      .getByLabel("Display name", { exact: true })
      .fill("Updated Employee");
    await dialog.getByLabel("Email", { exact: true }).fill("");
    await dialog.getByLabel("Manager", { exact: true }).click();
    await expect(
      page.getByRole("option", {
        name: "Review Employee (EMP-7)",
        exact: true,
      }),
    ).toHaveCount(0);
    await page.getByRole("option", { name: "No manager", exact: true }).click();
    await dialog.getByLabel("Department", { exact: true }).click();
    await page
      .getByRole("option", { name: "Maintenance (MAINT)", exact: true })
      .click();
    await expect(dialog.getByLabel("Team", { exact: true })).toHaveValue(
      "No team",
    );
    await dialog.getByLabel("Team", { exact: true }).click();
    await expect(
      page.getByRole("option", { name: "Day shift (DAY)", exact: true }),
    ).toHaveCount(0);
    await page
      .getByRole("option", { name: "Repairs (REPAIR)", exact: true })
      .click();
    await dialog.getByRole("button", { name: "Confirm", exact: true }).click();
    await expect(dialog).toHaveCount(0);
    expect(updates).toEqual([
      {
        displayName: "Updated Employee",
        email: null,
        managerUserId: null,
        organizationMapping: { departmentId: 21, teamId: 22 },
      },
    ]);
    await expect(
      page.getByText("Email: Not provided", { exact: true }),
    ).toBeVisible();
    await expect(
      page.getByText("Manager: Not assigned", { exact: true }),
    ).toBeVisible();
    await expect(
      page.getByText("User profile saved. Permissions are unchanged.", {
        exact: false,
      }),
    ).toBeVisible();

    await expect(
      page.getByText("Department: Maintenance", { exact: true }),
    ).toBeVisible();
    await expect(
      page.getByText("Team: Repairs", { exact: true }),
    ).toBeVisible();
    await page
      .getByRole("button", { name: "Edit profile", exact: true })
      .click();
    await dialog.getByLabel("Department", { exact: true }).click();
    await page
      .getByRole("option", { name: "No department", exact: true })
      .click();
    await expect(dialog.getByLabel("Team", { exact: true })).toBeDisabled();
    await dialog.getByRole("button", { name: "Confirm", exact: true }).click();
    await expect(dialog).toHaveCount(0);
    expect(updates.at(-1)?.organizationMapping).toEqual({
      departmentId: null,
      teamId: null,
    });
    await expect(
      page.getByText("Department: Not assigned", { exact: true }),
    ).toBeVisible();
    await expect(
      page.getByText("Team: Not assigned", { exact: true }),
    ).toBeVisible();

    await page
      .getByRole("button", { name: "Edit profile", exact: true })
      .click();
    await dialog
      .getByLabel("Display name", { exact: true })
      .fill("Unsaved employee");
    await page.getByRole("button", { name: "Back to users & access" }).click();
    await page
      .getByRole("alertdialog")
      .getByRole("button", { name: "Cancel", exact: true })
      .click();
    await expect(
      dialog.getByLabel("Display name", { exact: true }),
    ).toHaveValue("Unsaved employee");
    await page.getByRole("button", { name: "Back to users & access" }).click();
    await page
      .getByRole("alertdialog")
      .getByRole("button", { name: "Discard changes", exact: true })
      .click();
    await expect(dialog).toHaveCount(0);
    await expect(page.locator(".access-layout")).toBeVisible();
    rejectUpdate = true;
    await page
      .getByRole("button", { name: "Edit profile", exact: true })
      .click();
    await dialog.getByRole("button", { name: "Confirm", exact: true }).click();
    await expect(dialog.getByRole("alert")).toHaveText(
      "Manager assignment must not contain a reporting cycle.",
    );
    await expect(
      dialog.getByRole("button", { name: "Confirm", exact: true }),
    ).toBeEnabled();
    expect(pageErrors).toEqual([]);
  });
}
