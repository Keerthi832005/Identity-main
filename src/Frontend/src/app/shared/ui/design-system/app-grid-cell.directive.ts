import { Directive, TemplateRef, inject, input } from "@angular/core";

/** What a cell template receives; `cell` mirrors `$implicit` so either name reads naturally. */
export interface AppGridCellContext {
  readonly $implicit: GridCellInfo;
  readonly cell: GridCellInfo;
}

/* `data` and `value` are deliberately loose: the row shape differs per screen, and a cell template
   is written against the rows its own screen supplies. Typing them as unknown would force a cast in
   every template for no safety a caller does not already have. */
export interface GridCellInfo {
  /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
  readonly data: any;
  /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
  readonly value: any;
  readonly rowIndex: number;
  readonly columnIndex: number;
}

/**
 * Declares a cell renderer for {@link AppDataGridComponent}, named to match a column's
 * `cellTemplate`.
 *
 * DevExtreme's own `*dxTemplate` cannot be used here: it injects `DxTemplateHost`, which only the
 * `dx-data-grid` element provides, so a template written in a screen and projected into a wrapper
 * throws NG0201 before it ever renders. This directive just captures the TemplateRef; the grid
 * stamps it into DevExtreme's cell container itself.
 *
 * ```html
 * <div *appGridCell="'unitName'; let cell">{{ cell.data.unitName }}</div>
 * ```
 */
@Directive({ selector: "[appGridCell]", standalone: true })
export class AppGridCellDirective {
  /** Column `cellTemplate` this renderer answers to. */
  readonly name = input.required<string>({ alias: "appGridCell" });
  readonly template: TemplateRef<AppGridCellContext> = inject(TemplateRef);

  static ngTemplateContextGuard(
    _directive: AppGridCellDirective,
    context: unknown,
  ): context is AppGridCellContext {
    return true;
  }
}
