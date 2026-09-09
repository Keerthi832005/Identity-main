import { expect, test } from "@playwright/test";

for (const touch of [false, true]) {
  test.describe(touch ? "touch select" : "mouse select", () => {
    test.use({
      hasTouch: touch,
      viewport: touch
        ? { width: 390, height: 844 }
        : { width: 1280, height: 800 },
    });

    test("label-wrapped arrow supports selection, reopening and normal dismissal", async ({
      page,
    }, testInfo) => {
      const errors: string[] = [];
      page.on("pageerror", (error) => errors.push(error.message));
      // All data is synthetic. These tests never contact an IAM database.
      await page.route("**/identity/**", (route) =>
        route.fulfill({ status: 404, json: {} }),
      );
      const payload = Buffer.from(
        JSON.stringify({
          sub: "42",
          employee_code: "TEST-ADMIN",
          capability: ["iam.admin"],
          authorization_version: "3",
          exp: Math.floor(Date.now() / 1000) + 600,
        }),
      ).toString("base64url");
      await page.route("**/identity/api/v1/auth/browser/refresh", (route) =>
        route.fulfill({
          json: {
            succeeded: true,
            accessToken: `fixture.${payload}.fixture`,
            authorizationVersion: 3,
          },
        }),
      );
      await page.route(
        "**/identity/api/v1/admin/security/operations?*",
        (route) =>
          route.fulfill({
            json: {
              skip: 0,
              take: 50,
              totalAuditCount: 0,
              audits: [],
              sessions: [],
              generatedAt: "2026-08-31T00:00:00Z",
            },
          }),
      );
      await page.goto("/audit");
      const input = page.getByRole("combobox", { name: "Result", exact: true });
      const arrow = page
        .locator("app-select")
        .filter({ has: input })
        .locator(".dx-dropdowneditor-button");
      const success = page.getByRole("option", {
        name: "Success",
        exact: true,
      });
      for (let attempt = 0; attempt < 2; attempt++) {
        if (touch) await arrow.tap();
        else await arrow.click();
        await expect(success).toBeVisible();
        await page.screenshot({
          path: testInfo.outputPath(`select-open-${attempt}.png`),
          fullPage: true,
          animations: "disabled",
        });
        if (touch) await success.tap();
        else await success.click();
        await expect(input).toHaveValue("Success");
        await expect(success).toBeHidden();
      }
      await arrow.click();
      await input.press("Escape");
      await expect(success).toBeHidden();
      await arrow.click();
      await page
        .getByRole("heading", { name: "Audit & sessions", exact: true })
        .click();
      await expect(success).toBeHidden();
      // Native label focus and keyboard selection must still work.
      await page
        .locator("label")
        .filter({ has: input })
        .click({ position: { x: 5, y: 5 } });
      await expect(input).toBeFocused();
      await input.press("Alt+ArrowDown");
      await expect(success).toBeVisible();
      await input.press("ArrowDown");
      await input.press("Enter");
      await expect(input).toHaveValue("Failure");
      await expect(success).toBeHidden();
      expect(errors).toEqual([]);
    });
  });
}
