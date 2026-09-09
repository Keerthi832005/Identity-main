import { ChangeDetectionStrategy, Component, input } from "@angular/core";

/**
 * Covers already-rendered content during a refresh. The wrapped content keeps its layout, so the
 * page does not jump; `inert` stops it taking focus while the work is in flight.
 */
@Component({
  selector: "app-busy-overlay",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="busy">
      <div class="busy__content" [attr.inert]="busy() ? '' : null">
        <ng-content />
      </div>
      @if (busy()) {
        <div class="busy__veil" role="status" [attr.aria-label]="label()">
          <span class="busy__spinner" aria-hidden="true"></span>
          <span class="busy__label">{{ label() }}</span>
        </div>
      }
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .busy {
        position: relative;
      }

      .busy__veil {
        align-items: center;
        backdrop-filter: blur(1px);
        background: color-mix(in srgb, var(--app-surface) 68%, transparent);
        display: flex;
        flex-direction: column;
        gap: var(--app-space-2);
        inset: 0;
        justify-content: center;
        position: absolute;
        z-index: var(--app-z-overlay);
      }

      .busy__spinner {
        animation: app-busy-spin 700ms linear infinite;
        border: 2px solid var(--app-border);
        border-radius: 50%;
        border-top-color: var(--app-primary);
        height: 22px;
        width: 22px;
      }

      .busy__label {
        color: var(--app-text-secondary);
        font-size: var(--app-font-size-caption);
      }

      @keyframes app-busy-spin {
        to {
          transform: rotate(360deg);
        }
      }
    `,
  ],
})
export class AppBusyOverlayComponent {
  readonly busy = input(false);
  readonly label = input("Working…");
}
