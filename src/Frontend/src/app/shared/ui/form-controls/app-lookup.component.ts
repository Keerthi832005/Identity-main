import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  signal,
} from "@angular/core";
import { DxSelectBoxModule } from "devextreme-angular/ui/select-box";
import { CustomStore, DataSource } from "devextreme/common/data";
import { browserAutofillAttributes } from "../../../core/config/browser-autofill";

export interface LookupOption {
  readonly value: number;
  readonly label: string;
}
export type LookupSearch = (search: string) => Promise<readonly LookupOption[]>;

/** Typing searches the server; only selecting an option changes the stored ID. */
@Component({
  selector: "app-lookup",
  imports: [DxSelectBoxModule],
  template: `
    <dx-select-box
      [dataSource]="source"
      valueExpr="value"
      displayExpr="label"
      [value]="value ?? 0"
      [disabled]="disabled"
      [inputAttr]="inputAttributes"
      [searchEnabled]="true"
      [searchTimeout]="250"
      [minSearchLength]="0"
      [showDataBeforeSearch]="true"
      [showClearButton]="true"
      [acceptCustomValue]="false"
      [noDataText]="
        loadError()
          ? 'Could not load options'
          : 'No matches. Try another search.'
      "
      (onValueChanged)="choose($event)"
      (onKeyDown)="$event.event?.stopPropagation()"
    />
    @if (loadError()) {
      <span class="control-error" role="alert">
        {{ ariaLabel }} could not be loaded. Your selection is kept.
        <button type="button" [disabled]="disabled" (click)="retry()">
          Retry {{ ariaLabel.toLowerCase() }}
        </button>
      </span>
    }
  `,
  styleUrl: "./app-form-control.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { "(click)": "preventLabelClick($event)" },
})
export class AppLookupComponent implements OnChanges, OnDestroy {
  protected preventLabelClick(event: MouseEvent): void {
    if (
      (event.target as Element | null)?.closest(".dx-dropdowneditor-button") &&
      (event.currentTarget as HTMLElement | null)?.closest("label")
    ) {
      // Keep the browser's implicit label activation from toggling the popup twice.
      event.preventDefault();
    }
  }
  @Input({ required: true }) searchOptions!: LookupSearch;
  @Input() value: number | null = null;
  @Input() selectedLabel: string | null = null;
  @Input() emptyLabel = "Not assigned";
  @Input() ariaLabel = "Selection";
  @Input() disabled = false;
  /** A changed parent invalidates pending results and selected-item caches. */
  @Input() contextKey: number | string | null = null;
  @Output() readonly valueChange = new EventEmitter<number | null>();
  protected readonly loadError = signal(false);
  protected source = this.createSource();
  private generation = 0;
  private requestVersion = 0;
  private readonly cache = new Map<number, LookupOption>();

  protected get inputAttributes(): Record<string, string> {
    return { ...browserAutofillAttributes(), "aria-label": this.ariaLabel };
  }
  ngOnChanges(changes: SimpleChanges): void {
    if (changes["contextKey"] && !changes["contextKey"].firstChange) {
      this.generation++;
      this.source.dispose();
      this.cache.clear();
      this.loadError.set(false);
      this.source = this.createSource();
    }
  }
  ngOnDestroy(): void {
    this.generation++;
    this.source.dispose();
  }

  private createSource(): DataSource<LookupOption, number> {
    return new DataSource<LookupOption, number>({
      paginate: false,
      store: new CustomStore<LookupOption, number>({
        key: "value",
        loadMode: "processed",
        useDefaultSearch: false,
        load: (options) => this.loadOptions(String(options.searchValue ?? "")),
        byKey: async (value) =>
          value === 0
            ? { value: 0, label: this.emptyLabel }
            : (this.cache.get(value) ?? {
                value,
                label: this.selectedLabel || `${this.ariaLabel} #${value}`,
              }),
      }),
    });
  }
  private async loadOptions(search: string): Promise<LookupOption[]> {
    const generation = this.generation,
      version = ++this.requestVersion;
    this.loadError.set(false);
    if (this.disabled) return [];
    try {
      const options = await this.searchOptions(search);
      if (generation !== this.generation || version !== this.requestVersion)
        return [];
      for (const option of options) this.cache.set(option.value, option);
      return search.trim()
        ? [...options]
        : [{ value: 0, label: this.emptyLabel }, ...options];
    } catch {
      if (generation === this.generation && version === this.requestVersion)
        this.loadError.set(true);
      return [];
    }
  }
  protected choose(event: { value?: number | null; event?: unknown }): void {
    // Programmatic updates and data loading must not erase a saved assignment.
    if (!event.event || this.disabled) return;
    const value = event.value || null;
    if (value !== this.value) this.valueChange.emit(value);
  }
  protected retry(): void {
    void this.source.reload();
  }
}
