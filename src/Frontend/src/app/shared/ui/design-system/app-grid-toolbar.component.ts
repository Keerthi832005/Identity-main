import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  afterNextRender,
  inject,
  input,
  linkedSignal,
  output,
  signal,
} from "@angular/core";
import { DxButtonModule } from "devextreme-angular/ui/button";
import type { DxDataGridComponent } from "devextreme-angular/ui/data-grid";
import type { Column } from "devextreme/ui/data_grid";
import { AppFormControlsModule } from "../form-controls/app-form-controls.module";

/** One active grid filter, rendered as a removable chip. */
export interface GridFilterChip {
  readonly key: string;
  readonly field: string;
  readonly kind: "search" | "filterValue" | "filterValues";
  readonly label: string;
  readonly value: string;
}

type GridInstance = {
  on(name: string, handler: () => void): void;
  off(name: string, handler: () => void): void;
  option(name: string): unknown;
  option(name: string, value: unknown): void;
  columnOption(id: string, name: string, value: unknown): void;
  getVisibleColumns(): Column[];
  showColumnChooser(): void;
  clearFilter(): void;
};

const OPERATIONS: Record<string, string> = {
  "=": "is",
  "<>": "is not",
  ">": "after",
  ">=": "from",
  "<": "before",
  "<=": "up to",
  contains: "contains",
  notcontains: "excludes",
  startswith: "starts with",
  endswith: "ends with",
  between: "between",
};

/**
 * The control row every `dx-data-grid` in the product wears: a fixed slot order of heading,
 * projected search, then the column chooser, with active filters restated underneath as chips
 * that can be removed one at a time. DevExtreme's own toolbar is switched off by the grid preset,
 * so the chooser holds one position instead of drifting below whatever search a screen supplies.
 */
