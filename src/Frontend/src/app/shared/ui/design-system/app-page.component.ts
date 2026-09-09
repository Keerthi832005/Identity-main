import { ChangeDetectionStrategy, Component, input } from "@angular/core";

/** Page scaffold: title block, action slot and a sticky toolbar slot above the page body. */
@Component({
  selector: "app-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page__head">
        <div class="page__identity">
          @if (eyebrow()) {
            <p class="page__eyebrow">{{ eyebrow() }}</p>
          }
          <h1 class="page__title">{{ title() }}</h1>
          @if (subtitle()) {
            <p class="page__subtitle">{{ subtitle() }}</p>
          }
        </div>
        <div class="page__actions">
          <ng-content select="[page-actions]" />
        </div>
      </header>
      <div class="page__toolbar">
        <ng-content select="[page-toolbar]" />
      </div>
      <div class="page__body">
        <ng-content />
      </div>
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .page {
        display: flex;
        flex-direction: column;
        gap: var(--app-space-4);
      }

      .page__head {
        align-items: flex-start;
        display: flex;
        flex-wrap: wrap;
        gap: var(--app-space-4);
        justify-content: space-between;
      }

      .page__identity {
        min-width: 0;
      }

      .page__eyebrow {
        color: var(--app-accent-text);
        font-family: var(--app-font-display);
        font-size: var(--app-font-size-caption);
        font-weight: 600;
        letter-spacing: var(--app-letter-spacing-label);
        margin: 0 0 var(--app-space-1);
        text-transform: uppercase;
      }

      .page__title {
        font-family: var(--app-font-display);
        font-size: var(--app-font-size-title);
        font-weight: 700;
        line-height: var(--app-line-height-tight);
        margin: 0;
        text-wrap: balance;
      }

      .page__subtitle {
        color: var(--app-text-secondary);
        font-size: var(--app-font-size-supporting);
        line-height: var(--app-line-height-body);
        margin: var(--app-space-1) 0 0;
        max-width: 58ch;
      }

      .page__actions {
        align-items: center;
        display: flex;
        flex-wrap: wrap;
        gap: var(--app-space-2);
      }

      .page__toolbar {
        background: var(--app-canvas);
        padding-block: var(--app-space-1);
        position: sticky;
        top: 0;
        z-index: var(--app-z-sticky);
      }

      /* Collapse both optional slots so a page without them keeps the same rhythm as one with. */
      .page__toolbar:not(:has(*)),
      .page__actions:not(:has(*)) {
        display: none;
      }

      .page__body {
        display: flex;
        flex-direction: column;
        gap: var(--app-space-4);
        min-width: 0;
      }
    `,
  ],
})
export class AppPageComponent {
  readonly title = input.required<string>();
  readonly eyebrow = input("");
  readonly subtitle = input("");
}
