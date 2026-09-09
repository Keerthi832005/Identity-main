import { describe, expect, it } from "vitest";
import { parseCredentialPaste } from "./credential-paste";

describe("Credential paste", () => {
  it("reads two spreadsheet cells", () => {
    expect(parseCredentialPaste("ADMIN-001\tLocalDev-Admin-2026!")).toEqual({
      employeeCode: "ADMIN-001",
      password: "LocalDev-Admin-2026!",
    });
  });

  it("reads two lines", () => {
    expect(parseCredentialPaste("ADMIN-001\nLocalDev-Admin-2026!")).toEqual({
      employeeCode: "ADMIN-001",
      password: "LocalDev-Admin-2026!",
    });
  });

  it("reads a labelled pair from a password manager", () => {
    const pasted = "Username: ADMIN-001\nPassword: LocalDev-Admin-2026!";

    expect(parseCredentialPaste(pasted)).toEqual({
      employeeCode: "ADMIN-001",
      password: "LocalDev-Admin-2026!",
    });
  });

  it("reads labels in either order and ignores unrelated lines", () => {
    const pasted =
      "Identity Administration\nPassword = Secret-Value-2026!\nEmp Code = ADMIN-001";

    expect(parseCredentialPaste(pasted)).toEqual({
      employeeCode: "ADMIN-001",
      password: "Secret-Value-2026!",
    });
  });

  it("reads a colon-delimited pair", () => {
    expect(parseCredentialPaste("ADMIN-001:LocalDev-Admin-2026!")).toEqual({
      employeeCode: "ADMIN-001",
      password: "LocalDev-Admin-2026!",
    });
  });

  it("keeps a separator that appears inside the password", () => {
    expect(parseCredentialPaste("ADMIN-001:a:b:c")).toEqual({
      employeeCode: "ADMIN-001",
      password: "a:b:c",
    });
  });

  it("keeps a password that contains spaces", () => {
    expect(parseCredentialPaste("ADMIN-001\tcorrect horse battery")).toEqual({
      employeeCode: "ADMIN-001",
      password: "correct horse battery",
    });
  });

  it("refuses a single value rather than guessing which field it is", () => {
    /* A partial guess would drop a password into the employee-code field, where it is neither
       masked nor discarded. */
    expect(parseCredentialPaste("ADMIN-001")).toBeNull();
    expect(parseCredentialPaste("LocalDev-Admin-2026!")).toBeNull();
  });

  it("refuses when the first half is not a plausible employee code", () => {
    expect(parseCredentialPaste("some sentence with spaces\tvalue")).toBeNull();
  });

  it("refuses empty halves and empty input", () => {
    expect(parseCredentialPaste("ADMIN-001\t")).toBeNull();
    expect(parseCredentialPaste("\tsecret")).toBeNull();
    expect(parseCredentialPaste("")).toBeNull();
    expect(parseCredentialPaste("   ")).toBeNull();
  });

  it("refuses a large block that is clearly not a credential pair", () => {
    expect(parseCredentialPaste("a\nb\nc\nd\ne\nf")).toBeNull();
  });
});
