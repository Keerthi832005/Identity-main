import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from "@angular/core";
import { Router } from "@angular/router";
import { DxButtonModule, DxSelectBoxModule } from "devextreme-angular";
import { AppInlineAlertComponent } from "../ui/design-system/app-inline-alert.component";
import { AppPageComponent } from "../ui/design-system/app-page.component";
import { AppSectionComponent } from "../ui/design-system/app-section.component";
import { BulkDataService } from "./bulk-data.service";
import {
  ColumnMapping,
  matchByHeader,
  matchByPosition,
  toValues,
  unmappedRequiredColumns,
} from "./column-matcher";

const ignore = "__ignore__";

/**
 * Confirms how pasted columns line up with the template before anything is staged. A guessed
 * mapping is never applied silently: the administrator always sees and can change it.
 */
@Component({
  selector: "app-bulk-mapping",
  standalone: true,
  imports: [
    AppInlineAlertComponent,
    AppPageComponent,
    AppSectionComponent,
    DxButtonModule,
    DxSelectBoxModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: "./bulk-mapping.component.html",
  styleUrl: "./bulk-mapping.component.scss",
})
export class BulkMappingComponent {
  private readonly service = inject(BulkDataService);
  private readonly router = inject(Router);

  protected readonly paste = this.service.pendingPaste;
  protected readonly mappings = signal<readonly ColumnMapping[]>([]);
  protected readonly staging = signal(false);
  protected readonly error = signal("");

  protected readonly targets = computed(() => [
    { columnId: ignore, header: "Not imported" },
    ...(this.paste()?.columns.map((column) => ({
      columnId: column.columnId,
      header: column.header + (column.required ? " (required)" : ""),
    })) ?? []),
  ]);

  protected readonly dataRowCount = computed(() => {
    const paste = this.paste();
    if (!paste) return 0;
    return paste.hasHeaderRow
      ? Math.max(paste.rows.length - 1, 0)
      : paste.rows.length;
  });

  protected readonly missingRequired = computed(() =>
    this.paste()
      ? unmappedRequiredColumns(this.mappings(), this.paste()!.columns)
      : [],
  );

  constructor() {
    this.reset();
  }

  protected reset(): void {
    const paste = this.paste();
    if (!paste || paste.rows.length === 0) {
      this.mappings.set([]);
      return;
    }

    const sample = paste.hasHeaderRow ? (paste.rows[1] ?? []) : paste.rows[0];
    this.mappings.set(
      paste.hasHeaderRow
        ? matchByHeader(paste.rows[0], sample, paste.columns)
        : matchByPosition(sample, paste.columns),
    );
  }

  protected retarget(sourceIndex: number, columnId: string | null): void {
    const resolved = columnId === ignore ? null : columnId;
    this.mappings.set(
      this.mappings().map((mapping) =>
        mapping.sourceIndex === sourceIndex
          ? { ...mapping, columnId: resolved, auto: false }
          : /* One template column can only receive one source column. */
            mapping.columnId === resolved && resolved !== null
            ? { ...mapping, columnId: null, auto: false }
            : mapping,
      ),
    );
  }

  protected cancel(): void {
    this.service.clearPaste();
    void this.router.navigate(["/"]);
  }

  protected async stage(): Promise<void> {
    const paste = this.paste();
    if (!paste) return;

    this.staging.set(true);
    this.error.set("");
    try {
      const dataRows = paste.hasHeaderRow ? paste.rows.slice(1) : paste.rows;
      const rows = dataRows.map((row, index) => ({
        /* Excel's own numbering, so an error message names a row the administrator can find. */
        sourceRowNumber: index + (paste.hasHeaderRow ? 2 : 1),
        values: toValues(row, this.mappings()),
      }));
      const batch = await this.service.stagePaste(paste.entityKey, rows);
      this.service.clearPaste();
      await this.router.navigate(["/bulk", batch.batchKey]);
    } catch (error) {
      const detail = error as { error?: { title?: string } };
      this.error.set(
        detail?.error?.title ?? "The rows could not be staged. Try again.",
      );
    } finally {
      this.staging.set(false);
    }
  }
}
