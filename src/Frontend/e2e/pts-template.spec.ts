import { expect, test } from "@playwright/test";

for (const theme of ["light", "dark"] as const) {
  test(`PTS template navigation and account controls in ${theme}`, async ({
    page,
  }, testInfo) => {
    test.setTimeout(90000);
    await page.emulateMedia({ colorScheme: theme });
    await page.setViewportSize({ width: 1536, height: 900 });
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    const payload = Buffer.from(
      JSON.stringify({
        sub: "42",
        employee_code: "UI-ADMIN",
        display_name: "IAM Administrator",
        capability: ["iam.admin"],
        authorization_version: "7",
        exp: Math.floor(Date.now() / 1000) + 600,
      }),
    ).toString("base64url");
    await page.route("**/identity/api/v1/**", async (route) => {
      const path = new URL(route.request().url()).pathname;
      if (path.endsWith("/auth/browser/refresh"))
        return route.fulfill({ status: 401, json: {} });
      if (path.endsWith("/auth/browser/login"))
        return route.fulfill({
          json: {
            succeeded: true,
            accessToken: `header.${payload}.signature`,
            authorizationVersion: 7,
          },
        });
      if (path.endsWith("/auth/browser/logout"))
        return route.fulfill({ json: { succeeded: true } });
      if (path.endsWith("/admin/dashboard"))
        return route.fulfill({
          json: {
            applicationCount: 0,
            activeApplicationCount: 0,
            userCount: 0,
            activeUserCount: 0,
            activeSessionCount: 1,
            failedAuthenticationCountLast24Hours: 0,
            recentApplications: [],
            recentUsers: [],
            recentAuditEvents: [],
            generatedAt: "2026-08-30T09:00:00Z",
          },
        });
      if (path.endsWith("/admin/security/operations"))
        return route.fulfill({
          json: {
            skip: 0,
            take: 100,
            totalAuditCount: 0,
            audits: [],
            sessions: [],
            generatedAt: "2026-08-30T09:00:00Z",
          },
        });
      if (
        [
          "/admin/users",
          "/admin/applications",
          "/admin/organization-units",
        ].some((endpoint) => path.endsWith(endpoint))
      )
        return route.fulfill({
          json: { skip: 0, take: 20, totalCount: 0, items: [] },
        });
      throw new Error(`Unexpected fixture API route: ${path}`);
    });
    await page.goto("/");
    await page.getByLabel("Employee code").fill("UI-ADMIN");
    await page
      .getByRole("textbox", { name: "Password", exact: true })
      .fill("Fixture-only-password1!");
    await page.getByRole("button", { name: "Sign in securely" }).click();
    await expect(
      page.getByRole("heading", {
        name: "Welcome back, IAM Administrator",
        exact: true,
      }),
    ).toBeVisible();
    await expect(page.locator(".brand__mark img")).toHaveJSProperty(
      "naturalWidth",
      32,
    );
    const nav = page.getByRole("navigation", {
      name: "Primary navigation",
      exact: true,
    });
    /* Named rather than counted: a bare count says nothing about which menu is missing, and it
       breaks on every new entry without telling the reader whether that entry was intended. */
    await expect(nav.getByRole("link")).toHaveText([
      "Overview",
      "Applications",
      "Organizations",
      "Users & access",
      "Machines & agents",
      "Security",
      "Audit",
    ]);
    await expect(
      page
        .locator("#primary-navigation")
        .getByRole("button", { name: "Previous page", exact: true }),
    ).toHaveCount(0);
    await expect(
      page
        .locator("#primary-navigation")
        .getByRole("button", { name: "Next page", exact: true }),
    ).toHaveCount(0);
    await expect(page.getByPlaceholder("Find a menu")).toHaveCount(0);
    await page.locator("#primary-navigation").screenshot({
      path: testInfo.outputPath(`sidebar-${theme}.png`),
    });
    await nav.getByRole("link", { name: "Applications", exact: true }).click();
    await expect(
      page.getByRole("heading", { name: "Application catalog", exact: true }),
    ).toBeVisible();
    /* The description used to hide behind a toggle; a page now states what it is for outright. */
    await expect(
      page.getByText(
        "Security clients, the module hierarchy and capability definitions.",
      ),
    ).toBeVisible();
    await page.getByRole("button", { name: "Toggle navigation" }).click();
    await expect(page.locator(".shell")).toHaveClass(/shell--collapsed/);
    await page.locator("#primary-navigation").screenshot({
      path: testInfo.outputPath(`sidebar-${theme}-collapsed.png`),
    });
    await nav.getByRole("link", { name: "Organizations", exact: true }).click();
    await expect(
      page.getByRole("heading", { name: "Organizations", exact: true }),
    ).toBeVisible();
    await page.getByRole("button", { name: "Toggle navigation" }).click();
    const group = nav.getByRole("button", { name: /Administration/ });
    await group.click();
    await expect(
      nav.getByRole("link", { name: "Applications", exact: true }),
    ).toBeHidden();
    await group.click();
    await page.keyboard.press("Control+k");
    /* The palette is a combobox, not a plain textbox: it owns a listbox of menu results. */
    const search = page.getByRole("combobox", { name: "Search IAM menus" });
    await expect(search).toBeFocused();
    await search.fill("password");
    await search.press("Enter");
    await expect(
      page.getByRole("heading", { name: "Security controls", exact: true }),
    ).toBeVisible();
    await expect(page.locator(".breadcrumb strong")).toHaveText("Security");
    await nav
      .getByRole("link", { name: "Users & access", exact: true })
      .click();
    await expect(
      page.getByRole("heading", { name: "Users & access", exact: true }),
    ).toBeVisible();
    await nav.getByRole("link", { name: "Audit", exact: true }).click();
    await expect(
      page.getByRole("heading", { name: "Audit & sessions", exact: true }),
    ).toBeVisible();
    await expect(
      page
        .locator("#primary-navigation")
        .getByRole("button", { name: "Next page", exact: true }),
    ).toHaveCount(0);
    await nav.getByRole("link", { name: "Organizations", exact: true }).click();
    const account = page.getByRole("button", {
      name: "Open menu for IAM Administrator",
    });
    await account.click();
    await page.getByRole("button", { name: "My profile", exact: true }).click();
    await expect(
      page.getByRole("heading", { name: "Signed-in profile" }),
    ).toBeVisible();
    await page
      .getByRole("button", { name: "Roles and permissions", exact: true })
      .click();
    await expect(page.locator(".account-details li")).toHaveText("iam.admin");
    await page
      .getByRole("button", { name: "Session details", exact: true })
      .click();
    await expect(page.locator(".account-details dd").last()).toHaveText("7");
    await page
      .getByRole("button", { name: "Session details", exact: true })
      .click();
    const otherTheme = theme === "light" ? "Dark" : "Light";
    await page.getByRole("radio", { name: otherTheme, exact: true }).click();
    await expect(page.locator("html")).toHaveAttribute(
      "data-theme",
      otherTheme.toLowerCase(),
    );
    await page
      .getByRole("radio", {
        name: theme === "light" ? "Light" : "Dark",
        exact: true,
      })
      .click();
    await expect(page.locator("html")).toHaveAttribute("data-theme", theme);
    const selectedTheme = page.getByRole("radio", {
      name: theme === "light" ? "Light" : "Dark",
      exact: true,
    });
    await selectedTheme.focus();
    await page.keyboard.press("ArrowRight");
    await expect(
      page.getByRole("radio", {
        name: theme === "light" ? "Dark" : "System",
        exact: true,
      }),
    ).toBeFocused();
    await page.keyboard.press("ArrowLeft");
    await expect(selectedTheme).toBeFocused();
    expect(
      await page.evaluate(() =>
        localStorage.getItem("identity.theme-preference"),
      ),
    ).toBe(theme);
    await page.screenshot({
      path: testInfo.outputPath(`pts-template-${theme}-desktop.png`),
      fullPage: true,
    });
    await page.keyboard.press("Escape");
    await expect(account).toBeFocused();
    await expect(
      page.getByRole("dialog", { name: "Account and appearance" }),
    ).toBeHidden();
    await page.setViewportSize({ width: 390, height: 844 });
    await expect(page.locator("#primary-navigation")).toHaveAttribute(
      "inert",
      "",
    );
    await page.getByRole("button", { name: "Toggle navigation" }).click();
    await expect(
      page.getByRole("button", { name: "Close navigation drawer" }),
    ).toBeFocused();
    await expect
      .poll(async () =>
        Math.round(
          (await page.locator("#primary-navigation").boundingBox())!.x,
        ),
      )
      .toBe(0);
    await page.screenshot({
      path: testInfo.outputPath(`pts-template-${theme}-mobile-navigation.png`),
      fullPage: true,
    });
    await page.keyboard.press("Escape");
    await expect(
      page.getByRole("button", { name: "Toggle navigation" }),
    ).toBeFocused();
    await account.click();
    const menu = page.getByRole("dialog", { name: "Account and appearance" });
    await expect(menu).toBeInViewport();
    await page.screenshot({
      path: testInfo.outputPath(`pts-template-${theme}-mobile.png`),
      fullPage: true,
    });
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true);
    await page.getByRole("button", { name: "Sign out", exact: true }).click();
    await expect(
      page.getByRole("button", { name: "Sign in securely" }),
    ).toBeVisible();
    expect(errors).toEqual([]);
  });
}
