import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from "@angular/core";
import { DxButtonModule } from "devextreme-angular";

/** Failure presentation. The correlation id is always shown so a report can be traced to a request. */
@Component({
  selector: "app-error-state",
  standalone: true,
  imports: [DxButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="state state--error" role="alert">
      <i class="dx-icon dx-icon-warning" aria-hidden="true"></i>
      <h3 class="state__title">{{ title() }}</h3>
      <p class="state__message">{{ message() }}</p>
      @if (correlationId()) {
        <p class="state__reference">Reference {{ correlationId() }}</p>
      }
      <div class="state__actions">
        <dx-button
          stylingMode="contained"
          type="default"
          [text]="retryLabel()"
          (onClick)="retry.emit()"
        />
        <ng-content />
      </div>
    </div>
  `,
  styleUrl: "./app-state.component.scss",
})
export class AppErrorStateComponent {
  readonly title = input("Something went wrong");
  readonly message = input.required<string>();
  readonly correlationId = input("");
  readonly retryLabel = input("Try again");
  readonly retry = output<void>();
}
