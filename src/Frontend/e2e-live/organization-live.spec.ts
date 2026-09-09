import { randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";
import type {
  OrganizationUnit,
  OrganizationCreated,
  OrganizationFields,
  UnitType,
} from "../src/app/features/organization-management/organization.models";
const api = "/identity/api/v1/admin";
for (const theme of ["light", "dark"] as const) {
  test(`actual SQL organization management in ${theme}`, async ({
    page,
  }, testInfo) => {
    await page.emulateMedia({ colorScheme: theme });
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    // No route interception: login, reads and mutations use the actual IAM API and SQL.
    await page.goto("/organizations");
    await page.getByLabel("Employee code").fill("IAM-E2E-ADMIN");
    await page
      .getByRole("textbox", { name: "Password", exact: true })
      .fill(process.env["IAM_E2E_PASSWORD"]!);
    const loginPromise = page.waitForResponse((response) =>
      response.url().endsWith("/auth/browser/login"),
    );
    await page.getByRole("button", { name: "Sign in securely" }).click();
    const login = await loginPromise;
    expect(login.status()).toBe(200);
    const authentication = (await login.json()) as { accessToken: string };
    const headers = { Authorization: `Bearer ${authentication.accessToken}` };
    await expect(
      page.getByRole("heading", { name: "Organizations", exact: true }),
    ).toBeVisible();
    const rootCode = `LIVE-${theme}`;
    const fields: OrganizationFields = {
      unitCode: rootCode,
      unitName: `Live ${theme} organization`,
      description: "Live SQL verification",
      address: {
        addressLine1: null,
        addressLine2: null,
        addressLine3: null,
        city: null,
        district: null,
        stateName: null,
        postalCode: null,
        countryCode: null,
        latitude: null,
        longitude: null,
      },
    };
    const created: OrganizationUnit[] = [];
    async function create(
      type: UnitType,
      code: string,
      name: string,
    ): Promise<OrganizationUnit> {
      await page
        .getByRole("button", {
          name: type === "Organization" ? "New organization" : `New ${type}`,
          exact: true,
        })
        .click();
      const dialog = page.getByRole("form", { name: `New ${type}` });
      await dialog.getByLabel("Unit code", { exact: true }).fill(code);
      await dialog.getByLabel("Unit name", { exact: true }).fill(name);
      await dialog
        .getByLabel("Description", { exact: true })
        .fill("Live SQL verification");
      await dialog.getByLabel("Address line 1").fill("Verified address");
      await dialog.getByLabel("City", { exact: true }).fill("Chennai");
      const save = page.waitForResponse(
        (response) =>
          response.request().method() === "POST" &&
          response.url().includes("/admin/organizations"),
      );
      await dialog.getByRole("button", { name: "Save", exact: true }).click();
      const response = await save;
      expect(response.status()).toBe(201);
      const result = (await response.json()) as OrganizationCreated;
      await expect(dialog).toBeHidden();
      await expect(
        page.getByRole("heading", { name, exact: true }),
      ).toBeVisible();
      const read = await page.request.get(
        `${api}/organizations/${result.organizationId}/units/${result.organizationUnitId}`,
        { headers },
      );
      expect(read.status()).toBe(200);
      const unit = (await read.json()) as OrganizationUnit;
      expect(unit.unitType).toBe(type);
      expect(unit.address.city).toBe("Chennai");
      expect(unit.hierarchyPath.endsWith(`/${unit.organizationUnitId}/`)).toBe(
        true,
      );
      created.push(unit);
      return unit;
    }
    const root = await create("Organization", rootCode, fields.unitName);
    for (const type of [
      "Country",
      "Region",
      "State",
      "Branch",
      "Location",
    ] as const)
      await create(type, `${rootCode}-${type}`, `Live ${theme} ${type}`);
    await page.locator(".unit-item").filter({ hasText: rootCode }).click();
    const department = await create(
      "Department",
      `${rootCode}-Department`,
      `Live ${theme} Department`,
    );
    const team = await create("Team", `${rootCode}-Team`, `Live ${theme} Team`);
    expect(created).toHaveLength(8);
    expect(department.parentOrganizationUnitId).toBe(root.organizationUnitId);
    expect(team.parentOrganizationUnitId).toBe(department.organizationUnitId);
    for (const unit of created) {
      const detailUrl = `${api}/organizations/${unit.organizationId}/units/${unit.organizationUnitId}`;
      const correlation = randomUUID();
      const update = await page.request.put(detailUrl, {
        headers: { ...headers, "X-Correlation-ID": correlation },
        data: {
          unitCode: unit.unitCode,
          unitName: unit.unitName,
          description: "Updated through actual API",
          address: {
            ...unit.address,
            addressLine2: "Second",
            addressLine3: "Third",
            district: "District",
            stateName: "Tamil Nadu",
            postalCode: "600001",
            countryCode: "IND",
            latitude: 13.123456,
            longitude: 80.123456,
          },
          rowVersion: unit.rowVersion,
        },
      });
      expect(update.status()).toBe(200);
      const updated = (await update.json()) as OrganizationUnit;
      expect(updated.rowVersion).not.toBe(unit.rowVersion);
      expect(updated.hierarchyPath).toBe(unit.hierarchyPath);
      expect(updated.parentOrganizationUnitId).toBe(
        unit.parentOrganizationUnitId,
      );
      const replay = await page.request.put(detailUrl, {
        headers: { ...headers, "X-Correlation-ID": correlation },
        data: {
          unitCode: updated.unitCode,
          unitName: updated.unitName,
          description: updated.description,
          address: updated.address,
          rowVersion: updated.rowVersion,
        },
      });
      expect(replay.status()).toBe(200);
      expect(((await replay.json()) as OrganizationUnit).rowVersion).toBe(
        updated.rowVersion,
      );
      const audit = await page.request.get(
        `${api}/security/operations?correlationId=${correlation}&eventType=OrganizationUnitUpdated`,
        { headers },
      );
      expect(audit.status()).toBe(200);
      const auditBody = (await audit.json()) as { totalAuditCount: number };
      expect(auditBody.totalAuditCount).toBe(1);
    }
    // Reload the team before opening an editor, then race its snapshot with another actual HTTP write.
    await page.getByRole("button", { name: "Teams", exact: true }).click();
    await page.getByLabel("Search units").fill(`${rootCode}-Team`);
    await page.getByRole("button", { name: "Search", exact: true }).click();
    await page
      .locator(".unit-item")
      .filter({ hasText: `Live ${theme} Team` })
      .click();
    await page
      .getByRole("button", { name: "Edit details", exact: true })
      .click();
    await expect(page.getByRole("form", { name: "Edit Team" })).toBeVisible();
    const currentResponse = await page.request.get(
      `${api}/organizations/${team.organizationId}/units/${team.organizationUnitId}`,
      { headers },
    );
    const current = (await currentResponse.json()) as OrganizationUnit;
    const changed = await page.request.put(
      `${api}/organizations/${team.organizationId}/units/${team.organizationUnitId}`,
      {
        headers,
        data: {
          unitCode: current.unitCode,
          unitName: "Changed by concurrent writer",
          description: current.description,
          address: current.address,
          rowVersion: current.rowVersion,
        },
      },
    );
    expect(changed.status()).toBe(200);
    await page.getByLabel("Unit name", { exact: true }).fill("My local draft");
    await page
      .getByRole("form")
      .getByRole("button", { name: "Save", exact: true })
      .click();
    await expect(page.getByRole("alert")).toContainText("Reload");
    await expect(page.getByLabel("Unit name", { exact: true })).toHaveValue(
      "My local draft",
    );
    page.once("dialog", (dialog) => dialog.accept());
    await page.getByRole("button", { name: "Reload latest details" }).click();
    await expect(page.getByLabel("Unit name", { exact: true })).toHaveValue(
      "Changed by concurrent writer",
    );
    await page.getByLabel("Description", { exact: true }).fill("");
    await page.getByLabel("City", { exact: true }).fill("");
    await page
      .getByRole("form")
      .getByRole("button", { name: "Save", exact: true })
      .click();
    await expect(page.locator("form.editor")).toBeHidden();
    await expect(page.getByText("No description provided.")).toBeVisible();
    page.once("dialog", (dialog) => dialog.accept());
    await page.getByRole("button", { name: "Deactivate", exact: true }).click();
    await expect(page.locator(".detail-header .state")).toHaveText("Inactive");
    page.once("dialog", (dialog) => dialog.accept());
    await page.getByRole("button", { name: "Activate", exact: true }).click();
    await expect(page.locator(".detail-header .state")).toHaveText("Active");
    // Actual authorization and integrity failures; credentials/tokens remain in memory only.
    expect((await page.request.get(`${api}/organization-units`)).status()).toBe(
      401,
    );
    const invalid = await page.request.post(
      `${api}/organizations/${root.organizationId}/units`,
      {
        headers,
        data: {
          ...fields,
          unitCode: "INVALID",
          unitType: "Team",
          parentOrganizationUnitId: root.organizationUnitId,
        },
      },
    );
    expect(invalid.status()).toBe(400);
    const duplicate = await page.request.post(
      `${api}/organizations/${root.organizationId}/units`,
      {
        headers,
        data: {
          ...fields,
          unitCode: created[1].unitCode.toLowerCase(),
          unitType: "Department",
          parentOrganizationUnitId: root.organizationUnitId,
        },
      },
    );
    expect(duplicate.status()).toBe(409);
    expect(((await duplicate.json()) as { code: string }).code).toBe(
      "organization_code_conflict",
    );
    const wrongOwner = await page.request.post(
      `${api}/organizations/999999999/units`,
      {
        headers,
        data: {
          ...fields,
          unitType: "Country",
          parentOrganizationUnitId: root.organizationUnitId,
        },
      },
    );
    expect(wrongOwner.status()).toBe(400);
    expect(
      (
        await page.request.get(
          `${api}/organizations/999999999/units/${team.organizationUnitId}`,
          { headers },
        )
      ).status(),
    ).toBe(404);
    expect(
      (
        await page.request.get(`${api}/organization-units?take=51`, { headers })
      ).status(),
    ).toBe(400);
    const ordinaryCode = `E2E-ORDINARY-${theme}`;
    const userResponse = await page.request.post(`${api}/users`, {
      headers,
      data: { employeeCode: ordinaryCode, displayName: "Non administrator" },
    });
    expect(userResponse.status()).toBe(201);
    const user = (await userResponse.json()) as { resourceId: number };
    expect(
      (
        await page.request.post(`${api}/users/${user.resourceId}/password`, {
          headers,
          data: { password: process.env["IAM_E2E_PASSWORD"], expiresAt: null },
        })
      ).status(),
    ).toBe(200);
    expect(
      (
        await page.request.post(
          `${api}/users/${user.resourceId}/applications/${process.env["IAM_E2E_APP_ID"]}`,
          { headers },
        )
      ).status(),
    ).toBe(201);
    const ordinaryLogin = await page.request.post(
      "/identity/api/v1/auth/login",
      {
        data: {
          employeeCode: ordinaryCode,
          password: process.env["IAM_E2E_PASSWORD"],
          clientId: "identity-admin-web",
          clientSecret: null,
          deviceId: null,
        },
      },
    );
    expect(ordinaryLogin.status()).toBe(200);
    const ordinary = (await ordinaryLogin.json()) as { accessToken: string };
    expect(
      (
        await page.request.get(`${api}/organization-units`, {
          headers: { Authorization: `Bearer ${ordinary.accessToken}` },
        })
      ).status(),
    ).toBe(403);
    // More than one actual page; filter through the UI and verify next/previous.
    for (let i = 0; i < 21; i++) {
      const response = await page.request.post(
        `${api}/organizations/${root.organizationId}/units`,
        {
          headers,
          data: {
            ...fields,
            unitCode: `PAGE-${i}`,
            unitName: `Page country ${i.toString().padStart(2, "0")}`,
            unitType: "Country",
            parentOrganizationUnitId: root.organizationUnitId,
          },
        },
      );
      expect(response.status()).toBe(201);
    }
    await page
      .getByRole("button", { name: "All organizations", exact: true })
      .click();
    await page.getByLabel("Search units").fill(rootCode);
    await page.getByRole("button", { name: "Search", exact: true }).click();
    await page.locator(".unit-item").filter({ hasText: rootCode }).click();
    await page.getByRole("button", { name: "Scope to organization" }).click();
    await page.getByRole("button", { name: "Countries", exact: true }).click();
    await expect(page.locator(".unit-item")).toHaveCount(20);
    await page.getByRole("button", { name: "Next", exact: true }).click();
    await expect(page.locator(".unit-item")).toHaveCount(2);
    await page.getByRole("button", { name: "Previous", exact: true }).click();
    await expect(page.locator(".unit-item")).toHaveCount(20);
    await page.locator(".unit-item").first().click();
    await expect(page.locator(".detail-header")).toBeVisible();
    await page.screenshot({
      path: testInfo.outputPath(`live-${theme}-desktop.png`),
      fullPage: true,
      animations: "disabled",
    });
    await page.setViewportSize({ width: 390, height: 844 });
    await expect(page.locator(".sidebar")).not.toBeInViewport();
    await page.evaluate(() => window.scrollTo(0, 0));
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true);
    await page.screenshot({
      path: testInfo.outputPath(`live-${theme}-mobile.png`),
      fullPage: true,
      animations: "disabled",
    });
    await page
      .getByRole("button", { name: "Edit details", exact: true })
      .click();
    const save = page
      .getByRole("form")
      .getByRole("button", { name: "Save", exact: true });
    await save.scrollIntoViewIfNeeded();
    await expect(save).toBeInViewport();
    await expect(page.locator(".modal-backdrop")).toHaveCount(0);
    await page.screenshot({
      path: testInfo.outputPath(`live-${theme}-editor-mobile.png`),
      fullPage: true,
      animations: "disabled",
    });
    expect(errors).toEqual([]);
  });
}
