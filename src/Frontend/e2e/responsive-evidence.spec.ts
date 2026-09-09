import { expect, test, type Page } from "@playwright/test";

/*
 * Closes the responsive evidence gap recorded in T037 and T042: 360px and 1440px were captured
 * there, 768px and 1920px were not. Every screen is asserted for the one failure that is invisible
 * in a screenshot - a page body that scrolls sideways - and then photographed at both widths in
 * both themes.
 */

const viewports = [
  { name: "768", width: 768, height: 1024 },
  { name: "1920", width: 1920, height: 1080 },
] as const;

const screens = [
  { path: "/", name: "dashboard", heading: /^Welcome back,/ },
  {
    path: "/applications",
    name: "applications",
    heading: "Application catalog",
  },
  { path: "/users", name: "users", heading: "Users & access" },
  { path: "/users/42", name: "user-detail", heading: "Users & access" },
  {
    path: "/applications/10",
    name: "application-detail",
    heading: "Application catalog",
  },
  { path: "/organizations", name: "organizations", heading: "Organizations" },
  { path: "/audit", name: "audit", heading: "Audit & sessions" },
] as const;

const application = {
  applicationId: 10,
  applicationCode: "manufacturing",
  applicationName: "Manufacturing",
  tokenAudience: "urn:manufacturing",
  isActive: true,
  createdAt: "2026-08-29T12:00:00Z",
  updatedAt: null,
};
const user = {
  userId: 42,
  employeeCode: "TEST-ADMIN",
  displayName: "Test Administrator",
  isActive: true,
  securityVersion: 1,
  lastLoginAt: "2026-08-30T09:00:00Z",
  lockoutEndAt: null,
  createdAt: "2026-08-01T00:00:00Z",
  updatedAt: null,
};
const auditEvent = {
  authenticationAuditId: 1,
  userId: 42,
  applicationId: 10,
  eventType: "LoginSucceeded",
  succeeded: true,
  failureCode: null,
  correlationId: "aa11bb22-cc33-4d44-8e55-ff6677889900",
  occurredAt: "2026-08-30T09:00:00Z",
};
const modules = [
  {
    applicationModuleId: 500,
    applicationId: 10,
    moduleCode: "production",
    moduleName: "Production",
    description: "Shop floor",
    parentApplicationModuleId: null,
    displayOrder: 0,
    isSystem: false,
    isActive: true,
    createdAt: "2026-08-29T12:00:00Z",
    updatedAt: null,
  },
  {
    applicationModuleId: 501,
    applicationId: 10,
    moduleCode: "production.lines",
    moduleName: "Lines",
    description: null,
    parentApplicationModuleId: 500,
    displayOrder: 0,
    isSystem: false,
    isActive: true,
    createdAt: "2026-08-29T12:00:00Z",
    updatedAt: null,
  },
  {
    applicationModuleId: 502,
    applicationId: 10,
    moduleCode: "administration",
    moduleName: "Administration",
    description: null,
    parentApplicationModuleId: null,
    displayOrder: 1,
    isSystem: true,
    isActive: true,
    createdAt: "2026-08-29T12:00:00Z",
    updatedAt: null,
  },
];
const organizationUnit = {
  organizationId: 10,
  organizationUnitId: 100,
  parentOrganizationUnitId: null,
  unitType: "Organization",
  unitCode: "ORG",
  unitName: "Example organization",
  description: "Head office",
  hierarchyPath: "/100/",
  isActive: true,
  createdAt: "2026-08-30T00:00:00Z",
  updatedAt: null,
  rowVersion: "AAAAAAAAAAE=",
  address: {
    addressLine1: "1 Example Road",
    addressLine2: null,
    addressLine3: null,
    city: "Chennai",
    district: null,
    stateName: "Tamil Nadu",
    postalCode: "600001",
    countryCode: "IND",
    latitude: 13,
    longitude: 80,
  },
};

function paged(items: readonly object[]) {
  return { skip: 0, take: 50, totalCount: items.length, items };
}