@Component({
  selector: "app-grid-toolbar",
  standalone: true,
  imports: [DxButtonModule, AppFormControlsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: "./app-grid-toolbar.component.html",
  styleUrl: "./app-grid-toolbar.component.scss",
})
export class AppGridToolbarComponent {
  /** The grid this toolbar drives; pass the `dx-data-grid` template reference. */
  readonly for = input.required<DxDataGridComponent>();
  readonly heading = input("");
  readonly count = input<number | string | null>(null);
  /**
   * `client` filters the loaded rows through the grid's own search; `server` leaves the grid alone
   * and emits the term for the screen to reload with. Either way the search runs as someone types
   * - a button that has to be pressed to apply a filter is a step nobody expects to need.
   */
  readonly searchMode = input<"none" | "client" | "server">("none");
  readonly searchPlaceholder = input("Search this table");
  /** Server mode: the term the screen currently holds, so the box survives a reload. */
  readonly searchTerm = input("");
  readonly searchTermChange = output<string>();
  /** Off for a grid whose columns are all always wanted, such as a narrow picker panel. */
  readonly chooser = input(true);

  protected readonly chips = signal<readonly GridFilterChip[]>([]);
  /* Local while someone types, reset when the screen changes the term underneath - a filter
     cleared elsewhere, or a route restored from a link.
     An effect mirroring the input into a plain signal lost the typed text whenever the toolbar was
     rebuilt: the fresh instance started empty and the input it mirrored had not changed since, so
     nothing put the text back. A linkedSignal fixes that, but a bare one introduces a race - the
     screen echoes our own term back asynchronously, and by then someone may have typed further, so
     the echo would overwrite the newer keystrokes. Ignoring an echo of what we last emitted keeps
     both properties. */
  protected readonly searchText = linkedSignal<string, string>({
    source: () => this.searchTerm(),
    computation: (term, previous) =>
      nextSearchText(term, previous?.value, this.lastEmitted),
  });
  /** Legacy alias for searchMode="client"; the last screens using it move to searchMode next. */
  readonly searchable = input(false);
  private readonly destroyRef = inject(DestroyRef);
  private searchTimer?: ReturnType<typeof setTimeout>;
  private lastEmitted = "";

  constructor() {
    afterNextRender(() => this.attach());
    this.destroyRef.onDestroy(() => clearTimeout(this.searchTimer));
  }

  protected get showsSearch(): boolean {
    return this.searchMode() !== "none" || this.searchable();
  }

  private instance(): GridInstance | null {
    return (this.for()?.instance as unknown as GridInstance) ?? null;
  }

  private attach(): void {
    const grid = this.instance();
    if (!grid) return;
    const refresh = (): void => this.refresh();
    grid.on("contentReady", refresh);
    grid.on("optionChanged", refresh);
    this.destroyRef.onDestroy(() => {
      grid.off("contentReady", refresh);
      grid.off("optionChanged", refresh);
    });
    this.refresh();
  }

  private refresh(): void {
    const grid = this.instance();
    if (!grid) return;
    const search = String(grid.option("searchPanel.text") ?? "");
    if (
      this.searchMode() !== "server" &&
      search.trim() !== this.searchText().trim()
    )
      this.searchText.set(search);
    const next = buildFilterChips(search, grid.getVisibleColumns());
    if (!sameChips(this.chips(), next)) this.chips.set(next);
  }

  protected remove(chip: GridFilterChip): void {
    const grid = this.instance();
    if (!grid) return;
    if (chip.kind === "search") {
      grid.option("searchPanel.text", "");
      this.searchText.set("");
    } else if (chip.kind === "filterValue")
      grid.columnOption(chip.field, "filterValue", null);
    else grid.columnOption(chip.field, "filterValues", null);
    this.refresh();
  }

  protected applySearch(value: string): void {
    this.searchText.set(value);
    if (this.searchMode() === "server") {
      /* Debounced: every keystroke would otherwise be a request. */
      clearTimeout(this.searchTimer);
      this.searchTimer = setTimeout(() => this.emitTerm(value), 300);
      return;
    }
    this.instance()?.option("searchPanel.text", value);
  }

  /** Enter applies the term now rather than waiting out the debounce. */
  protected submitSearch(): void {
    if (this.searchMode() !== "server") return;
    clearTimeout(this.searchTimer);
    this.emitTerm(this.searchText());
  }

  private emitTerm(value: string): void {
    this.lastEmitted = value;
    this.searchTermChange.emit(value);
  }

  protected clearAll(): void {
    const grid = this.instance();
    if (!grid) return;
    grid.clearFilter();
    grid.option("searchPanel.text", "");
    this.searchText.set("");
    this.refresh();
  }

  protected chooseColumns(): void {
    this.instance()?.showColumnChooser();
  }
}

/** Restates the grid's own filter state as chips. Exported so it can be tested without a grid. */
export function buildFilterChips(
  search: string,
  columns: readonly Column[],
): GridFilterChip[] {
  const chips: GridFilterChip[] = [];
  if (search.trim())
    chips.push({
      key: "search",
      field: "",
      kind: "search",
      label: "Search",
      value: search,
    });

  for (const column of columns) {
    const field = column.dataField ?? column.name;
    if (!field) continue;
    const label = column.caption ?? field;
    const single = (column as { filterValue?: unknown }).filterValue;
    if (single !== undefined && single !== null && single !== "") {
      const operation = OPERATIONS[String(column.selectedFilterOperation)];
      chips.push({
        key: `filterValue:${field}`,
        field,
        kind: "filterValue",
        label,
        value: `${operation ?? ""} ${format(single)}`.trim(),
      });
    }

    const many = (column as { filterValues?: unknown }).filterValues;
    if (Array.isArray(many) && many.length)
      chips.push({
        key: `filterValues:${field}`,
        field,
        kind: "filterValues",
        label,
        value: many.map(format).join(", "),
      });
  }

  return chips;
}

/**
 * What the search box should show when the screen reports a term.
 *
 * An echo of the term this toolbar last emitted is ignored, because the screen reports it back
 * asynchronously and by then someone may have typed further; adopting it would undo those
 * keystrokes. Any other term is the screen genuinely changing the search - a filter cleared
 * elsewhere, or a route restored from a link - and wins.
 */
export function nextSearchText(
  term: string,
  previous: string | undefined,
  lastEmitted: string,
): string {
  return term === lastEmitted && previous !== undefined ? previous : term;
}

function format(value: unknown): string {
  if (value instanceof Date) return value.toLocaleDateString();
  if (value === true) return "Yes";
  if (value === false) return "No";
  return String(value);
}

function sameChips(
  left: readonly GridFilterChip[],
  right: readonly GridFilterChip[],
): boolean {
  return (
    left.length === right.length &&
    left.every(
      (chip, index) =>
        chip.key === right[index].key && chip.value === right[index].value,
    )
  );
}
