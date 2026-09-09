import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
} from "@angular/core";

export type SkeletonShape = "text" | "row" | "card";

/** Loading placeholder. Announced as busy so screen readers are not left on stale content. */
@Component({
  selector: "app-skeleton",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: "status", "aria-busy": "true", "aria-label": "Loading" },
  template: `
    <div class="skeleton skeleton--{{ shape() }}">
      @for (width of widths(); track $index) {
        <span class="skeleton__bar" [style.width.%]="width"></span>
      }
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .skeleton {
        display: flex;
        flex-direction: column;
        gap: var(--app-space-3);
        padding: var(--app-space-4);
      }

      .skeleton__bar {
        animation: app-skeleton-shimmer 1.4s linear infinite;
        background: linear-gradient(
          90deg,
          var(--app-surface-muted) 25%,
          var(--app-border) 37%,
          var(--app-surface-muted) 63%
        );
        background-size: 400% 100%;
        border-radius: 6px;
        display: block;
        height: 12px;
      }

      .skeleton--row .skeleton__bar {
        height: 16px;
      }

      .skeleton--card .skeleton__bar {
        height: 22px;
      }

      @keyframes app-skeleton-shimmer {
        from {
          background-position: 100% 0;
        }
        to {
          background-position: 0 0;
        }
      }
    `,
  ],
})
export class AppSkeletonComponent {
  readonly shape = input<SkeletonShape>("text");
  readonly lines = input(4);

  /* Uneven widths read as content rather than as a progress bar. */
  protected readonly widths = computed(() => {
    const pattern = [92, 68, 84, 74, 88, 62];
    return Array.from(
      { length: Math.max(1, this.lines()) },
      (_, index) => pattern[index % pattern.length],
    );
  });
}
