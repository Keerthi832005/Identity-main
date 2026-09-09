import { HttpErrorResponse, HttpHeaders } from "@angular/common/http";
import { signal } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { ActivatedRoute, Router } from "@angular/router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { SessionService } from "../../core/auth/session.service";
import { RUNTIME_CONFIG } from "../../core/config/runtime-config";
import { ThemePreferenceService } from "../../core/theme/theme-preference.service";
import { SignInComponent } from "./sign-in.component";

class SignInHarness extends SignInComponent {
  credentials(): Promise<void> {
    this.employeeCode.set("FIXTURE-USER");
    this.password.set("Fixture-only-password1!");
    return this.submitCredentials();
  }
  mfa(): Promise<void> {
    this.code.set("123456");
    return this.submitMfa();
  }
  goBack(): void {
    this.back();
  }
  remaining(): number {
    return this.retry.remainingSeconds();
  }
  paste(event: ClipboardEvent): void {
    this.handleCredentialPaste(event);
  }
  filled(): { employeeCode: string; password: string } {
    return { employeeCode: this.employeeCode(), password: this.password() };
  }
}

describe("Sign-in throttling", () => {
  let component: SignInHarness;
  const session = {
    signIn: vi.fn(),
    completeMfa: vi.fn(),
    cancelMfa: vi.fn(),
  };
  beforeEach(() => {
    vi.useFakeTimers();
    session.signIn.mockReset();
    session.completeMfa.mockReset();
    TestBed.configureTestingModule({
      providers: [
        { provide: SessionService, useValue: session },
        {
          provide: Router,
          useValue: { navigateByUrl: vi.fn(async () => true) },
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: new Map() } },
        },
        {
          provide: RUNTIME_CONFIG,
          useValue: { applicationName: "Fixture", devSignIns: [] },
        },
        {
          provide: ThemePreferenceService,
          useValue: { resolved: signal("light"), mode: signal("light") },
        },
      ],
    });
    component = TestBed.runInInjectionContext(() => new SignInHarness());
  });
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.useRealTimers();
  });
  function throttled(): HttpErrorResponse {
    return new HttpErrorResponse({
      status: 429,
      headers: new HttpHeaders({ "Retry-After": "3" }),
    });
  }

  it("blocks repeated credential submissions and never automatically resends credentials", async () => {
    session.signIn.mockRejectedValue(throttled());
    await component.credentials();
    expect(component.remaining()).toBe(3);
    await component.credentials();
    expect(session.signIn).toHaveBeenCalledTimes(1);
    vi.advanceTimersByTime(3000);
    expect(component.remaining()).toBe(0);
    expect(session.signIn).toHaveBeenCalledTimes(1);
    session.signIn.mockResolvedValue({ requiresTwoFactor: true });
    await component.credentials();
    expect(session.signIn).toHaveBeenCalledTimes(2);
  });

  it("keeps the credential cooldown when moving back from MFA", async () => {
    session.completeMfa.mockRejectedValue(throttled());
    await component.mfa();
    await component.mfa();
    expect(session.completeMfa).toHaveBeenCalledTimes(1);
    component.goBack();
    await component.credentials();
    expect(session.signIn).not.toHaveBeenCalled();
    expect(component.remaining()).toBe(3);
  });

  it("guards concurrent submits even before the first request returns", async () => {
    let finish!: (value: { requiresTwoFactor: boolean }) => void;
    session.signIn.mockReturnValue(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    const pending = component.credentials();
    await component.credentials();
    expect(session.signIn).toHaveBeenCalledTimes(1);
    finish({ requiresTwoFactor: true });
    await pending;
  });

  function pasteEvent(text: string, target?: HTMLInputElement): ClipboardEvent {
    const event = new Event("paste", { cancelable: true });
    Object.defineProperties(event, {
      clipboardData: { value: { getData: () => text } },
      target: { value: target ?? document.body },
    });
    return event as ClipboardEvent;
  }

  it("fills a labelled clipboard pair without sending an authentication request", () => {
    const event = pasteEvent(
      "Username: FIXTURE-USER\nPassword: Fixture-only-password1!",
    );
    component.paste(event);
    expect(event.defaultPrevented).toBe(true);
    expect(component.filled()).toEqual({
      employeeCode: "FIXTURE-USER",
      password: "Fixture-only-password1!",
    });
    expect(session.signIn).not.toHaveBeenCalled();
  });

  it("handles document paste in the rendered form and keeps the password masked", async () => {
    vi.useRealTimers();
    const fixture = TestBed.createComponent(SignInComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    const event = pasteEvent(
      "Username: FIXTURE-USER\nPassword: Fixture-only-password1!",
    );
    document.dispatchEvent(event);
    fixture.detectChanges();
    await fixture.whenStable();
    const employee = fixture.nativeElement.querySelector(
      'input[name="employeeCode"]',
    ) as HTMLInputElement;
    const password = fixture.nativeElement.querySelector(
      'input[name="password"]',
    ) as HTMLInputElement;
    expect(event.defaultPrevented).toBe(true);
    expect(employee.value).toBe("FIXTURE-USER");
    expect(password.value).toBe("Fixture-only-password1!");
    expect(password.type).toBe("text");
    expect(password.closest("app-input")?.classList).toContain(
      "pts-input--secret",
    );
    expect(fixture.nativeElement.textContent).toContain(
      "Employee code and password filled from the clipboard",
    );
    expect(session.signIn).not.toHaveBeenCalled();
    fixture.destroy();
  });

  it.each(["password", "text"])(
    "preserves ordinary password paste in a %s editor",
    (type) => {
      const input = document.createElement("input");
      input.type = type;
      input.name = "password";
      const event = pasteEvent("Fixture:password,with|punctuation!", input);
      component.paste(event);
      expect(event.defaultPrevented).toBe(false);
      expect(component.filled()).toEqual({ employeeCode: "", password: "" });
      expect(session.signIn).not.toHaveBeenCalled();
    },
  );

  it("allows a spreadsheet pair pasted into the password editor", () => {
    const input = document.createElement("input");
    input.type = "password";
    const event = pasteEvent("FIXTURE-USER\tFixture-only-password1!", input);
    component.paste(event);
    expect(event.defaultPrevented).toBe(true);
    expect(component.filled().employeeCode).toBe("FIXTURE-USER");
    expect(session.signIn).not.toHaveBeenCalled();
  });

  it("does not replace credentials while authentication or MFA is in progress", async () => {
    let finish!: (value: { requiresTwoFactor: boolean }) => void;
    session.signIn.mockReturnValue(
      new Promise((resolve) => (finish = resolve)),
    );
    const pending = component.credentials();
    const busyPaste = pasteEvent("OTHER-USER\tAnother-fixture-password1!");
    component.paste(busyPaste);
    expect(busyPaste.defaultPrevented).toBe(false);
    finish({ requiresTwoFactor: true });
    await pending;
    const mfaPaste = pasteEvent("OTHER-USER\tAnother-fixture-password1!");
    component.paste(mfaPaste);
    expect(mfaPaste.defaultPrevented).toBe(false);
    expect(component.filled().employeeCode).toBe("FIXTURE-USER");
    expect(session.signIn).toHaveBeenCalledTimes(1);
  });
});
