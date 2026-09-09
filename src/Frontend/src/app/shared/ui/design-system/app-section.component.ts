import { ChangeDetectionStrategy, Component, input } from "@angular/core";

/** Bordered surface with a heading bar, an action slot and projected content. */
@Component({
  selector: "app-section",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="section">
      <header class="section__head">
        <h2 class="section__heading">{{ heading() }}</h2>
        @if (caption()) {
          <p class="section__caption">{{ caption() }}</p>
        }
        <div class="section__actions">
          <ng-content select="[section-actions]" />
        </div>
      </header>
      <div class="section__body" [class.section__body--flush]="flush()">
        <ng-content />
      </div>
    </section>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .section {
        background: var(--app-surface);
        border: 1px solid var(--app-border);
        border-radius: var(--app-radius-md);
        box-shadow: var(--app-shadow-sm);
        overflow: hidden;
      }

      .section__head {
        align-items: center;
        border-bottom: 1px solid var(--app-border);
        display: flex;
        flex-wrap: wrap;
        gap: var(--app-space-2) var(--app-space-3);
        min-height: 52px;
        padding: var(--app-space-3) var(--app-space-4);
      }

      .section__heading {
        font-family: var(--app-font-display);
        font-size: var(--app-font-size-subheading);
        font-weight: 600;
        margin: 0;
      }

      .section__caption {
        color: var(--app-text-secondary);
        font-size: var(--app-font-size-caption);
        margin: 0;
      }

      .section__actions {
        align-items: center;
        display: flex;
        flex-wrap: wrap;
        gap: var(--app-space-2);
        margin-left: auto;
      }

      .section__actions:not(:has(*)) {
        display: none;
      }

      .section__body {
        padding: var(--app-space-4);
      }

      /* Grids and lists draw their own edge-to-edge padding. */
      .section__body--flush {
        padding: 0;
      }
    `,
  ],
})
export class AppSectionComponent {
  readonly heading = input.required<string>();
  readonly caption = input("");
  /** Removes body padding for content that manages its own insets, such as a grid. */
  readonly flush = input(false);
}
