import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from "@angular/core";
import { ActivatedRoute, Router } from "@angular/router";
import { DxButtonModule } from "devextreme-angular/ui/button";
import { SessionService } from "../../core/auth/session.service";
import { RetryCooldown } from "../../core/http/retry-cooldown";
import { RUNTIME_CONFIG } from "../../core/config/runtime-config";
import { ThemePreferenceService } from "../../core/theme/theme-preference.service";
import { AppFormControlsModule } from "../../shared/ui/form-controls/app-form-controls.module";
import { parseCredentialPaste } from "./credential-paste";

type SignInStep = "credentials" | "mfa";

@Component({
  selector: "app-sign-in",
  imports: [DxButtonModule, AppFormControlsModule],
  templateUrl: "./sign-in.component.html",
  styleUrl: "./sign-in.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  /* Document level: on a freshly loaded page nothing inside the form has focus, so a host
     listener would never see the paste until the user clicked first. */
  host: { "(document:paste)": "handleCredentialPaste($event)" },
})
export class SignInComponent {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly config = inject(RUNTIME_CONFIG);
  protected readonly theme = inject(ThemePreferenceService);
  protected readonly applicationName = this.config.applicationName;
  protected readonly step = signal<SignInStep>("credentials");
  protected readonly employeeCode = signal("");
  protected readonly password = signal("");
  protected readonly code = signal("");
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly retry = new RetryCooldown(inject(DestroyRef));
  protected readonly themeAction = computed(() =>
    this.theme.resolved() === "dark" ? "Use light theme" : "Use dark theme",
  );

  protected readonly pasteNotice = signal("");

  /**
   * Fills both fields from one pasted credential pair.
   *
   * It fills but never submits. An accidental paste that submitted would spend an authentication
   * attempt against the lockout budget, and a mis-parsed pair would spend it on a guaranteed
   * failure; signing in stays an explicit act.
   */
  protected handleCredentialPaste(event: ClipboardEvent): void {
    if (this.step() !== "credentials" || this.submitting()) return;

    const text = event.clipboardData?.getData("text/plain") ?? "";
    const target = event.target;
    // A password can itself contain ':' or ','. Leave single-value pastes in
    // this field to the browser, including when the password is revealed.
    if (
      target instanceof HTMLInputElement &&
      (target.type === "password" || target.name === "password") &&
      !/[\t\r\n]/.test(text)
    )
      return;
    const pasted = parseCredentialPaste(text);
    if (!pasted) return;

    event.preventDefault();
    this.employeeCode.set(pasted.employeeCode);
    this.password.set(pasted.password);
    this.error.set(null);
    this.pasteNotice.set(
      "Employee code and password filled from the clipboard. Check them, then sign in.",
    );
    setTimeout(() => this.focusSubmit());
  }

  private focusSubmit(): void {
    document
      .querySelector<HTMLElement>(".primary-action .dx-button-content")
      ?.closest<HTMLElement>(".dx-button")
      ?.focus();
  }

  protected async submitCredentials(): Promise<void> {
    this.pasteNotice.set("");
    if (this.submitting() || this.retry.remainingSeconds() > 0) return;
    if (!this.employeeCode().trim() || this.password().length < 12) {
      this.error.set(
        "Enter your employee code and password (minimum 12 characters).",
      );
      return;
    }
    await this.run(async () => {
      const result = await this.session.signIn(
        this.employeeCode().trim(),
        this.password(),
      );
      if (result.requiresTwoFactor) this.step.set("mfa");
      else await this.navigateAfterLogin();
    });
  }

  protected async submitMfa(): Promise<void> {
    if (this.submitting() || this.retry.remainingSeconds() > 0) return;
    if (!/^\d{6}$/.test(this.code())) {
      this.error.set("Enter the six-digit code from your authenticator app.");
      return;
    }
    await this.run(async () => {
      await this.session.completeMfa(this.code());
      await this.navigateAfterLogin();
    });
  }

  protected back(): void {
    this.session.cancelMfa();
    this.code.set("");
    this.error.set(null);
    this.step.set("credentials");
  }

  private async run(action: () => Promise<void>): Promise<void> {
    if (this.submitting() || this.retry.remainingSeconds() > 0) return;
    this.submitting.set(true);
    this.error.set(null);
    try {
      await action();
    } catch (failure) {
      if (this.retry.capture(failure)) return;
      const problem = failure as {
        error?: { detail?: string; title?: string };
      };
      this.error.set(
        problem.error?.detail ??
          problem.error?.title ??
          "Sign-in failed. Check your credentials.",
      );
    } finally {
      this.submitting.set(false);
    }
  }

  private navigateAfterLogin(): Promise<boolean> {
    const returnUrl = this.route.snapshot.queryParamMap.get("returnUrl");
    return this.router.navigateByUrl(
      returnUrl?.startsWith("/") ? returnUrl : "/",
    );
  }
}
