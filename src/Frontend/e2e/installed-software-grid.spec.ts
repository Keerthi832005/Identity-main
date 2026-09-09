import { expect, test } from "@playwright/test";

for (const theme of ["light", "dark"] as const) {
  test(`software grid searches all pages and filters metadata in ${theme}`, async ({
    page,
  }, testInfo) => {
    await page.emulateMedia({ colorScheme: theme });
    await page.setViewportSize({ width: 1440, height: 1000 });
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    const now = new Date().toISOString();
    const machine = {
      installationId: "81111111-1111-4111-8111-111111111111",
      deviceId: 101,
      hostname: "SOFTWARE-FIXTURE",
      agentVersion: "1.0.2",
      createdAt: now,
      lastReportAt: now,
      isActive: true,
      isTrusted: true,
      trustedUntil: null,
    };
    const second = {
      ...machine,
      installationId: "82222222-2222-4222-8222-222222222222",
      hostname: "LEGACY-FIXTURE",
    };
    const software = Array.from({ length: 62 }, (_, i) => ({
      name: `Tool ${String(i + 1).padStart(3, "0")}`,
      version: `2.${i}`,
      publisher: i >= 50 ? "Rare Vendor" : "Example Publisher",
      installedOn: i ? "2026-08-31" : null,
      estimatedSizeBytes: i ? 1572864 : null,
      registryView: i % 2 ? "32-bit" : "64-bit",
    }));
    const inventory = {
      capturedAt: now,
      hostname: machine.hostname,
      os: "Windows fixture",
      architecture: "X64",
      agentVersion: "1.0.2",
      hardware: {
        manufacturer: "Test",
        model: "Fixture",
        serialNumber: "TEST",
        cpu: "Fixture CPU",
        logicalProcessors: 8,
        memoryBytes: 1073741824,
        disks: [],
      },
      software,
      collectionWarnings: [],
    };
    // All API traffic is intercepted. No real accounts, enrollment or inventory is changed.
    await page.route("**/identity/**", (route) => {
      const url = new URL(route.request().url());
      if (url.pathname.endsWith("/auth/browser/refresh")) {
        const claims = Buffer.from(
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
            accessToken: `fixture.${claims}.fixture`,
            authorizationVersion: 1,
          },
        });
      }
      if (url.pathname.endsWith("/admin/agents"))
        return route.fulfill({
          json: { items: [machine, second], total: 2, skip: 0, take: 50 },
        });
      if (url.pathname.endsWith("/control"))
        return route.fulfill({
          json: {
            lastSeenAt: now,
            agentVersion: "1.0.2",
            supervisorVersion: "1.0.1",
            update: null,
          },
        });
      if (url.pathname.endsWith(machine.installationId))
        return route.fulfill({ json: { machine, inventory } });
      if (url.pathname.endsWith(second.installationId))
        return route.fulfill({
          json: {
            machine: second,
            inventory: {
              ...inventory,
              software: [
                { name: "Legacy application", version: "", publisher: "" },
              ],
            },
          },
        });
      return route.fulfill({ status: 404, json: {} });
    });
    await page.goto("/agents");
    await page.getByRole("link", { name: /SOFTWARE-FIXTURE/ }).click();
    const grid = page.locator("app-installed-software-grid");
    const rows = grid.locator(".dx-data-row");
    await expect(rows).toHaveCount(25);
    await expect(grid).toContainText("Page 1 of 3 (62 applications)");
    await expect(grid).toContainText("31 Aug 2026");
    await expect(grid).toContainText("Not reported");
    await grid.locator(".dx-next-button").click();
    await expect(grid).toContainText("Page 2 of 3");
    await expect(rows.first()).toContainText("Tool 026");
    const search = grid.getByPlaceholder("Search all software");
    await search.fill("Tool 062");
    await expect(rows).toHaveCount(1);
    await expect(rows.first()).toContainText("Tool 062");
    await grid.getByRole("button", { name: "Clear software filters" }).click();
    await expect(rows).toHaveCount(25);
    await expect(grid).toContainText("Page 1 of 3");
    await grid
      .locator(".dx-datagrid-filter-row td")
      .nth(2)
      .getByRole("textbox")
      .fill("Rare Vendor");
    await expect(rows).toHaveCount(12);
    await expect(grid).toContainText("12 applications");
    await grid.getByRole("button", { name: "Clear software filters" }).click();
    await expect(rows).toHaveCount(25);
    await expect(grid).toContainText("Page 1 of 3 (62 applications)");
    await grid
      .locator(".dx-page-size")
      .getByText("50", { exact: true })
      .click();
    await grid.getByRole("button", { name: "Page 1", exact: true }).click();
    await expect(rows).toHaveCount(50);
    await search.fill("No matching application");
    await expect(rows).toHaveCount(0);
    await expect(
      grid.getByText(/No installed software to display/),
    ).toBeVisible();
    await grid.getByRole("button", { name: "Clear software filters" }).click();
    await search.fill("Tool 062");
    await expect(rows).toHaveCount(1);
    await grid.scrollIntoViewIfNeeded();
    await page.screenshot({
      path: testInfo.outputPath(`software-grid-${theme}.png`),
      fullPage: true,
    });
    await page.setViewportSize({ width: 390, height: 844 });
    await expect(search).toBeVisible();
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth + 1,
      ),
    ).toBe(true);
    await page.screenshot({
      path: testInfo.outputPath(`software-grid-${theme}-mobile.png`),
      fullPage: true,
    });
    await page.getByRole("link", { name: "Back to machines" }).click();
    await page.getByRole("link", { name: /LEGACY-FIXTURE/ }).click();
    await expect(rows).toHaveCount(1);
    await expect(grid).toContainText("Legacy application");
    await expect(search).toHaveValue("");
    await expect(grid).toContainText("Not reported");
    expect(errors).toEqual([]);
  });
}
