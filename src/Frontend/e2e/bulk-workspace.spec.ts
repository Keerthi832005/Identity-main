import { expect, test } from "@playwright/test";

for (const mobile of [false, true]) {
  test(`bulk grid paging, corrections and commit recovery on ${mobile ? "mobile" : "desktop"}`, async ({
    page,
  }, testInfo) => {
    await page.setViewportSize(
      mobile ? { width: 390, height: 844 } : { width: 1440, height: 1000 },
    );
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    // All requests are isolated fixtures: no production identity record can be changed.
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
    const batchKey = "91111111-1111-4111-8111-111111111111";
    let committed = false;
    let commitAttempts = 0;
    const rows = Array.from({ length: 253 }, (_, index) => ({
      sourceRowNumber: index + 3,
      state: index === 0 ? "Update" : index === 252 ? "Invalid" : "Create",
      values: {
        unitType: index === 0 ? "Organization" : "Country",
        unitCode: index === 0 ? "QA-ROOT" : `QA-${index}`,
        unitName: index === 252 ? "" : `Test unit ${index}`,
        parentUnitCode: index === 0 ? "" : "QA-ROOT",
        description: "Synthetic regression data",
        addressLine1: "",
      },
      errors:
        index === 252
          ? [
              {
                sourceRowNumber: 255,
                columnId: "unitName",
                code: "cell.required",
                message: "A unit name is required.",
              },
            ]
          : [],
    }));
    const reads: { skip: number; take: number }[] = [];
    await page.route(
      `**/identity/api/v1/admin/bulk/staging/${batchKey}**`,
      async (route) => {
        const request = route.request();
        const url = new URL(request.url());
        if (url.pathname.endsWith("/commit")) {
          expect(request.method()).toBe("POST");
          commitAttempts++;
          if (commitAttempts === 1)
            return route.fulfill({
              status: 400,
              json: {
                title: "The request is invalid.",
                correlationId: "test-commit-reference",
              },
            });
          committed = true;
          return route.fulfill({
            json: {
              batchKey,
              createdRowCount: 252,
              updatedRowCount: 1,
              remainingInvalidRowCount: 0,
              alreadyCommitted: false,
            },
          });
        }
        if (url.pathname.endsWith("/rows/255")) {
          expect(request.method()).toBe("PUT");
          expect(request.postDataJSON().values.unitName).toBe("Corrected unit");
          rows[252].values.unitName = "Corrected unit";
          rows[252].state = "Create";
          rows[252].errors = [];
          return route.fulfill({ json: {} });
        }
        if (request.method() !== "GET")
          return route.fulfill({ status: 405, json: {} });
        const skip = Number(url.searchParams.get("skip"));
        const take = Number(url.searchParams.get("take"));
        reads.push({ skip, take });
        const invalidRows = rows.filter(
          (row) => row.state === "Invalid",
        ).length;
        const filtered =
          url.searchParams.get("filter") === "needs-attention"
            ? rows.filter((row) => row.state === "Invalid")
            : rows;
        return route.fulfill({
          json: {
            batch: {
              batchKey,
              entityKey: "organization-units",
              source: "Excel",
              fileName: "fixture.xlsx",
              state: committed ? "Committed" : "Staged",
              submittedAt: new Date().toISOString(),
              expiresAt: new Date(Date.now() + 86400000).toISOString(),
              totalRows: rows.length,
              createRows: committed ? 0 : 252 - invalidRows,
              updateRows: committed ? 0 : 1,
              invalidRows,
              appliedRows: committed ? rows.length : 0,
            },
            items: filtered
              .slice(skip, skip + take)
              .map((row) => (committed ? { ...row, state: "Applied" } : row)),
            totalCount: filtered.length,
            skip,
            take,
          },
        });
      },
    );

    await page.goto(`/bulk/${batchKey}`);
    const grid = page.locator("dx-data-grid");
    await expect(grid).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Commit 252 rows", exact: true }),
    ).toBeEnabled();
    await expect
      .poll(() => reads.some((read) => read.skip === 0 && read.take === 25))
      .toBe(true);
    const pager = grid.locator(".dx-pager");
    await expect(pager).toBeVisible();
    await expect(page.locator(".dx-loadpanel-content")).toBeHidden();
    if (mobile) {
      // Adaptive pager exposes both the page number and the page-size dropdown.
      await expect(pager.locator(".dx-page-sizes")).toBeVisible();
      await expect(pager.locator(".dx-pages")).toBeVisible();
      await pager.locator(".dx-page-sizes .dx-selectbox").click();
      await page.getByRole("option", { name: "100", exact: true }).click();
      await expect(
        pager.getByRole("combobox", { name: "Page size", exact: true }),
      ).toHaveValue("100");
      await expect(grid.locator(".dx-data-row")).toHaveCount(100);
      const pageNumber = pager.getByRole("spinbutton", {
        name: "Page number",
        exact: true,
      });
      await pageNumber.fill("3");
      await pageNumber.press("Enter");
      await expect(
        grid.getByRole("textbox", { name: "unitCode on row 203", exact: true }),
      ).toHaveValue("QA-200");
    } else {
      await pager.getByLabel("Page 11", { exact: true }).click();
      await expect
        .poll(() => reads.some((read) => read.skip === 250 && read.take === 25))
        .toBe(true);
      await expect(
        grid.getByRole("textbox", { name: "unitName on row 255", exact: true }),
      ).toHaveCount(1);
      await pager
        .getByRole("button", { name: "Items per page: 50", exact: true })
        .click();
      await expect
        .poll(() => reads.some((read) => read.take === 50))
        .toBe(true);
    }
    await page
      .getByRole("button", { name: "Needs attention", exact: true })
      .click();
    const name = grid.getByRole("textbox", {
      name: "unitName on row 255",
      exact: true,
    });
    await expect(name).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Refresh rows", exact: true }),
    ).toBeEnabled();
    await expect(page.locator(".dx-loadpanel-content")).toBeHidden();
    await expect(name).toBeEnabled();
    await name.fill("Corrected unit");
    await name.press("Tab");
    await expect(
      page.getByRole("button", { name: "Commit 253 rows", exact: true }),
    ).toBeEnabled();
    await expect(grid.getByText("No rows match this filter.")).toBeVisible();
    await page.getByRole("button", { name: "All 253", exact: true }).click();
    await expect(
      grid.getByRole("textbox", { name: "unitCode on row 3", exact: true }),
    ).toHaveValue("QA-ROOT");
    await expect(
      page.getByRole("button", { name: "Refresh rows", exact: true }),
    ).toBeEnabled();
    await expect(page.locator(".dx-loadpanel-content")).toBeHidden();
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: testInfo.outputPath(
        `bulk-grid-${mobile ? "mobile" : "desktop"}.png`,
      ),
      fullPage: true,
    });
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true);
    await page
      .getByRole("button", { name: "Commit 253 rows", exact: true })
      .click();
    await page
      .getByRole("button", { name: "Apply import", exact: true })
      .click();
    await expect(
      page.getByText("Request could not be completed", { exact: true }),
    ).toBeVisible();
    await expect(
      page.getByText("Reference test-commit-reference", { exact: true }),
    ).toBeVisible();
    await expect(grid).toBeVisible();
    await expect(
      page.getByText("Batch unavailable", { exact: true }),
    ).toHaveCount(0);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
      path: testInfo.outputPath(
        `bulk-commit-error-${mobile ? "mobile" : "desktop"}.png`,
      ),
      fullPage: true,
    });
    await page
      .getByRole("button", { name: "Refresh rows", exact: true })
      .click();
    await expect(
      page.getByRole("button", { name: "Commit 253 rows", exact: true }),
    ).toBeEnabled();
    await page
      .getByRole("button", { name: "Commit 253 rows", exact: true })
      .click();
    await page
      .getByRole("button", { name: "Apply import", exact: true })
      .click();
    await expect(
      page.getByText("Commit applied", { exact: true }),
    ).toBeVisible();
    await expect(grid.getByRole("textbox").first()).toBeDisabled();
    expect(commitAttempts).toBe(2);
    expect(errors).toEqual([]);
  });
}
