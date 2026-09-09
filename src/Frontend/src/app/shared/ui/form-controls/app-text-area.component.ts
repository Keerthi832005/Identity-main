import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  Output,
  booleanAttribute,
  inject,
  signal,
  numberAttribute,
} from "@angular/core";
import { DxTextAreaModule } from "devextreme-angular";
import { createAppControlEvent } from "./app-control-event";
import { browserAutofillAttributes } from "../../../core/config/browser-autofill";
import {
  AppEditorInstance,
  clearValidationError,
  constraintMessage,
  fieldName,
  showValidationError,
  wireNativeValidation,
} from "./app-editor-validation";

@Component({
  selector: "app-text-area",
  standalone: true,
  imports: [DxTextAreaModule],
  templateUrl: "./app-text-area.component.html",
  styleUrl: "./app-form-control.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppTextAreaComponent implements AfterViewInit {
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);
  private readonly wiredControls = new WeakSet<Element>();
  private editor?: AppEditorInstance;
  /* Shown under the editor: the widget only reveals its own message on hover. */
  protected readonly validationError = signal<string | null>(null);

  @Input() value: string | null | undefined = "";
  @Input() placeholder = "";
  @Input({ transform: booleanAttribute }) disabled = false;
  @Input({ transform: booleanAttribute }) readonly = false;
  @Input({ transform: booleanAttribute }) required = false;
  @Input({ alias: "maxlength", transform: numberAttribute }) maxLength = 0;
  @Output() readonly input = new EventEmitter<Event>();
  @Output() readonly change = new EventEmitter<Event>();

  protected get inputAttributes(): Record<string, string> {
    return {
      ...browserAutofillAttributes(),
      ...(this.required
        ? { required: "required", "aria-required": "true" }
        : {}),
    };
  }

  /** The DevExtreme editor, so required feedback has a target. */
  protected captureEditor(editor: AppEditorInstance): void {
    this.editor = editor;
    queueMicrotask(() => this.wireEditorValidation());
  }

  private reportConstraint(
    control: HTMLInputElement | HTMLTextAreaElement,
  ): void {
    const message = constraintMessage(
      control,
      fieldName(this.host.nativeElement, ""),
    );
    this.validationError.set(message);
    showValidationError(this.editor, message);
  }

  ngAfterViewInit(): void {
    this.wireEditorValidation();
  }

  private wireEditorValidation(): void {
    wireNativeValidation(
      this.host.nativeElement,
      this.wiredControls,
      (control) => this.reportConstraint(control),
    );
  }

  protected emitValue(value: unknown, originalEvent: unknown): void {
    if (!originalEvent) return;
    // Answering the field resolves the required message it was showing.
    if (value !== null && value !== undefined && value !== "") {
      this.validationError.set(null);
      clearValidationError(this.editor);
    }
    const event = createAppControlEvent(value);
    this.input.emit(event);
    this.change.emit(event);
  }
}