function session() {
  const payload = Buffer.from(
    JSON.stringify({
      sub: "42",
      employee_code: "TEST-ADMIN",
      capability: ["iam.admin"],
      authorization_version: "1",
      exp: Math.floor(Date.now() / 1000) + 600,
    }),
  ).toString("base64url");
  return {
    succeeded: true,
    accessToken: `header.${payload}.signature`,
    authorizationVersion: 1,
  };
}

async function stubApi(page: Page): Promise<void> {
  /* The first refresh finds no session so the sign-in form is exercised; afterwards it behaves like
     a real refresh cookie, because each screen is reached by a full navigation. */
  let signedIn = false;
  await page.route("**/identity/api/v1/auth/browser/refresh", (route) =>
    signedIn
      ? route.fulfill({ json: session() })
      : route.fulfill({ status: 401, json: {} }),
  );
  await page.route("**/identity/api/v1/auth/browser/login", (route) => {
    signedIn = true;
    return route.fulfill({ json: session() });
  });
  await page.route("**/identity/api/v1/admin/**", (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith("/dashboard"))
      return route.fulfill({
        json: {
          applicationCount: 4,
          activeApplicationCount: 4,
          userCount: 128,
          activeUserCount: 120,
          activeSessionCount: 9,
          failedAuthenticationCountLast24Hours: 2,
          recentApplications: [application],
          recentUsers: [user],
          recentAuditEvents: [auditEvent],
          generatedAt: "2026-08-30T09:05:00Z",
        },
      });
    if (path.endsWith("/catalog"))
      return route.fulfill({
        json: {
          application,
          clients: [],
          modules,
          capabilities: [],
        },
      });
    if (path.endsWith("/access"))
      return route.fulfill({
        json: {
          user,
          applications: [],
          roleAssignments: [],
          overrides: [],
          evaluatedAt: "2026-08-30T09:05:00Z",
        },
      });
    if (path.endsWith("/security/operations"))
      return route.fulfill({
        json: {
          skip: 0,
          take: 50,
          totalAuditCount: 1,
          audits: [auditEvent],
          sessions: [],
          generatedAt: "2026-08-30T09:05:00Z",
        },
      });
    if (path.endsWith("/organization-units"))
      return route.fulfill({ json: paged([organizationUnit]) });
    if (path.endsWith("/applications"))
      return route.fulfill({ json: paged([application]) });
    if (path.endsWith("/users")) return route.fulfill({ json: paged([user]) });
    return route.fulfill({ json: {} });
  });
}

for (const theme of ["light", "dark"] as const) {
  test(`every screen holds its layout at 768px and 1920px in ${theme}`, async ({
    page,
  }, testInfo) => {
    test.setTimeout(120000);
    await page.emulateMedia({ colorScheme: theme });
    const pageErrors: string[] = [];
    page.on("pageerror", (error) => pageErrors.push(error.message));
    await stubApi(page);

    await page.setViewportSize(viewports[0]);
    await page.goto("/");
    await page.getByLabel("Employee code").fill("TEST-ADMIN");
    await page
      .getByRole("textbox", { name: "Password", exact: true })
      .fill("browser-fixture-only-password");
    await page.getByRole("button", { name: "Sign in securely" }).click();
    await expect(page.locator("html")).toHaveAttribute("data-theme", theme);

    for (const viewport of viewports) {
      await page.setViewportSize(viewport);
      for (const screen of screens) {
        await page.goto(screen.path);
        await expect(
          page
            .getByRole("heading", { name: screen.heading, exact: true })
            .first(),
        ).toBeVisible();
        // A body that scrolls sideways is the responsive failure a screenshot cannot show.
        expect(
          await page.evaluate(
            () => document.documentElement.scrollWidth <= window.innerWidth,
          ),
          `${screen.name} scrolls sideways at ${viewport.name}px`,
        ).toBe(true);
        await page.screenshot({
          path: testInfo.outputPath(
            `${screen.name}-${theme}-${viewport.name}.png`,
          ),
          fullPage: true,
          animations: "disabled",
        });
      }
    }

    expect(pageErrors).toEqual([]);
  });
}
