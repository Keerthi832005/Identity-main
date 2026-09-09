import {
  AfterContentInit,
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ContentChildren,
  DestroyRef,
  ElementRef,
  EventEmitter,
  Input,
  Output,
  QueryList,
  booleanAttribute,
  inject,
  signal,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { DxSelectBoxModule, DxTextBoxModule } from "devextreme-angular";
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
import { AppOptionComponent } from "./app-option.component";

interface AppSelectItem {
  readonly value: string | number | null;
  readonly label: string;
  readonly icon: string;
}

@Component({
  selector: "app-select",
  standalone: true,
  imports: [DxSelectBoxModule, DxTextBoxModule],
  templateUrl: "./app-select.component.html",
  styleUrl: "./app-form-control.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { "(click)": "preventLabelClick($event)" },
})
export class AppSelectComponent implements AfterContentInit, AfterViewInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);
  private readonly wiredControls = new WeakSet<Element>();
  private editor?: AppEditorInstance;
  /* Shown under the editor: the widget only reveals its own message on hover. */
  protected readonly validationError = signal<string | null>(null);

  @ContentChildren(AppOptionComponent)
  private options!: QueryList<AppOptionComponent>;
  @Input() value: string | number | null | undefined = "";
  @Input() placeholder = "";
  @Input({ transform: booleanAttribute }) disabled = false;
  @Input({ transform: booleanAttribute }) required = false;
  @Input() ariaLabel = "";
  @Output() readonly change = new EventEmitter<Event>();

  protected readonly items = signal<AppSelectItem[]>([]);

  protected preventLabelClick(event: MouseEvent): void {
    if (
      (event.target as Element | null)?.closest(".dx-dropdowneditor-button") &&
      this.host.nativeElement.closest("label")
    ) {
      // A label otherwise forwards the arrow click to the input and toggles it closed again.
      event.preventDefault();
    }
  }

  protected get inputAttributes(): Record<string, string> {
    return {
      ...browserAutofillAttributes(),
      ...(this.ariaLabel ? { "aria-label": this.ariaLabel } : {}),
      ...(this.required
        ? { required: "required", "aria-required": "true" }
        : {}),
    };
  }

  ngAfterContentInit(): void {
    this.syncItems();
    this.options.changes
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.syncItems());
    queueMicrotask(() => this.syncItems());
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
      fieldName(this.host.nativeElement, this.ariaLabel),
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

  protected emitValue(value: unknown): void {
    if (value === this.value) return;
    // Answering the field resolves the required message it was showing.
    if (value !== null && value !== undefined && value !== "") {
      this.validationError.set(null);
      clearValidationError(this.editor);
    }
    this.change.emit(createAppControlEvent(value));
  }

  /** Icon templates cost a render pass, so they are only wired when an option actually has one. */
  protected hasIcons(): boolean {
    return this.items().some((item) => !!item.icon);
  }

  private syncItems(): void {
    this.items.set(
      this.options.map((option) => ({
        value: option.value,
        label: option.resolvedLabel(),
        icon: option.icon,
      })),
    );
  }
}
