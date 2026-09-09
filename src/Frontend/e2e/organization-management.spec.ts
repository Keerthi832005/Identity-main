import { expect, test } from "@playwright/test";
import type {
  OrganizationUnit,
  OrganizationFields,
  UnitType,
} from "../src/app/features/organization-management/organization.models";

for (const theme of ["light", "dark"] as const) {
  test(`organization management covers both hierarchies and conflict recovery in ${theme}`, async ({
    page,
  }, testInfo) => {
    test.setTimeout(90000);
    await page.emulateMedia({ colorScheme: theme });
    const pageErrors: string[] = [];
    page.on("pageerror", (error) => pageErrors.push(error.message));
    const units: OrganizationUnit[] = [];
    let nextId = 100;
    let stale = false;
    let failRead = false;
    const version = (value: number) =>
      Buffer.from(value.toString().padStart(8, "0")).toString("base64");
    await page.route("**/identity/api/v1/auth/browser/refresh", (route) =>
      route.fulfill({ status: 401, json: {} }),
    );
    await page.route("**/identity/api/v1/auth/browser/login", (route) => {
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
    });
    await page.route(
      "**/identity/api/v1/admin/organization-units?*",
      (route) => {
        if (failRead)
          return route.fulfill({ status: 500, json: { title: "Unavailable" } });
        const query = new URL(route.request().url()).searchParams;
        const all = units.filter(
          (unit) =>
            (!query.has("organizationId") ||
              unit.organizationId === Number(query.get("organizationId"))) &&
            (!query.has("parentOrganizationUnitId") ||
              unit.parentOrganizationUnitId ===
                Number(query.get("parentOrganizationUnitId"))) &&
            (!query.has("unitType") ||
              unit.unitType === query.get("unitType")) &&
            (!query.has("isActive") ||
              unit.isActive === (query.get("isActive") === "true")) &&
            (!query.has("search") ||
              `${unit.unitCode} ${unit.unitName} ${unit.description}`
                .toLowerCase()
                .includes(query.get("search")!.toLowerCase())),
        );
        const skip = Number(query.get("skip")),
          take = Number(query.get("take"));
        expect(take).toBeLessThanOrEqual(50);
        return route.fulfill({
          json: {
            skip,
            take,
            totalCount: all.length,
            items: all.slice(skip, skip + take),
          },
        });
      },
    );
    await page.route("**/identity/api/v1/admin/organizations**", (route) => {
      const request = route.request(),
        path = new URL(request.url()).pathname;
      const match = path.match(
        /organizations\/(\d+)\/units\/(\d+)(\/active)?$/,
      );
      if (match) {
        const index = units.findIndex(
          (unit) =>
            unit.organizationId === Number(match[1]) &&
            unit.organizationUnitId === Number(match[2]),
        );
        if (index < 0)
          return route.fulfill({ status: 404, json: { title: "Missing" } });
        if (request.method() === "GET")
          return route.fulfill({ json: units[index] });
        const body = request.postDataJSON() as OrganizationFields & {
          rowVersion: string;
          isActive: boolean;
        };
        expect(request.method()).toBe("PUT");
        expect(body.rowVersion).toBe(units[index].rowVersion);
        expect(body).not.toHaveProperty("parentOrganizationUnitId");
        expect(body).not.toHaveProperty("unitType");
        if (stale) {
          stale = false;
          units[index] = {
            ...units[index],
            unitName: "Concurrent saved name",
            rowVersion: version(nextId++),
          };
          return route.fulfill({
            status: 409,
            json: {
              code: "organization_concurrency_conflict",
              title:
                "This unit changed. Reload the latest details before retrying.",
            },
          });
        }
        units[index] = {
          ...units[index],
          ...(match[3]
            ? { isActive: body.isActive }
            : {
                unitCode: body.unitCode,
                unitName: body.unitName,
                description: body.description,
                address: body.address,
              }),
          rowVersion: version(nextId++),
          updatedAt: "2026-08-30T01:00:00Z",
        };
        return route.fulfill({ json: units[index] });
      }
      expect(request.method()).toBe("POST");
      const body = request.postDataJSON() as OrganizationFields & {
        unitType?: UnitType;
        parentOrganizationUnitId?: number;
      };
      const parent = units.find(
        (unit) => unit.organizationUnitId === body.parentOrganizationUnitId,
      );
      const id = nextId++;
      const type = body.unitType ?? "Organization";
      if (type !== "Organization") {
        expect(parent).toBeDefined();
        expect(parent!.isActive).toBe(true);
      }
      const unit: OrganizationUnit = {
        ...body,
        organizationId: parent?.organizationId ?? id,
        organizationUnitId: id,
        parentOrganizationUnitId: parent?.organizationUnitId ?? null,
        unitType: type,
        hierarchyPath: `${parent?.hierarchyPath ?? "/"}${id}/`,
        isActive: true,
        createdAt: "2026-08-30T00:00:00Z",
        updatedAt: null,
        rowVersion: version(id),
      };
      units.push(unit);
      return route.fulfill({ status: 201, json: unit });
    });
    await page.goto("/organizations");
    await page.getByLabel("Employee code").fill("TEST-ADMIN");
    await page
      .getByRole("textbox", { name: "Password", exact: true })
      .fill("browser-fixture-only-password");
    await page.getByRole("button", { name: "Sign in securely" }).click();
    await expect(
      page.getByRole("heading", { name: "Organizations", exact: true }),
    ).toBeVisible();
    await expect(page.locator("html")).toHaveAttribute("data-theme", theme);
    await expect(
      page.getByRole("heading", { name: "No units match these filters" }),
    ).toBeVisible();
    await page
      .getByRole("button", { name: "New organization", exact: true })
      .click();
    const editor = page.getByRole("form", { name: "New Organization" });
    await expect(page).toHaveURL(/\/organizations\/new$/);
    await expect(page.locator(".type-navigation")).toBeHidden();
    await expect(
      page.getByRole("button", { name: "Back to organizations" }),
    ).toBeVisible();
    await editor.getByLabel("Unit code", { exact: true }).fill("DISCARD");
    await page.getByRole("button", { name: "Back to organizations" }).click();
    await page
      .getByRole("alertdialog")
      .getByRole("button", { name: "Cancel", exact: true })
      .click();
    await expect(editor.getByLabel("Unit code", { exact: true })).toHaveValue(
      "DISCARD",
    );
    const back = page.goBack();
    await page
      .getByRole("alertdialog")
      .getByRole("button", { name: "Discard changes", exact: true })
      .click();
    await back;
    await expect(page).toHaveURL(/\/organizations$/);
    await expect(page.locator(".type-navigation")).toBeVisible();
    await page
      .getByRole("button", { name: "New organization", exact: true })
      .click();
    await editor.getByLabel("Unit code", { exact: true }).fill("ORG");
    await editor
      .getByLabel("Unit name", { exact: true })
      .fill("Review organization");
    await editor.getByLabel("Address line 3").fill("Floor three");
    await editor.getByLabel("Latitude", { exact: true }).fill("91");
    await editor.getByRole("button", { name: "Save", exact: true }).click();
    await expect(editor.getByRole("alert")).toContainText("Latitude");
    await editor.getByLabel("Latitude", { exact: true }).fill("13.123456");
    await editor.getByLabel("Longitude", { exact: true }).fill("80.123456");
    // Filled controls must remain aligned and keep the focus cue inside the
    // field, including numeric editors and their validation affordance.
    const textControl = editor.getByLabel("Unit name", { exact: true });
    const numberControl = editor.getByLabel("Longitude", { exact: true });
    const textBox = await textControl.evaluate((input) => {
      const editor = input.closest(".dx-texteditor")!;
      const style = getComputedStyle(editor);
      return {
        height: editor.getBoundingClientRect().height,
        topBorder: style.borderTopWidth,
        bottomBorder: style.borderBottomWidth,
        background: style.backgroundColor,
      };
    });
    const numberBox = await numberControl.evaluate((input) => {
      const editor = input.closest(".dx-texteditor")!;
      return {
        height: editor.getBoundingClientRect().height,
        focus: getComputedStyle(editor).boxShadow,
      };
    });
    expect(textBox.height).toBe(48);
    expect(numberBox.height).toBe(textBox.height);
    expect(textBox.topBorder).toBe("0px");
    expect(textBox.bottomBorder).toBe("1px");
    expect(textBox.background).not.toBe("rgba(0, 0, 0, 0)");
    expect(numberBox.focus).toContain("inset");
    await expect(
      editor.getByRole("button", { name: "Save", exact: true }),
    ).toHaveCSS("border-radius", "4px");
    await page.screenshot({
      path: testInfo.outputPath(`organization-${theme}-desktop.png`),
      fullPage: true,
      animations: "disabled",
    });
    await page.setViewportSize({ width: 390, height: 844 });
    await expect(page.locator(".sidebar")).not.toBeInViewport();
    await page.evaluate(() => window.scrollTo(0, 0));
    await expect(editor).toBeVisible();
    const box = await editor.boundingBox();
    expect(box!.x).toBeGreaterThanOrEqual(0);
    expect(box!.width).toBeLessThanOrEqual(390);
    expect(await page.locator(".modal-backdrop").count()).toBe(0);
    await page.screenshot({
      path: testInfo.outputPath(`organization-${theme}-mobile.png`),
      fullPage: true,
      animations: "disabled",
    });
    await editor.getByRole("button", { name: "Save", exact: true }).click();
    await expect(editor).toBeHidden();
    await page.setViewportSize({ width: 1280, height: 720 });
    await expect(
      page.getByRole("heading", { name: "Review organization", exact: true }),
    ).toBeVisible();
    for (const type of [
      "Country",
      "Region",
      "State",
      "Branch",
      "Location",
      "Department",
      "Team",
    ] as const) {
      if (type === "Department") {
        await page
          .getByRole("button", { name: "Back to organization units" })
          .click();
        await page
          .locator(".unit-item")
          .filter({ hasText: "Review organization" })
          .click();
      }
      await page
        .getByRole("button", { name: `New ${type}`, exact: true })
        .click();
      const dialog = page.getByRole("form", { name: `New ${type}` });
      if (type === "Country") {
        await expect(dialog).toBeVisible();
        await expect(page).toHaveURL(/\/new\/Country$/);
        await page.reload();
        await page.getByLabel("Employee code").fill("TEST-ADMIN");
        await page
          .getByRole("textbox", { name: "Password", exact: true })
          .fill("browser-fixture-only-password");
        await page.getByRole("button", { name: "Sign in securely" }).click();
        await expect(dialog).toBeVisible();
        await expect(dialog).toContainText("Review organization");
      }
      await dialog
        .getByLabel("Unit code", { exact: true })
        .fill(type.toUpperCase());
      await dialog
        .getByLabel("Unit name", { exact: true })
        .fill(`Review ${type}`);
      await dialog.getByRole("button", { name: "Save", exact: true }).click();
      await expect(dialog).toBeHidden();
      await page
        .getByRole("button", { name: "Edit details", exact: true })
        .click();
      const edit = page.getByRole("form", { name: `Edit ${type}` });
      await edit
        .getByLabel("Description", { exact: true })
        .fill(`${type} description`);
      await edit.getByLabel("City", { exact: true }).fill("Chennai");
      await edit.getByRole("button", { name: "Save", exact: true }).click();
      await expect(edit).toBeHidden();
      await expect(
        page.getByText(`${type} description`, { exact: true }),
      ).toBeVisible();
    }
    expect(new Set(units.map((unit) => unit.unitType)).size).toBe(8);
    await page
      .getByRole("button", { name: "Edit details", exact: true })
      .click();
    await page
      .getByLabel("Unit name", { exact: true })
      .fill("My unsaved draft");
    stale = true;
    await page
      .getByRole("form")
      .getByRole("button", { name: "Save", exact: true })
      .click();
    await expect(page.getByRole("alert")).toContainText("Reload");
    await expect(page.getByLabel("Unit name", { exact: true })).toHaveValue(
      "My unsaved draft",
    );
    await expect(
      page.getByRole("form").getByRole("button", { name: "Save", exact: true }),
    ).toBeDisabled();
    await page.getByRole("button", { name: "Reload latest details" }).click();
    await page
      .getByRole("alertdialog")
      .getByRole("button", { name: "Discard and reload", exact: true })
      .click();
    await expect(page.getByLabel("Unit name", { exact: true })).toHaveValue(
      "Concurrent saved name",
    );
    await page.getByLabel("Description", { exact: true }).fill("");
    await page
      .getByRole("form")
      .getByRole("button", { name: "Save", exact: true })
      .click();
    await expect(page.locator("form.editor")).toBeHidden();
    await expect(page.getByText("No description provided.")).toBeVisible();
    await page.getByRole("button", { name: "Deactivate", exact: true }).click();
    await page
      .getByRole("alertdialog")
      .getByRole("button", { name: "Confirm change", exact: true })
      .click();
    await expect(page.locator(".detail-header .state")).toHaveText("Inactive");
    await page.getByRole("button", { name: "Activate", exact: true }).click();
    await page
      .getByRole("alertdialog")
      .getByRole("button", { name: "Confirm change", exact: true })
      .click();
    await expect(page.locator(".detail-header .state")).toHaveText("Active");
    await page
      .getByRole("button", { name: "All organizations", exact: true })
      .click();
    await page
      .locator(".unit-item")
      .filter({ hasText: "Review organization" })
      .click();
    await page
      .getByRole("button", { name: "Browse children", exact: true })
      .click();
    await expect(page.locator(".unit-item")).toHaveCount(2);
    await page
      .locator(".unit-item")
      .filter({ hasText: "Review Country" })
      .click();
    await expect(
      page.getByRole("navigation", { name: "Unit ancestors" }),
    ).toContainText("Review organization");
    await expect(
      page.getByRole("button", { name: "Back to organization units" }),
    ).toBeVisible();
    await page.screenshot({
      path: testInfo.outputPath(`records-${theme}-desktop.png`),
      fullPage: true,
      animations: "disabled",
    });
    failRead = true;
    await page.getByRole("button", { name: "Search", exact: true }).click();
    await expect(page.getByRole("alert")).toContainText("could not be loaded");
    failRead = false;
    await page.getByRole("button", { name: "Retry list" }).click();
    await expect(page.locator(".unit-item")).toHaveCount(2);
    expect(pageErrors).toEqual([]);
  });
}
