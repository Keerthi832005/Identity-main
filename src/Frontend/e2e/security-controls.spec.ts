import { expect, test } from "@playwright/test";

function accessToken(): string {
  const payload = Buffer.from(
    JSON.stringify({
      sub: "42",
      employee_code: "ADMIN001",
      capability: ["iam.admin"],
      authorization_version: "3",
      exp: Math.floor(Date.now() / 1000) + 600,
    }),
  ).toString("base64url");
  return `header.${payload}.signature`;
}

test("administrator replaces a credential without retaining its value", async ({
  page,
}, testInfo) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.route("**/identity/**", (route) =>
    route.fulfill({ status: 404, json: {} }),
  );
  await page.route("**/identity/api/v1/auth/browser/refresh", (route) =>
    route.fulfill({ status: 401, contentType: "application/json", body: "{}" }),
  );
  await page.route("**/identity/api/v1/auth/browser/login", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        succeeded: true,
        accessToken: accessToken(),
        authorizationVersion: 3,
      }),
    }),
  );
  const users = [
    {
      userId: 42,
      employeeCode: "ADMIN001",
      displayName: "Identity Administrator",
      isActive: true,
      securityVersion: 3,
    },
    ...Array.from({ length: 50 }, (_, index) => ({
      userId: 100 + index,
      employeeCode: `EMP-${index}`,
      displayName: `Employee ${index}`,
      isActive: true,
      securityVersion: 1,
    })),
  ];
  await page.route("**/identity/api/v1/admin/users?*", (route) => {
    const params = new URL(route.request().url()).searchParams;
    const query = (params.get("search") ?? "").toLowerCase();
    const skip = Number(params.get("skip") ?? 0);
    const matches = users.filter((user) =>
      `${user.displayName} ${user.employeeCode}`.toLowerCase().includes(query),
    );
    return route.fulfill({
      json: {
        items: matches.slice(skip, skip + 50),
        totalCount: matches.length,
      },
    });
  });
  await page.route("**/identity/api/v1/admin/users/*/security", (route) => {
    const id = Number(
      new URL(route.request().url()).pathname.split("/").at(-2),
    );
    return route.fulfill({
      json: {
        user: users.find((user) => user.userId === id),
        credentials: [],
        devices: [],
        mfaMethods: [],
      },
    });
  });
  await page.route("**/identity/api/v1/admin/users/42/security", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        user: {
          userId: 42,
          employeeCode: "ADMIN001",
          displayName: "Identity Administrator",
          isActive: true,
          securityVersion: 3,
        },
        credentials: [],
        devices: [],
        mfaMethods: [],
      }),
    }),
  );
  await page.route(
    "**/identity/api/v1/admin/users/42/password",
    async (route) => {
      expect(
        (route.request().postDataJSON() as { password: string }).password,
      ).toBe("Secure password 2026!");
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ succeeded: true, failureCode: null }),
      });
    },
  );

  await page.goto("/security");
  // Wait for the protected-route session check and lazy sign-in route before inspecting fields.
  await page.waitForURL(/\/login\?returnUrl=%2Fsecurity$/);
  await expect(page.getByLabel("Employee code")).toHaveAttribute(
    "autocomplete",
    "off",
  );
  await expect(page.getByLabel("Password", { exact: true })).toHaveAttribute(
    "autocomplete",
    "new-password",
  );
  await page.getByLabel("Employee code").fill("ADMIN001");
  await page
    .getByLabel("Password", { exact: true })
    .fill("correct horse battery");
  await page.getByRole("button", { name: "Sign in securely" }).click();
  await expect(
    page.getByRole("heading", { name: "Security controls" }),
  ).toBeVisible();
  const password = page.getByLabel("New password");
  const grid = page.locator(".user-grid");
  await expect(
    grid.getByRole("columnheader", { name: "Column User", exact: true }),
  ).toBeVisible();
  await expect(
    grid.getByRole("columnheader", {
      name: "Column Employee code",
      exact: true,
    }),
  ).toBeVisible();
  await password.fill("clear me when changing accounts");
  await grid.getByRole("gridcell", { name: "Employee 0", exact: true }).click();
  await expect(page.locator(".identity h2")).toHaveText("Employee 0");
  await expect(password).toHaveValue("");
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await expect(page.locator(".user-pager")).toContainText("51–51 of 51");
  await expect(page.locator(".identity h2")).toHaveText("Employee 49");
  await expect(
    page.getByRole("button", { name: "Next", exact: true }),
  ).toBeDisabled();
  await page.getByRole("button", { name: "Previous", exact: true }).click();
  await expect(page.locator(".identity h2")).toHaveText(
    "Identity Administrator",
  );
  const findUser = page.getByRole("textbox", {
    name: "Find user",
    exact: true,
  });
  await findUser.fill("missing account");
  await findUser.press("Enter");
  await expect(grid).toContainText("No users found.");
  await expect(
    page.getByRole("heading", { name: "Select a user", exact: true }),
  ).toBeVisible();
  await findUser.fill("ADMIN001");
  await findUser.press("Enter");
  await expect(page.locator(".identity h2")).toHaveText(
    "Identity Administrator",
  );
  await findUser.fill("");
  await findUser.press("Enter");
  await expect(page.locator(".user-pager")).toContainText("1–50 of 51");
  const listBounds = await page.locator(".user-panel").boundingBox();
  const detailBounds = await page.locator(".content").boundingBox();
  expect(Math.abs(listBounds!.y - detailBounds!.y)).toBeLessThan(10);
  expect(
    (await page
      .getByRole("heading", { name: "Credentials", exact: true })
      .boundingBox())!.y - detailBounds!.y,
  ).toBeLessThan(180);
  await page.screenshot({
    path: testInfo.outputPath("security-aligned.png"),
    fullPage: true,
  });
  await expect(password).toHaveValue("");
  await page.setViewportSize({ width: 390, height: 844 });
  await expect
    .poll(async () => {
      const bounds = await page.locator("#primary-navigation").boundingBox();
      return bounds!.x + bounds!.width;
    })
    .toBeLessThanOrEqual(0);
  await expect(
    grid.getByRole("columnheader", {
      name: "Column Employee code",
      exact: true,
    }),
  ).toBeInViewport();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({
    path: testInfo.outputPath("security-grid-mobile.png"),
    fullPage: true,
  });
  await page.setViewportSize({ width: 1440, height: 1000 });
  await expect(password).toHaveAttribute("autocomplete", "new-password");
  await expect(password).toHaveAttribute("data-lpignore", "true");
  await password.fill("Secure password 2026!");
  await page.getByRole("button", { name: "Set password" }).click();
  await expect(page.getByText("Password replaced.")).toBeVisible();
  await expect(password).toHaveValue("");
  await page.route("**/identity/api/v1/admin/security/operations?*", (route) =>
    route.fulfill({
      json: {
        skip: 0,
        take: 50,
        totalAuditCount: 0,
        audits: [],
        generatedAt: new Date().toISOString(),
        sessions: [
          {
            tokenFamilyId: "fixture-family",
            userId: 42,
            applicationId: 7,
            applicationClientId: 8,
            deviceId: null,
            userDisplayName: "Identity Administrator",
            employeeCode: "ADMIN001",
            applicationName: "Production Tracking System",
            clientName: "PTS Web",
            issuedAt: new Date().toISOString(),
            expiresAt: new Date(Date.now() + 86400000).toISOString(),
            lastConsumedAt: null,
            revokedAt: null,
            isActive: true,
          },
        ],
      },
    }),
  );
  await page
    .getByRole("navigation", { name: "Primary navigation", exact: true })
    .getByRole("link", { name: "Audit", exact: true })
    .click();
  await expect(
    page.getByRole("gridcell", { name: "Identity Administrator", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("gridcell", {
      name: "Production Tracking System",
      exact: true,
    }),
  ).toBeVisible();
  await expect(
    page.getByRole("gridcell", { name: "PTS Web", exact: true }),
  ).toBeVisible();
  await page.screenshot({
    path: testInfo.outputPath("session-names.png"),
    fullPage: true,
  });
});
