import { defineConfig } from "@playwright/test";
const database = process.env["IAM_E2E_DATABASE"];
const proxy = process.env["IAM_E2E_PROXY_PATH"];
const port = Number(process.env["IAM_E2E_WEB_PORT"]);
if (
  !database ||
  !/^FIN_IAM_OrgBrowserTests_[0-9]{8}_[a-f0-9]{8}$/.test(database) ||
  !proxy ||
  !Number.isInteger(port) ||
  port < 1024 ||
  port > 65535 ||
  !process.env["IAM_E2E_PASSWORD"]
)
  throw new Error(
    "Run IAM/scripts/Test-OrganizationBrowser.ps1 with its isolated test environment.",
  );
export default defineConfig({
  testDir: "./e2e-live",
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 120000,
  outputDir: "test-results/organization-live",
  use: {
    baseURL: `http://127.0.0.1:${port}`,
    trace: "off",
    video: "off",
    screenshot: "off",
  },
  webServer: {
    command: `npm start -- --host 127.0.0.1 --port ${port} --proxy-config "${proxy}"`,
    url: `http://127.0.0.1:${port}`,
    reuseExistingServer: false,
    timeout: 120000,
  },
});
