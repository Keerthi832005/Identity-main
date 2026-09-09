import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from "@angular/core";

export interface ChipOption {
  readonly value: string;
  readonly label: string;
  /** Optional trailing count, shown as a superscript beside the label. */
  readonly count?: number | null;
}

/**
 * A short, fixed set of choices laid out as chips rather than hidden behind a drop-down.
 *
 * Used where the options are few and switching between them is the main thing someone does on the
 * screen: a drop-down costs two clicks and hides what the alternatives even are.
 */
@Component({
  selector: "app-chip-select",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: "./app-chip-select.component.html",
  styleUrl: "./app-chip-select.component.scss",
})
export class AppChipSelectComponent {
  readonly options = input.required<readonly ChipOption[]>();
  readonly value = input("");
  readonly label = input("");
  readonly disabled = input(false);
  readonly valueChange = output<string>();
}
