import { expect, test } from "@playwright/test";
import { readFile } from "node:fs/promises";

for (const theme of ["light", "dark"] as const) {
  test(`machine diagnostics, ping outcomes and inventory export in ${theme}`, async ({
    page,
  }, testInfo) => {
    await page.emulateMedia({ colorScheme: theme });
    await page.setViewportSize(
      theme === "dark"
        ? { width: 390, height: 844 }
        : { width: 1440, height: 1000 },
    );
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    const now = new Date().toISOString();
    const machine = {
      installationId: "81111111-1111-4111-8111-111111111111",
      deviceId: 101,
      hostname: "QA-WORKSTATION",
      currentUserName: "FACTORY\\operator",
      windowsUsersReported: true,
      agentVersion: "1.0.1",
      createdAt: now,
      lastReportAt: now,
      isActive: true,
      isTrusted: true,
      trustedUntil: new Date(Date.now() + 86400000).toISOString(),
    };
    const retired = {
      ...machine,
      installationId: "82222222-2222-4222-8222-222222222222",
      hostname: "QA-RETIRED",
      isActive: false,
      isTrusted: false,
    };
    const inventory = {
      capturedAt: now,
      hostname: machine.hostname,
      os: "Windows test fixture",
      architecture: "X64",
      agentVersion: "1.0.1",
      hardware: {
        manufacturer: "Test manufacturer",
        model: "Synthetic PC",
        serialNumber: "TEST-001",
        cpu: "Fixture CPU",
        logicalProcessors: 8,
        memoryBytes: 16 * 1073741824,
        disks: [
          {
            name: "C:",
            totalBytes: 512 * 1073741824,
            freeBytes: 5 * 1073741824,
          },
        ],
      },
      software: [
        { name: "Example Tool", version: "2.0", publisher: "Example" },
      ],
      collectionWarnings: ["Synthetic test data"],
      diagnostics: { systemUptimeSeconds: 90061, agentUptimeSeconds: 3660 },
      windowsUsers: {
        currentUserName: "FACTORY\\operator",
        sessionsReported: true,
        profilesReported: true,
        sessions: [
          {
            sessionId: 1,
            userName: "FACTORY\\operator",
            state: "Active",
            isConsole: true,
          },
          {
            sessionId: 2,
            userName: "FACTORY\\technician",
            state: "Disconnected",
            isConsole: false,
          },
        ],
        profiles: [
          { sid: "S-1-5-21-100", userName: "FACTORY\\operator", loaded: true },
          {
            sid: "S-1-5-21-101",
            userName: "FACTORY\\technician",
            loaded: false,
          },
        ],
      },
    };
    let outcome = "reachable";
    let updateRequested = false;
    let controlReads = 0;
    await page.route("**/identity/**", (route) =>
      route.fulfill({ status: 404, json: {} }),
    );
    await page.route("**/identity/api/v1/auth/browser/refresh", (route) => {
      const payload = Buffer.from(
        JSON.stringify({
          sub: "42",
          employee_code: "TEST-ADMIN",
          capability: ["iam.admin"],
          authorization_version: "3",
          exp: Math.floor(Date.now() / 1000) + 600,
        }),
      ).toString("base64url");
      return route.fulfill({
        json: {
          succeeded: true,
          accessToken: `fixture.${payload}.fixture`,
          authorizationVersion: 3,
        },
      });
    });
    await page.route("**/identity/api/v1/admin/agents?*", (route) =>
      route.fulfill({
        json: { items: [machine, retired], total: 2, skip: 0, take: 50 },
      }),
    );
    await page.route(
      `**/identity/api/v1/admin/agents/${machine.installationId}`,
      (route) => route.fulfill({ json: { machine, inventory } }),
    );
    await page.route(
      `**/identity/api/v1/admin/agents/${retired.installationId}`,
      (route) => route.fulfill({ json: { machine: retired, inventory: null } }),
    );
    const update = {
      requestId: "83333333-3333-4333-8333-333333333333",
      requestedAt: now,
      expiresAt: new Date(Date.now() + 900000).toISOString(),
      deliveredAt: now,
      completedAt: null,
      status: "queued",
      agentVersion: null,
    };
    await page.route(`**/identity/api/v1/admin/agents/*/control`, (route) => {
      const active = route.request().url().includes(machine.installationId);
      if (active && updateRequested) controlReads++;
      return route.fulfill({
        json: {
          lastSeenAt: active ? now : null,
          agentVersion: "1.0.1",
          supervisorVersion: active ? "1.0.1" : "1.0.0",
          update:
            active && updateRequested
              ? {
                  ...update,
                  status: controlReads > 1 ? "upToDate" : "received",
                  agentVersion: controlReads > 1 ? "1.0.1" : null,
                }
              : null,
        },
      });
    });
    await page.route(
      `**/identity/api/v1/admin/agents/${machine.installationId}/update`,
      (route) => {
        expect(route.request().method()).toBe("POST");
        expect(route.request().postDataJSON()).toEqual({});
        updateRequested = true;
        return route.fulfill({ json: update });
      },
    );
    await page.route(
      `**/identity/api/v1/admin/agents/${machine.installationId}/ping`,
      (route) => {
        expect(route.request().method()).toBe("POST");
        expect(route.request().postDataJSON()).toEqual({});
        return outcome === "limited"
          ? route.fulfill({
              status: 429,
              headers: { "Retry-After": "60" },
              json: {},
            })
          : route.fulfill({
              json: {
                status: outcome,
                checkedAt: now,
                address: "192.0.2.1",
                roundTripMilliseconds: outcome === "reachable" ? 0 : null,
              },
            });
      },
    );
    await page.goto("/agents");
    await expect(
      page.getByText("FACTORY\\operator", { exact: true }).first(),
    ).toBeAttached();
    await page.screenshot({
      path: testInfo.outputPath(`machines-grid-${theme}.png`),
      fullPage: true,
    });
    await page.getByRole("link", { name: /QA-WORKSTATION/ }).click();
    await expect(page).toHaveURL(
      new RegExp(`/agents/${machine.installationId}`),
    );
    await expect(
      page.getByRole("heading", { name: "Signed-in sessions" }),
    ).toBeVisible();
    await expect(page.getByText("Disconnected · Session 2")).toBeVisible();
    await expect(page.getByText("1d 1h 1m", { exact: true })).toBeVisible();
    await expect(page.getByText(/Low disk space/)).toBeVisible();
    await expect(page.getByText(/Terminal trust expires/)).toBeVisible();
    await expect(page.getByText("Agent online", { exact: true })).toBeVisible();
    await page.getByRole("button", { name: "Update now", exact: true }).click();
    await expect(page.getByText(/Received — checking/)).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Update now", exact: true }),
    ).toBeDisabled();
    await expect(
      page.getByText("Already up to date", { exact: true }),
    ).toBeVisible({ timeout: 15000 });
    await page
      .getByRole("button", { name: "Ping machine", exact: true })
      .click();
    await expect(
      page.getByText("Network reachable", { exact: true }),
    ).toBeVisible();
    await expect(
      page.getByText("0 ms round trip", { exact: true }),
    ).toBeVisible();
    const downloadPromise = page.waitForEvent("download");
    await page
      .getByRole("button", { name: "Download inventory", exact: true })
      .click();
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toBe(
      "IAM-QA-WORKSTATION-inventory.json",
    );
    const file = testInfo.outputPath("inventory.json");
    await download.saveAs(file);
    const exported = JSON.parse(await readFile(file, "utf8"));
    expect(exported.machine.installationId).toBe(machine.installationId);
    expect(exported.inventory.diagnostics.systemUptimeSeconds).toBe(90061);
    expect(exported.networkPing.status).toBe("reachable");
    await page.screenshot({
      path: testInfo.outputPath(`agent-diagnostics-${theme}.png`),
      fullPage: true,
    });
    outcome = "noReply";
    await page
      .getByRole("button", { name: "Ping machine", exact: true })
      .click();
    await expect(page.getByText(/No ICMP reply/)).toBeVisible();
    await expect(
      page.getByText("Reporting", { exact: true }).first(),
    ).toBeVisible();
    outcome = "limited";
    await page
      .getByRole("button", { name: "Ping machine", exact: true })
      .click();
    await expect(page.getByText(/Wait one minute/)).toBeVisible();
    await page.getByRole("link", { name: "Back to machines" }).click();
    await page.getByRole("link", { name: /QA-RETIRED/ }).click();
    await expect(
      page.getByRole("button", { name: "Update now", exact: true }),
    ).toBeDisabled();
    await expect(
      page.getByText(/Immediate updates require a one-time supervisor/),
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Ping machine", exact: true }),
    ).toBeDisabled();
    await expect(page.getByText(/No ICMP reply/)).toHaveCount(0);
    expect(errors).toEqual([]);
  });
}
