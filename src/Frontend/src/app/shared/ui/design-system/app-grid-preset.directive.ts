import { Directive, inject, input } from "@angular/core";
import { DxDataGridComponent } from "devextreme-angular";

export type GridDensity = "comfortable" | "compact";

/**
 * The single tabular presentation for the product: header style, row lines, hover, filter row,
 * column chooser and empty text. Applied to `dx-data-grid` so no screen configures these itself.
 *
 * Defaults are assigned in the constructor, which runs before Angular applies the element's own
 * input bindings - so anything a screen sets explicitly still wins.
 */
@Directive({
  selector: "dx-data-grid[appGridPreset]",
  standalone: true,
  host: { "[class.app-grid--compact]": 'density() === "compact"' },
})
export class AppGridPresetDirective {
  readonly density = input<GridDensity>("comfortable");

  constructor() {
    const grid = inject(DxDataGridComponent);
    grid.showBorders = false;
    grid.showRowLines = true;
    grid.showColumnLines = false;
    grid.hoverStateEnabled = true;
    grid.rowAlternationEnabled = false;
    grid.columnAutoWidth = true;
    grid.allowColumnResizing = true;
    grid.columnResizingMode = "widget";
    grid.wordWrapEnabled = false;
    grid.noDataText = "Nothing to show yet.";
    grid.filterRow = { visible: true, applyFilter: "auto" };
    grid.headerFilter = { visible: true, allowSearch: true };
    /* Centred on the viewport rather than anchored to the button: the chooser is a dialog about the
       whole table, and DevExtreme's default anchoring dropped it half off-screen when the button
       sat near the right edge. */
    grid.columnChooser = {
      enabled: true,
      mode: "select",
      allowSearch: true,
      title: "Choose columns",
      width: 320,
      height: 420,
      position: { my: "center", at: "center", of: window },
    };
    /* app-grid-toolbar renders the search box and the chooser button in one fixed row above the
       grid; DevExtreme's own toolbar would put a second, differently-placed copy under it. */
    grid.toolbar = { visible: false };
    grid.loadPanel = { enabled: false };
    grid.scrolling = { columnRenderingMode: "standard" };
    grid.keyboardNavigation = {
      enabled: true,
      enterKeyAction: "startEdit",
      enterKeyDirection: "column",
    };
  }
}
