import { expect, test } from "@playwright/test";
import type { RoleSummary } from "../src/app/features/user-access/user-access.models";

for (const theme of ["light", "dark"] as const) {
  test(`role cards keep compact badges and wrap safely in ${theme}`, async ({
    page,
  }, testInfo) => {
    test.setTimeout(90_000);
    await page.emulateMedia({ colorScheme: theme });
    const errors: string[] = [];
    const unexpected: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    const user = {
      userId: 7,
      employeeCode: "TEST-7",
      displayName: "Layout Reviewer",
      isActive: true,
      securityVersion: 1,
    };
    const application = {
      applicationId: 10,
      applicationCode: "identity",
      applicationName: "Identity Administration",
      isActive: true,
    };
    const roles: RoleSummary[] = [
      {
        roleId: 1,
        applicationId: 10,
        roleName: "Identity Administrator",
        roleCode: "identity-administrator",
        description: "Bootstrap administration role.",
        isActive: true,
        isSystem: true,
      },
      {
        roleId: 2,
        applicationId: 10,
        roleName: "Regional Manufacturing Quality Administrator",
        roleCode: "regional-manufacturing-quality-administrator".repeat(2),
        description:
          "A longer description must wrap without stretching the status badge.",
        isActive: false,
        isSystem: false,
      },
      {
        roleId: 3,
        applicationId: 10,
        roleName: "Supervisor",
        roleCode: "supervisor",
        description: null,
        isActive: true,
        isSystem: false,
      },
    ];
    let roleCount = 1;
    // Browser-only fixtures: no IAM account, permission or database is changed.
    await page.route("**/identity/**", (route) => {
      const path = new URL(route.request().url()).pathname;
      if (path.endsWith("/auth/browser/refresh")) {
        const payload = Buffer.from(
          JSON.stringify({
            sub: "42",
            employee_code: "TEST-ADMIN",
            capability: ["iam.admin"],
            authorization_version: "1",
            exp: Math.floor(Date.now() / 1000) + 600,
          }),
        ).toString("base64url");
        return route.fulfill({
          json: {
            succeeded: true,
            accessToken: `header.${payload}.signature`,
            authorizationVersion: 1,
          },
        });
      }
      if (route.request().method() === "GET") {
        if (path.endsWith("/users"))
          return route.fulfill({ json: { items: [user], totalCount: 1 } });
        if (path.endsWith("/applications"))
          return route.fulfill({
            json: { items: [application], totalCount: 1 },
          });
        if (path.endsWith("/users/7/access"))
          return route.fulfill({
            json: {
              user,
              applications: [],
              roleAssignments: [],
              overrides: [],
            },
          });
        if (path.endsWith("/applications/10/access"))
          return route.fulfill({
            json: {
              applicationId: 10,
              roles: roles.slice(0, roleCount),
              rolePermissions: roles.slice(0, roleCount).map((role) => ({
                rolePermissionId: role.roleId,
                roleId: role.roleId,
                capabilityCode:
                  role.roleId === 1
                    ? "iam.admin"
                    : "quality.inspect.extended-capability-name".repeat(2),
                revokedAt: null,
              })),
              capabilities: [],
            },
          });
      }
      unexpected.push(`${route.request().method()} ${path}`);
      return route.fulfill({ status: 404, json: {} });
    });

    for (roleCount of [1, 3]) {
      await page.goto("/users/7");
      await page.getByRole("tab", { name: "Roles", exact: true }).click();
      const catalog = page.locator(".role-catalog");
      const cards = catalog.locator(".role-grid > article");
      await expect(cards).toHaveCount(roleCount);
      await expect(page.locator("html")).toHaveAttribute("data-theme", theme);
      for (const width of [1920, 1280, 768, 390]) {
        await page.setViewportSize({ width, height: 1000 });
        await catalog.scrollIntoViewIfNeeded();
        const layout = await cards.evaluateAll((elements) =>
          elements.map((card) => {
            const bounds = card.getBoundingClientRect();
            const status = card
              .querySelector(".state")!
              .getBoundingClientRect();
            const identity = card.querySelector("div")!.getBoundingClientRect();
            return {
              badgeHeight: status.height,
              badgeTop: status.top,
              identityTop: identity.top,
              gap: status.left - identity.right,
              fitsCard: status.right <= bounds.right,
              overflow: card.scrollWidth > card.clientWidth,
            };
          }),
        );
        for (const card of layout) {
          expect(card.badgeHeight).toBeLessThan(36);
          expect(Math.abs(card.badgeTop - card.identityTop)).toBeLessThan(2);
          expect(card.gap).toBeGreaterThanOrEqual(11);
          expect(card.fitsCard).toBe(true);
          expect(card.overflow).toBe(false);
        }
        expect(
          await catalog.evaluate((el) => el.scrollWidth <= el.clientWidth),
        ).toBe(true);
        const bounds = await catalog.boundingBox();
        expect(bounds!.x).toBeGreaterThanOrEqual(0);
        expect(bounds!.x + bounds!.width).toBeLessThanOrEqual(width);
        await expect(
          catalog.getByRole("button", { name: "New role", exact: true }),
        ).toBeVisible();
        await expect(
          catalog.getByRole("button", {
            name: "Grant capability",
            exact: true,
          }),
        ).toBeVisible();
        await catalog.screenshot({
          path: testInfo.outputPath(`roles-${roleCount}-${theme}-${width}.png`),
          animations: "disabled",
        });
      }
    }
    expect(errors).toEqual([]);
    expect(unexpected).toEqual([]);
  });
}
