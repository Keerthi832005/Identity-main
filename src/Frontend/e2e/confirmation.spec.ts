import { expect, test } from "@playwright/test";

for (const theme of ["light", "dark"] as const) {
  for (const mobile of [false, true]) {
    test(`themed confirmation protects user revocation in ${theme} ${mobile ? "mobile" : "desktop"}`, async ({
      page,
    }, testInfo) => {
      await page.emulateMedia({ colorScheme: theme });
      await page.setViewportSize(
        mobile ? { width: 390, height: 844 } : { width: 1440, height: 900 },
      );
      const errors: string[] = [];
      const nativeDialogs: string[] = [];
      const unexpected: string[] = [];
      const writes: string[] = [];
      page.on("pageerror", (error) => errors.push(error.message));
      page.on("dialog", async (dialog) => {
        nativeDialogs.push(dialog.message());
        await dialog.dismiss();
      });
      const user = {
        userId: 7,
        employeeCode: "EMP-7",
        displayName: "Review Employee",
        isActive: true,
        securityVersion: 1,
        lastLoginAt: null,
        lockoutEndAt: null,
        createdAt: "2026-08-30T00:00:00Z",
        updatedAt: null,
      };
      // All API requests are isolated fixtures. Nothing reaches a real account or IAM database.
      await page.route("**/identity/**", (route) => {
        unexpected.push(route.request().url());
        return route.fulfill({ status: 404, json: {} });
      });
      await page.route("**/identity/api/v1/auth/browser/refresh", (route) => {
        const payload = Buffer.from(
          JSON.stringify({
            sub: "42",
            employee_code: "ADMIN001",
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
      await page.route("**/identity/api/v1/admin/users?*", (route) =>
        route.fulfill({
          json: { items: [user], totalCount: 1, skip: 0, take: 50 },
        }),
      );
      await page.route("**/identity/api/v1/admin/applications?*", (route) =>
        route.fulfill({
          json: { items: [], totalCount: 0, skip: 0, take: 50 },
        }),
      );
      await page.route("**/identity/api/v1/admin/users/7/access", (route) =>
        route.fulfill({
          json: {
            user,
            applications: [],
            roleAssignments: [],
            overrides: [],
            evaluatedAt: "2026-08-30T00:00:00Z",
          },
        }),
      );
      await page.route("**/identity/api/v1/admin/resources/User/7", (route) => {
        expect(route.request().method()).toBe("DELETE");
        writes.push(route.request().url());
        user.isActive = false;
        return route.fulfill({ json: { resourceId: 7 } });
      });

      await page.goto("/users/7");
      const revoke = page.getByRole("button", {
        name: "Revoke user",
        exact: true,
      });
      await expect(revoke).toBeVisible();
      const dialog = page.getByRole("alertdialog", { name: "Revoke access?" });
      await revoke.click();
      await expect(dialog).toBeVisible();
      await expect(dialog).toHaveAttribute("aria-modal", "true");
      await expect(dialog).toContainText(
        "Revoke Review Employee? This access change is audited.",
      );
      const cancel = dialog.getByRole("button", {
        name: "Cancel",
        exact: true,
      });
      const approve = dialog.getByRole("button", {
        name: "Revoke",
        exact: true,
      });
      await expect(cancel).toBeFocused();
      await page.keyboard.press("Tab");
      await expect(approve).toBeFocused();
      await page.keyboard.press("Tab");
      await expect(cancel).toBeFocused();
      await page.keyboard.press("Shift+Tab");
      await expect(approve).toBeFocused();
      await page.keyboard.press("Escape");
      await expect(dialog).toHaveCount(0);
      await expect(revoke).toBeFocused();
      expect(writes).toEqual([]);

      await revoke.click();
      await cancel.click();
      await expect(dialog).toHaveCount(0);
      expect(writes).toEqual([]);
      await revoke.click();
      await expect(dialog).toBeVisible();
      await page.mouse.click(5, 5);
      await expect(dialog).toHaveCount(0);
      expect(writes).toEqual([]);

      await revoke.click();
      await expect(cancel).toBeFocused();
      await expect(page.locator("html")).toHaveAttribute("data-theme", theme);
      const bounds = await dialog.boundingBox();
      const viewport = page.viewportSize()!;
      expect(bounds).not.toBeNull();
      expect(bounds!.x).toBeGreaterThanOrEqual(0);
      expect(bounds!.y).toBeGreaterThanOrEqual(0);
      expect(bounds!.x + bounds!.width).toBeLessThanOrEqual(viewport.width);
      expect(bounds!.y + bounds!.height).toBeLessThanOrEqual(viewport.height);
      await expect(cancel).toBeInViewport();
      await expect(approve).toBeInViewport();
      await expect(approve.locator(".dx-button-text")).toHaveCSS(
        "color",
        "rgb(255, 255, 255)",
      );
      const surface = await dialog.evaluate(
        (element) => getComputedStyle(element).backgroundColor,
      );
      expect(surface).not.toBe("rgba(0, 0, 0, 0)");
      await page.screenshot({
        path: testInfo.outputPath(
          `confirmation-${theme}-${mobile ? "mobile" : "desktop"}.png`,
        ),
        fullPage: true,
        animations: "disabled",
      });

      await approve.click();
      await expect(dialog).toHaveCount(0);
      await expect(
        page.getByText("Review Employee revoked.", { exact: true }),
      ).toBeVisible();
      expect(writes).toHaveLength(1);
      await expect(revoke).toHaveCount(0);
      await expect(page.getByText("Inactive", { exact: true })).toBeVisible();
      expect(nativeDialogs).toEqual([]);
      expect(unexpected).toEqual([]);
      expect(errors).toEqual([]);
    });
  }
}
