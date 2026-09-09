import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  EmbeddedViewRef,
  ViewContainerRef,
  booleanAttribute,
  computed,
  contentChildren,
  inject,
  input,
  numberAttribute,
  output,
  viewChild,
} from "@angular/core";
import {
  DxDataGridComponent,
  DxDataGridModule,
} from "devextreme-angular/ui/data-grid";
import type { Column } from "devextreme/ui/data_grid";
import {
  AppGridPresetDirective,
  GridDensity,
} from "./app-grid-preset.directive";
import {
  AppGridCellContext,
  AppGridCellDirective,
  GridCellInfo,
} from "./app-grid-cell.directive";
import { AppGridToolbarComponent } from "./app-grid-toolbar.component";
import { AppSkeletonComponent } from "./app-skeleton.component";

export type GridSelectionMode = "none" | "single" | "multiple";

/**
 * The one tabular surface in the product. A screen supplies rows, columns and the handful of
 * decisions that genuinely differ between screens - paging, selection, density - and gets the
 * toolbar, filter chips, column chooser, filter row, header filter, empty and loading states
 * already wired. Nothing else configures a `dx-data-grid` directly.
 *
 * Cell renderers are declared with `*appGridCell`, named to match a column's `cellTemplate`; the
 * grid stamps them into DevExtreme's cell containers, because DevExtreme's own `*dxTemplate`
 * cannot cross a component boundary. A screen-owned search box goes in the `toolbar-search` slot,
 * extra buttons in `toolbar-actions`, and a server-paged footer in `grid-footer`.
 */
@Component({
  selector: "app-data-grid",
  standalone: true,
  imports: [
    DxDataGridModule,
    AppGridPresetDirective,
    AppGridToolbarComponent,
    AppSkeletonComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: "./app-data-grid.component.html",
  styleUrl: "./app-data-grid.component.scss",
})
export class AppDataGridComponent {
  readonly rows = input.required<readonly unknown[]>();
  readonly columns = input<readonly Column[]>([]);
  readonly keyExpr = input("");
  readonly heading = input("");
  readonly ariaLabel = input("");
  readonly count = input<number | string | null>(null);

  readonly emptyText = input("Nothing to show yet.");
  readonly loadingText = input("Loading…");
  readonly loading = input(false, { transform: booleanAttribute });
  readonly disabled = input(false, { transform: booleanAttribute });
  /** Skeleton rows drawn in place of the grid on a first load. */
  readonly skeletonRows = input(6, { transform: numberAttribute });

  readonly toolbar = input(true, { transform: booleanAttribute });
  readonly searchMode = input<"none" | "client" | "server">("none");
  readonly searchTerm = input("");
  readonly searchTermChange = output<string>();
  readonly searchPlaceholder = input("Search this table");
  readonly chooser = input(true, { transform: booleanAttribute });

  readonly density = input<GridDensity>("comfortable");
  readonly flush = input(false, { transform: booleanAttribute });
  readonly height = input("");
  readonly wordWrap = input(true, { transform: booleanAttribute });
  readonly autoWidth = input(false, { transform: booleanAttribute });
  readonly resizeMode = input<"nextColumn" | "widget">("nextColumn");

  readonly sortable = input(false, { transform: booleanAttribute });
  readonly filterable = input(true, { transform: booleanAttribute });
  /** 0 disables the client pager, for a screen that pages on the server. */
  readonly pageSize = input(0, { transform: numberAttribute });
  readonly pageSizes = input<number[]>([10, 25, 50, 100]);
  readonly infoText = input("Page {0} of {1} · {2} rows");

  readonly selection = input<GridSelectionMode>("none");
  readonly selectedKeys = input<readonly unknown[]>([]);

  readonly rowClick = output<unknown>();
  readonly selectionChange = output<readonly unknown[]>();

  /* DevExtreme's typings take mutable arrays; the inputs stay readonly so a screen cannot mutate
     grid state from the outside. The casts buy that guarantee at the boundary. */
  protected get dataSource(): unknown[] {
    return this.rows() as unknown[];
  }

  /* Undefined, not an empty array: DevExtreme reads the columns off the data when none are given,
     and an empty array would instead render a grid with no columns at all. */
  private readonly gridRef = viewChild(DxDataGridComponent);
  private readonly cells = contentChildren(AppGridCellDirective);
  private readonly viewContainer = inject(ViewContainerRef);
  private readonly views: EmbeddedViewRef<AppGridCellContext>[] = [];

  /* Columns are rebuilt whenever either half changes: a column naming a cellTemplate has that name
     swapped for a render function bound to the matching *appGridCell. */
  private readonly resolvedColumns = computed(() => {
    const renderers = new Map(this.cells().map((cell) => [cell.name(), cell]));
    return this.columns().map((column) => {
      const named = (column as { cellTemplate?: unknown }).cellTemplate;
      const renderer =
        typeof named === "string" ? renderers.get(named) : undefined;
      if (!renderer) return column;
      return {
        ...column,
        cellTemplate: (container: HTMLElement, options: GridCellInfo) =>
          this.stamp(renderer.template, container, options),
      };
    });
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => this.destroyViews());
  }

  /* DevExtreme owns the cell element, so the view is created against this component's container -
     which keeps it in Angular's change detection - and its nodes are then moved into the cell. */
  private stamp(
    template: AppGridCellDirective["template"],
    container: HTMLElement,
    options: GridCellInfo,
  ): void {
    const view = this.viewContainer.createEmbeddedView(template, {
      $implicit: options,
      cell: options,
    });
    view.detectChanges();
    for (const node of view.rootNodes) container.appendChild(node as Node);
    this.views.push(view);
  }

  /** Called on every render: DevExtreme empties recycled cells, orphaning the views inside them. */
  protected pruneViews(): void {
    for (let index = this.views.length - 1; index >= 0; index--) {
      const view = this.views[index];
      const attached = view.rootNodes.some(
        (node: Node) => (node as Node).isConnected,
      );
      if (attached) continue;
      view.destroy();
      this.views.splice(index, 1);
    }
  }

  private destroyViews(): void {
    for (const view of this.views) view.destroy();
    this.views.length = 0;
  }

  /** Clears the filter row, the header filters and the search, and returns to the first page. */
  clearFilters(): void {
    const grid = this.gridRef()?.instance;
    if (!grid) return;
    grid.clearFilter();
    grid.option("searchPanel.text", "");
    void grid.pageIndex(0);
  }

  protected get gridColumns(): unknown {
    const columns = this.resolvedColumns();
    return columns.length ? columns : undefined;
  }

  protected get selectedRowKeys(): unknown[] {
    return this.selectedKeys() as unknown[];
  }
}
