import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  Output,
  OnChanges,
  SimpleChanges,
  ViewChild,
  booleanAttribute,
  inject,
  signal,
  numberAttribute,
} from "@angular/core";
import {
  DxAutocompleteModule,
  DxCheckBoxModule,
  DxDateBoxModule,
  DxNumberBoxComponent,
  DxNumberBoxModule,
  DxTextBoxModule,
} from "devextreme-angular";
import { createAppControlEvent } from "./app-control-event";
import { browserAutofillAttributes } from "../../../core/config/browser-autofill";
import {
  AppEditorInstance,
  clearEditorParseError,
  clearValidationError,
  constraintMessage,
  fieldName,
  showValidationError,
  wireNativeValidation,
} from "./app-editor-validation";

type AppInputType =
  | "checkbox"
  | "date"
  | "datetime-local"
  | "email"
  | "number"
  | "password"
  | "search"
  | "secret"
  | "tel"
  | "text"
  | "time";

@Component({
  selector: "app-input",
  standalone: true,
  imports: [
    DxAutocompleteModule,
    DxCheckBoxModule,
    DxDateBoxModule,
    DxNumberBoxModule,
    DxTextBoxModule,
  ],
  templateUrl: "./app-input.component.html",
  styleUrl: "./app-form-control.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    "[class.pts-input--checkbox]": "type === 'checkbox'",
    "[class.pts-input--secret]": "isPassword",
    "[class.pts-input--revealed]": "passwordVisible()",
  },
})
export class AppInputComponent implements AfterViewInit, OnChanges {
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);
  private readonly wiredControls = new WeakSet<Element>();
  private editor?: AppEditorInstance;
  /* Shown under the editor: the widget only reveals its own message on hover. */
  protected readonly validationError = signal<string | null>(null);

  @Input() value: string | number | null | undefined = "";
  @Input() type: AppInputType = "text";
  @Input() placeholder = "";
  @Input() name = "";
  @Input() inputmode = "";
  @Input() suggestions: readonly string[] = [];
  @Input({ transform: booleanAttribute }) disabled = false;
  @Input({ transform: booleanAttribute }) readonly = false;
  @Input({ transform: booleanAttribute }) required = false;
  @Input({ transform: booleanAttribute }) checked = false;
  @Input({ alias: "maxlength", transform: numberAttribute }) maxLength = 0;
  @Input({ transform: numberAttribute }) min = Number.NaN;
  @Input({ transform: numberAttribute }) max = Number.NaN;
  @Input({ transform: numberAttribute }) step = 1;
  @Input() ariaLabel = "";
  @Input() ariaLabelledby = "";
  @Input() ariaDescribedby = "";
  @Input() ariaInvalid = "";

  @Output() readonly input = new EventEmitter<Event>();
  @Output() readonly change = new EventEmitter<Event>();

  protected get isDate(): boolean {
    return (
      this.type === "date" ||
      this.type === "datetime-local" ||
      this.type === "time"
    );
  }

  protected get isNumber(): boolean {
    return this.type === "number";
  }

  protected get isCheckbox(): boolean {
    return this.type === "checkbox";
  }

  protected get useAutocomplete(): boolean {
    return (
      !this.isDate &&
      !this.isNumber &&
      !this.isCheckbox &&
      this.suggestions.length > 0
    );
  }

  /** Reveal state for password fields. New component instances always start hidden. */
  protected readonly passwordVisible = signal(false);

  /** Both masked types carry the reveal button. */
  protected get isPassword(): boolean {
    return this.type === "password" || this.type === "secret";
  }

  protected get textMode(): "email" | "password" | "search" | "tel" | "text" {
    /* Mask credentials in CSS while keeping the native input type as text. Chrome binds its saved
       credential popup to password inputs even when autocomplete is disabled. */
    if (this.isPassword) return "text";
    return ["email", "search", "tel"].includes(this.type)
      ? (this.type as "email" | "search" | "tel")
      : "text";
  }

  protected togglePasswordVisible(): void {
    this.passwordVisible.update((visible) => !visible);
  }

  /** Options object for the in-editor reveal button; recomputed so the icon and label follow state. */
  protected get revealButtonOptions(): Record<string, unknown> {
    const visible = this.passwordVisible();
    return {
      icon: visible ? "eyeclose" : "eyeopen",
      stylingMode: "text",
      hint: visible ? "Hide password" : "Show password",
      elementAttr: {
        "aria-label": visible ? "Hide password" : "Show password",
        class: "app-input__reveal",
      },
      onClick: () => this.togglePasswordVisible(),
    };
  }

  /* DevExtreme renders date editors with an empty box when no placeholder is
     supplied, which leaves the user guessing at the expected format. Fall back
     to the format the mask actually accepts (the default en-US short forms). */
  protected get resolvedPlaceholder(): string {
    if (this.placeholder) return this.placeholder;
    if (this.type === "date") return "M/D/YYYY";
    if (this.type === "datetime-local") return "M/D/YYYY, h:mm AM/PM";
    if (this.type === "time") return "h:mm AM/PM";
    return "";
  }

  protected get dateType(): "date" | "datetime" | "time" {
    return this.type === "datetime-local"
      ? "datetime"
      : (this.type as "date" | "time");
  }

  /* DxDateBox works in Date objects, but every caller binds the native input's string shape
     ("YYYY-MM-DD", "YYYY-MM-DDTHH:mm", "HH:mm"). Passing the raw string through made the widget
     call .getTime() on it and throw on blur, so parse inbound and re-format outbound. */
  protected get dateValue(): Date | null {
    const raw: unknown = this.value;
    if (raw === "" || raw === null || raw === undefined) return null;
    if (raw instanceof Date) return raw;
    const text = String(raw);
    const parsed =
      this.type === "time" ? new Date(`1970-01-01T${text}`) : new Date(text);
    return Number.isNaN(parsed.getTime()) ? null : parsed;
  }

  protected get numberValue(): number | null {
    if (this.value === "" || this.value === null || this.value === undefined)
      return null;
    const parsed = Number(this.value);
    return Number.isFinite(parsed) ? parsed : null;
  }

  protected get numberMin(): number | undefined {
    return Number.isFinite(this.min) ? this.min : undefined;
  }

  protected get numberMax(): number | undefined {
    return Number.isFinite(this.max) ? this.max : undefined;
  }

  /** The DevExtreme editor for the active branch of the template. */
  protected captureEditor(editor: AppEditorInstance): void {
    this.editor = editor;
    clearEditorParseError(editor);
    queueMicrotask(() => this.wireEditorValidation());
  }

  private reportConstraint(
    control: HTMLInputElement | HTMLTextAreaElement,
  ): void {
    const message = constraintMessage(
      control,
      fieldName(this.host.nativeElement, this.ariaLabel),
    );
    this.validationError.set(message);
    showValidationError(this.editor, message);
  }

  ngAfterViewInit(): void {
    this.wireEditorValidation();
    this.syncNativeAriaState();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes["value"] && !changes["value"].firstChange)
      queueMicrotask(() => clearEditorParseError(this.editor));
    if (changes["ariaInvalid"] || changes["ariaDescribedby"])
      queueMicrotask(() => this.syncNativeAriaState());
  }

  private syncNativeAriaState(): void {
    const control = this.host.nativeElement.querySelector("input");
    if (!control) return;
    this.setOptionalAttribute(control, "aria-invalid", this.ariaInvalid);
    this.setOptionalAttribute(
      control,
      "aria-describedby",
      this.ariaDescribedby,
    );
  }

  private setOptionalAttribute(
    control: HTMLInputElement,
    name: string,
    value: string,
  ): void {
    if (value) control.setAttribute(name, value);
    else control.removeAttribute(name);
  }

  private wireEditorValidation(): void {
    clearEditorParseError(this.editor);
    wireNativeValidation(
      this.host.nativeElement,
      this.wiredControls,
      (control) => this.reportConstraint(control),
    );
  }

  protected get inputAttributes(): Record<string, string> {
    return {
      ...(this.name ? { name: this.name } : {}),
      ...browserAutofillAttributes(),
      ...(this.isPassword ? { "data-secret": "true" } : {}),
      ...(this.inputmode ? { inputmode: this.inputmode } : {}),
      ...(this.required
        ? { required: "required", "aria-required": "true" }
        : {}),
      ...(this.ariaLabel || this.host.nativeElement.getAttribute("aria-label")
        ? {
            "aria-label":
              this.ariaLabel ||
              this.host.nativeElement.getAttribute("aria-label")!,
          }
        : {}),
      ...(this.ariaLabelledby
        ? { "aria-labelledby": this.ariaLabelledby }
        : {}),
      ...(this.ariaDescribedby
        ? { "aria-describedby": this.ariaDescribedby }
        : {}),
      ...(this.ariaInvalid ? { "aria-invalid": this.ariaInvalid } : {}),
    };
  }

  focus(): void {
    this.host.nativeElement.querySelector("input")?.focus();
  }

  protected emitValue(value: unknown, originalEvent: unknown): void {
    if (!originalEvent) return;
    // Answering the field resolves the required message it was showing.
    if (value !== null && value !== undefined && value !== "") {
      this.validationError.set(null);
      clearValidationError(this.editor);
    }
    const normalized = this.normalize(value);
    const event = createAppControlEvent(normalized, Boolean(value));
    this.input.emit(event);
    this.change.emit(event);
  }

  private normalize(value: unknown): unknown {
    if (!(value instanceof Date)) return value;
    const pad = (part: number) => String(part).padStart(2, "0");
    const date = `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`;
    if (this.type === "date") return date;
    const time = `${pad(value.getHours())}:${pad(value.getMinutes())}`;
    return this.type === "time" ? time : `${date}T${time}`;
  }
}
