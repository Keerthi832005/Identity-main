import { describe, expect, it } from "vitest";
import { normalizeRuntimeConfig } from "./runtime-config";

describe("runtime configuration", () => {
  it("accepts a same-origin identity path", () => {
    expect(
      normalizeRuntimeConfig({
        identityBaseUrl: "/identity/",
        identityClientId: "identity-admin-web",
        applicationName: "Identity Administration",
      }),
    ).toEqual({
      identityBaseUrl: "/identity",
      identityClientId: "identity-admin-web",
      applicationName: "Identity Administration",
      devExtremeLicenseKey: "",
    });
  });

  it("rejects cross-origin identity URLs", () => {
    expect(() =>
      normalizeRuntimeConfig({
        identityBaseUrl: "https://identity.example.test",
        identityClientId: "web",
        applicationName: "Identity",
      }),
    ).toThrow(/same-origin/);
  });
});
