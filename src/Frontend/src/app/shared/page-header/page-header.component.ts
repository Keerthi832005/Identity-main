import { ChangeDetectionStrategy, Component, input } from "@angular/core";

/**
 * The page title block for a screen that cannot wrap its body in `app-page` - an inline editor
 * rendered above its own form. It draws the same eyebrow, title and subtitle as `app-page`, so a
 * form opened from a list does not announce itself in a different visual language than the list
 * it came from.
 */
@Component({
  selector: "app-page-header",
  standalone: true,
  template: `
    <header class="page-header">
      <div class="page-header__identity">
        <p class="page-header__eyebrow">{{ eyebrow() }}</p>
        <h1 class="page-header__title">{{ title() }}</h1>
        <p class="page-header__description">{{ description() }}</p>
        <ng-content select="[page-badge]" />
      </div>
      <div class="page-header__actions">
        <ng-content select="[page-actions]" />
      </div>
    </header>
  `,
  styleUrl: "./page-header.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PageHeaderComponent {
  readonly eyebrow = input.required<string>();
  readonly title = input.required<string>();
  readonly description = input.required<string>();
}
