import { ChangeDetectionStrategy, Component, input } from "@angular/core";

/** Empty result presentation. Callers project a recovery action; a bare message is never enough. */
@Component({
  selector: "app-empty-state",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="state" role="status">
      <i class="dx-icon dx-icon-{{ icon() }}" aria-hidden="true"></i>
      <h3 class="state__title">{{ title() }}</h3>
      @if (message()) {
        <p class="state__message">{{ message() }}</p>
      }
      <div class="state__actions"><ng-content /></div>
    </div>
  `,
  styleUrl: "./app-state.component.scss",
})
export class AppEmptyStateComponent {
  readonly title = input.required<string>();
  readonly message = input("");
  readonly icon = input("box");
}
