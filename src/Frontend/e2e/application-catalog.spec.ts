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

for (const theme of ["light", "dark"] as const) {
  test(`administrator signs in and manages the typed application catalog in ${theme}`, async ({
    page,
  }, testInfo) => {
    await page.emulateMedia({ colorScheme: theme });
    const clients: object[] = [];
    const application = {
      applicationId: 10,
      applicationCode: "manufacturing",
      applicationName: "Manufacturing",
      tokenAudience: "urn:manufacturing",
      isActive: true,
      createdAt: "2026-08-29T12:00:00Z",
      updatedAt: null,
    };
    await page.route("**/identity/api/v1/auth/browser/refresh", (route) =>
      route.fulfill({
        status: 401,
        contentType: "application/json",
        body: "{}",
      }),
    );
    await page.route("**/identity/api/v1/auth/browser/login", (route) =>
      route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          succeeded: true,
          failureCode: null,
          accessToken: accessToken(),
          accessTokenExpiresAt: null,
          refreshToken: null,
          refreshTokenExpiresAt: null,
          authorizationVersion: 3,
          mfaChallengeId: null,
        }),
      }),
    );
    await page.route("**/identity/api/v1/admin/dashboard?*", (route) =>
      route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          applicationCount: 1,
          activeApplicationCount: 1,
          userCount: 1,
          activeUserCount: 1,
          activeSessionCount: 1,
          failedAuthenticationCountLast24Hours: 0,
          recentApplications: [application],
          recentUsers: [],
          recentAuditEvents: [],
          generatedAt: "2026-08-29T12:00:00Z",
        }),
      }),
    );
    await page.route("**/identity/api/v1/admin/applications?*", (route) =>
      route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          skip: 0,
          take: 50,
          totalCount: 1,
          items: [application],
        }),
      }),
    );
    await page.route(
      "**/identity/api/v1/admin/applications/10/catalog",
      (route) =>
        route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify({
            application,
            clients,
            modules: [
              {
                applicationModuleId: 11,
                moduleCode: "shop",
                moduleName: "Shop",
                isActive: true,
                displayOrder: 0,
              },
            ],
            capabilities: [],
          }),
        }),
    );
    await page.route(
      "**/identity/api/v1/admin/applications/10/clients",
      async (route) => {
        const request = route.request().postDataJSON() as {
          clientId: string;
          clientSecret: unknown;
        };
        expect(request.clientId).toBe("manufacturing-web");
        expect(request.clientSecret).toBeNull();
        clients.push({
          applicationClientId: 20,
          applicationId: 10,
          clientId: request.clientId,
          clientName: "Manufacturing Web",
          clientType: "Public",
          secretVersion: 0,
          isActive: true,
          createdAt: "2026-08-29T12:00:00Z",
          expiresAt: null,
          revokedAt: null,
        });
        await route.fulfill({
          status: 201,
          contentType: "application/json",
          body: JSON.stringify({
            resourceType: "ApplicationClient",
            resourceId: 20,
            userId: null,
            applicationId: 10,
            authorizationVersion: null,
          }),
        });
      },
    );

    await page.goto("/login");
    await page.getByLabel("Employee code").fill("ADMIN001");
    await page
      .getByRole("textbox", { name: "Password", exact: true })
      .fill("correct horse battery");
    await page.getByRole("button", { name: "Sign in securely" }).click();
    await expect(
      page.getByRole("heading", { name: /^Welcome back,/ }),
    ).toBeVisible();
    await page
      .getByRole("navigation", { name: "Primary navigation", exact: true })
      .getByRole("link", { name: "Applications", exact: true })
      .click();
    await expect(
      page.getByRole("heading", { name: "Application catalog" }),
    ).toBeVisible();
    await page
      .getByRole("link", { name: "Manufacturing", exact: true })
      .click();
    await expect(page).toHaveURL(/\/applications\/10/);
    for (const [action, title] of [
      ["New application", "New application"],
      ["Add module", "New module"],
      ["Add capability", "New capability"],
    ]) {
      await page.getByRole("button", { name: action, exact: true }).click();
      await expect(
        page.getByRole("heading", { name: title, exact: true }),
      ).toBeVisible();
      await expect(page.locator(".catalog-layout")).toBeHidden();
      await expect(page.locator(".modal-backdrop")).toHaveCount(0);
      await page.getByRole("button", { name: "Back to applications" }).click();
      await expect(page.locator(".catalog-layout")).toBeVisible();
    }
    await page.getByRole("button", { name: "Add client" }).click();
    await page.getByLabel("Client ID").fill("unsaved-client");
    await page
      .getByRole("link", { name: "Users & access", exact: true })
      .click();
    await page
      .getByRole("alertdialog")
      .getByRole("button", { name: "Cancel", exact: true })
      .click();
    await expect(page.getByLabel("Client ID")).toHaveValue("unsaved-client");
    await page.getByRole("button", { name: "Back to applications" }).click();
    await page
      .getByRole("alertdialog")
      .getByRole("button", { name: "Discard changes", exact: true })
      .click();
    await page.getByRole("button", { name: "Add client" }).click();
    await page.setViewportSize({ width: 390, height: 844 });
    await expect(page.locator(".sidebar")).not.toBeInViewport();
    await page.evaluate(() => window.scrollTo(0, 0));
    await expect(
      page.getByRole("button", { name: "Back to applications" }),
    ).toBeInViewport();
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true);
    await page.screenshot({
      path: testInfo.outputPath(`catalog-${theme}-mobile.png`),
      fullPage: true,
      animations: "disabled",
    });
    await page.setViewportSize({ width: 1280, height: 720 });
    await page.getByLabel("Client ID").fill("manufacturing-web");
    await page.getByLabel("Name").fill("Manufacturing Web");
    await page.getByRole("button", { name: "Create" }).click();
    await expect(page.getByText("Manufacturing Web")).toBeVisible();
    await expect(page.getByText("Client created successfully.")).toBeVisible();
  });

  test(`user access explains Deny precedence over role grants in ${theme}`, async ({
    page,
  }, testInfo) => {
    await page.emulateMedia({ colorScheme: theme });
    await page.route("**/identity/api/v1/auth/browser/refresh", (route) =>
      route.fulfill({
        status: 401,
        contentType: "application/json",
        body: "{}",
      }),
    );
    await page.route("**/identity/api/v1/auth/browser/login", (route) =>
      route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          succeeded: true,
          failureCode: null,
          accessToken: accessToken(),
          accessTokenExpiresAt: null,
          refreshToken: null,
          refreshTokenExpiresAt: null,
          authorizationVersion: 3,
          mfaChallengeId: null,
        }),
      }),
    );
    const user = {
      userId: 42,
      employeeCode: "ADMIN001",
      displayName: "Identity Administrator",
      isActive: true,
      securityVersion: 1,
      lastLoginAt: null,
      lockoutEndAt: null,
      createdAt: "2026-08-29T12:00:00Z",
      updatedAt: null,
    };
    const application = {
      applicationId: 10,
      applicationCode: "iam-administration",
      applicationName: "Identity Administration",
      tokenAudience: "urn:identity:administration",
      isActive: true,
      createdAt: "2026-08-29T12:00:00Z",
      updatedAt: null,
    };
    await page.route("**/identity/api/v1/admin/users?*", (route) =>
      route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          skip: 0,
          take: 50,
          totalCount: 1,
          items: [user],
        }),
      }),
    );
    await page.route("**/identity/api/v1/admin/applications?*", (route) =>
      route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          skip: 0,
          take: 50,
          totalCount: 1,
          items: [
            application,
            {
              ...application,
              applicationId: 20,
              applicationName: "Review application",
            },
          ],
        }),
      }),
    );
    await page.route("**/identity/api/v1/admin/users/42/access", (route) =>
      route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          user,
          applications: [
            {
              userId: 42,
              applicationId: 10,
              applicationCode: "iam-administration",
              applicationName: "Identity Administration",
              isActive: true,
              authorizationVersion: 4,
              assignedAt: "2026-08-29T12:00:00Z",
              revokedAt: null,
              effectiveCapabilities: ["iam.admin"],
            },
          ],
          roleAssignments: [
            {
              userRoleId: 40,
              userId: 42,
              applicationId: 10,
              roleId: 30,
              roleCode: "administrator",
              roleName: "Administrator",
              assignedAt: "2026-08-29T12:00:00Z",
              revokedAt: null,
            },
          ],
          overrides: [
            {
              userPermissionOverrideId: 50,
              userId: 42,
              applicationId: 10,
              moduleCapabilityId: 23,
              capabilityCode: "iam.audit",
              effect: "Deny",
              reason: "Separation of duties",
              assignedAt: "2026-08-29T12:00:00Z",
              expiresAt: null,
              revokedAt: null,
            },
          ],
          evaluatedAt: "2026-08-29T12:00:00Z",
        }),
      }),
    );
    await page.route(
      "**/identity/api/v1/admin/applications/*/access",
      (route) =>
        route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify({
            applicationId: 10,
            roles: [
              {
                roleId: 30,
                applicationId: 10,
                roleCode: "administrator",
                roleName: "Administrator",
                description: null,
                isSystem: true,
                isActive: true,
              },
            ],
            rolePermissions: [
              {
                rolePermissionId: 31,
                applicationId: 10,
                roleId: 30,
                moduleCapabilityId: 22,
                capabilityCode: "iam.admin",
                grantedAt: "2026-08-29T12:00:00Z",
                revokedAt: null,
              },
              {
                rolePermissionId: 32,
                applicationId: 10,
                roleId: 30,
                moduleCapabilityId: 23,
                capabilityCode: "iam.audit",
                grantedAt: "2026-08-29T12:00:00Z",
                revokedAt: null,
              },
            ],
            capabilities: [
              {
                moduleCapabilityId: 22,
                capabilityCode: "iam.admin",
                capabilityName: "Administration",
                isActive: true,
              },
            ],
          }),
        }),
    );
    await page.goto("/users");
    await page.getByLabel("Employee code").fill("ADMIN001");
    await page
      .getByRole("textbox", { name: "Password", exact: true })
      .fill("correct horse battery");
    await page.getByRole("button", { name: "Sign in securely" }).click();
    await expect(
      page.getByRole("heading", { name: "Users & access" }),
    ).toBeVisible();
    await page
      .getByRole("link", { name: "Identity Administrator", exact: true })
      .click();
    // The detail is tabbed; grants and overrides live under Access.
    await page.getByRole("tab", { name: "Access", exact: true }).click();
    await expect(page.getByText("Deny", { exact: true })).toBeVisible();
    await expect(page.getByText("Separation of duties")).toBeVisible();
    for (const [tab, action, title] of [
      ["Roles", "New role", "Create role"],
      ["Access", "Assign role", "Assign role"],
      ["Roles", "Grant capability", "Grant role capability"],
      ["Access", "Add override", "Set user override"],
    ]) {
      await page.getByRole("tab", { name: tab, exact: true }).click();
      await page.getByRole("button", { name: action, exact: true }).click();
      await expect(
        page.getByRole("heading", { name: title, exact: true }),
      ).toBeVisible();
      await expect(page.locator(".access-layout")).toBeHidden();
      await expect(page.locator(".modal-backdrop")).toHaveCount(0);
      await page
        .getByRole("button", { name: "Back to users & access" })
        .click();
      await expect(page.locator(".access-layout")).toBeVisible();
    }
    await page.getByLabel("Application context", { exact: true }).click();
    await page
      .getByRole("option", { name: "Review application", exact: true })
      .click();
    await page
      .getByRole("button", { name: "Grant application", exact: true })
      .click();
    await expect(
      page.getByRole("form", { name: "Grant application", exact: true }),
    ).toContainText("Identity Administrator");
    await page.getByRole("button", { name: "Back to users & access" }).click();
    await expect(
      page.getByLabel("Application context", { exact: true }),
    ).toHaveValue("Review application");
    await page.getByLabel("Application context", { exact: true }).click();
    await page
      .getByRole("option", { name: "Identity Administration", exact: true })
      .click();
    const effective = page.locator(".explanation > footer");
    await expect(
      effective.getByText("iam.admin", { exact: true }),
    ).toBeVisible();
    await expect(effective.getByText("iam.audit", { exact: true })).toHaveCount(
      0,
    );
  });
}
