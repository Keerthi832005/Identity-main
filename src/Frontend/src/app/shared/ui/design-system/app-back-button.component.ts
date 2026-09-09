import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from "@angular/core";
import { DxButtonModule } from "devextreme-angular/ui/button";

/**
 * The one way back out of a detail view. Each screen used to write its own - an anchor here, a
 * bare button there - so the affordance changed shape depending on where someone had navigated
 * from.
 */
@Component({
  selector: "app-back-button",
  standalone: true,
  imports: [DxButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dx-button
      [text]="text()"
      icon="arrowleft"
      stylingMode="outlined"
      [disabled]="disabled()"
      (onClick)="back.emit()"
    />
  `,
  styles: [
    `
      :host {
        display: inline-flex;
      }
    `,
  ],
})
export class AppBackButtonComponent {
  readonly text = input.required<string>();
  readonly disabled = input(false);
  readonly back = output<void>();
}
