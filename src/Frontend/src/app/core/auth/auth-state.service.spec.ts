import { TestBed } from "@angular/core/testing";
import { beforeEach, describe, expect, it } from "vitest";
import { AuthState } from "./auth-state.service";

function token(payload: object): string {
  return `header.${btoa(JSON.stringify(payload))}.signature`;
}

describe("AuthState", () => {
  let auth: AuthState;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    auth = TestBed.inject(AuthState);
  });

  it("derives display and capability state from a live token", () => {
    auth.setToken(
      token({
        display_name: "Identity Admin",
        employee_code: "ADMIN001",
        capability: ["iam.admin"],
        exp: Math.floor(Date.now() / 1000) + 300,
      }),
    );
    expect(auth.displayName()).toBe("Identity Admin");
    expect(auth.employeeCode()).toBe("ADMIN001");
    expect(auth.hasCapability("iam.admin")).toBe(true);
  });

  it("rejects an expired token", () => {
    auth.setToken(token({ exp: 1, capability: "iam.admin" }));
    expect(auth.token()).toBeNull();
    expect(auth.isAuthenticated()).toBe(false);
  });
});
