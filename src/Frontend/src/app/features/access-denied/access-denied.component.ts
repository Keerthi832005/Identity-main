import { ChangeDetectionStrategy, Component, inject } from "@angular/core";
import { Router } from "@angular/router";
import { DxButtonModule } from "devextreme-angular";
import { SessionService } from "../../core/auth/session.service";
import { AppErrorStateComponent } from "../../shared/ui/design-system/app-error-state.component";
import { AppPageComponent } from "../../shared/ui/design-system/app-page.component";

/** Authorization failure presented deliberately, on the same scaffold as every other screen. */
@Component({
  selector: "app-access-denied",
  standalone: true,
  imports: [AppErrorStateComponent, AppPageComponent, DxButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page
      eyebrow="Authorization"
      title="You do not have access to this workspace"
      subtitle="Your account is signed in, but it is not granted the identity administration capability."
    >
      <app-error-state
        title="Administration access is required"
        message="Ask an existing identity administrator to grant your account the iam.admin capability, then sign in again."
        retryLabel="Sign in as another user"
        (retry)="signOut()"
      />
    </app-page>
  `,
  styles: [
    `
      :host {
        display: block;
        padding: var(--app-space-5);
      }
    `,
  ],
})
export class AccessDeniedComponent {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);

  protected async signOut(): Promise<void> {
    await this.session.logout();
    await this.router.navigateByUrl("/login");
  }
}
