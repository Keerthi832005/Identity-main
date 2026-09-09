import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
} from "@angular/core";

export type AlertTone = "info" | "success" | "warning" | "danger";

/** Inline message band. Danger and warning announce assertively; the quieter tones do not. */
@Component({
  selector: "app-inline-alert",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="alert alert--{{ tone() }}"
      [attr.role]="role()"
      [attr.aria-live]="live()"
    >
      <i class="dx-icon dx-icon-{{ icon() }}" aria-hidden="true"></i>
      <div class="alert__body">
        @if (heading()) {
          <strong class="alert__heading">{{ heading() }}</strong>
        }
        <ng-content />
      </div>
      <div class="alert__actions">
        <ng-content select="[alert-actions]" />
      </div>
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .alert {
        align-items: center;
        border: 1px solid;
        border-radius: var(--app-radius-md);
        display: flex;
        font-size: var(--app-font-size-supporting);
        gap: var(--app-space-3);
        line-height: var(--app-line-height-body);
        padding: var(--app-space-3) var(--app-space-4);
      }

      .alert > .dx-icon {
        flex: none;
        font-size: 22px;
        height: 22px;
        line-height: 22px;
        width: 22px;
      }

      .alert__body {
        min-width: 0;
      }

      .alert__heading {
        display: block;
        font-weight: 600;
      }

      .alert__actions {
        align-items: center;
        display: flex;
        gap: var(--app-space-2);
        margin-left: auto;
      }

      .alert__actions:not(:has(*)) {
        display: none;
      }

      /* Neutral, not accent-tinted. The brand accent is a red, so an accent-soft information band
         is read as a failure; only warning and danger may carry alarm. */
      .alert--info {
        background: var(--app-surface-muted);
        border-color: var(--app-border-strong);
      }

      .alert--info .dx-icon {
        color: var(--app-text-secondary);
      }

      .alert--success {
        background: var(--app-success-soft);
        border-color: var(--app-success-border);
      }

      .alert--warning {
        background: var(--app-warning-soft);
        border-color: var(--app-warning-border);
      }

      .alert--danger {
        background: var(--app-danger-soft);
        border-color: var(--app-danger-border);
      }

      .alert--danger .dx-icon {
        color: var(--app-danger);
      }
    `,
  ],
})
export class AppInlineAlertComponent {
  readonly tone = input<AlertTone>("info");
  readonly heading = input("");

  protected readonly icon = computed(
    () =>
      ({
        info: "info",
        success: "check",
        warning: "warning",
        danger: "warning",
      })[this.tone()],
  );

  protected readonly role = computed(() =>
    this.tone() === "danger" ? "alert" : "status",
  );

  protected readonly live = computed(() =>
    this.tone() === "danger" ? "assertive" : "polite",
  );
}
