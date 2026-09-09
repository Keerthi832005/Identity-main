import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Input,
  inject,
} from "@angular/core";

@Component({
  selector: "app-option",
  standalone: true,
  template: "<ng-content />",
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { hidden: "true" },
})
export class AppOptionComponent {
  private readonly host = inject(ElementRef<HTMLElement>);

  @Input({ required: true }) value: string | number | null = "";
  @Input() label = "";
  /** Optional DevExtreme icon name, rendered before the label in the field and the dropdown. */
  @Input() icon = "";

  resolvedLabel(): string {
    return (
      this.label ||
      this.host.nativeElement.textContent?.trim() ||
      String(this.value ?? "")
    );
  }
}
