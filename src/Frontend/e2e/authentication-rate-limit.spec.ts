import { expect, test } from "@playwright/test";

for (const theme of ["light", "dark"] as const) {
  test(`sign-in and MFA honor retry countdowns in ${theme}`, async ({
    page,
  }, testInfo) => {
    await page.emulateMedia({ colorScheme: theme });
    await page.setViewportSize({ width: 1440, height: 1000 });
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    let logins = 0;
    let mfaRequests = 0;
    await page.route("**/config.json", (route) =>
      route.fulfill({
        json: {
          identityBaseUrl: "/identity",
          identityClientId: "fixture-iam",
          applicationName: "Identity Administration",
          devExtremeLicenseKey: "",
        },
      }),
    );
    await page.route("**/identity/api/v1/auth/browser/**", async (route) => {
      const path = new URL(route.request().url()).pathname;
      if (path.endsWith("/refresh"))
        return route.fulfill({ status: 401, json: {} });
      if (path.endsWith("/login")) {
        logins++;
        if (logins > 1)
          return route.fulfill({
            status: 202,
            json: {
              succeeded: false,
              refreshToken: null,
              accessToken: null,
              mfaChallengeId: "33445566-7788-4990-aabb-ccddeeff0011",
            },
          });
      } else if (path.endsWith("/mfa/complete")) {
        mfaRequests++;
      } else {
        throw new Error(`Unexpected fixture route: ${path}`);
      }
      return route.fulfill({
        status: 429,
        headers: { "Retry-After": "3" },
        json: {
          code: "rate_limit_exceeded",
          title: "Too many authentication requests",
        },
      });
    });
    await page.goto("/login");
    await page
      .getByRole("textbox", { name: "Employee code", exact: true })
      .fill("FIXTURE-USER");
    await page
      .getByRole("textbox", { name: "Password", exact: true })
      .fill("Fixture-only-password1!");
    await page.clock.install();
    const login = page.getByRole("button", {
      name: "Sign in securely",
      exact: true,
    });
    await login.click();
    const status = page.getByRole("status");
    await expect(status).toContainText("Try again in 3 seconds");
    await expect(login).toBeDisabled();
    // Synthetic submit verifies the handler guard as well as the disabled button.
    await page
      .locator("form")
      .evaluate((form) =>
        form.dispatchEvent(
          new Event("submit", { bubbles: true, cancelable: true }),
        ),
      );
    expect(logins).toBe(1);
    await page.screenshot({
      path: testInfo.outputPath(`retry-${theme}.png`),
      fullPage: true,
    });
    await page.clock.fastForward(3000);
    await expect(status).toHaveCount(0);
    await expect(login).toBeEnabled();
    expect(logins).toBe(1);
    await login.click();
    await page
      .getByRole("textbox", { name: "Authenticator code", exact: true })
      .fill("123456");
    const verify = page.getByRole("button", { name: "Verify", exact: true });
    await verify.click();
    await expect(status).toContainText("Try again in 3 seconds");
    await expect(verify).toBeDisabled();
    await page
      .locator("form")
      .evaluate((form) =>
        form.dispatchEvent(
          new Event("submit", { bubbles: true, cancelable: true }),
        ),
      );
    expect(mfaRequests).toBe(1);
    await page.clock.fastForward(3000);
    await expect(verify).toBeEnabled();
    expect(mfaRequests).toBe(1);
    expect(errors).toEqual([]);
  });
}
