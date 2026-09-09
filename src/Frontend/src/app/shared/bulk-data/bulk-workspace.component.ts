import { ConfirmationService } from "../ui/confirmation/confirmation.service";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnDestroy,
  signal,
} from "@angular/core";
import { ActivatedRoute, Router } from "@angular/router";
import { DxButtonModule, DxDataGridModule } from "devextreme-angular";
import { CustomStore, DataSource } from "devextreme/common/data";
import { AppBusyOverlayComponent } from "../ui/design-system/app-busy-overlay.component";
import { AppErrorStateComponent } from "../ui/design-system/app-error-state.component";
import { AppInlineAlertComponent } from "../ui/design-system/app-inline-alert.component";
import { AppPageComponent } from "../ui/design-system/app-page.component";
import { AppSectionComponent } from "../ui/design-system/app-section.component";
import { AppSkeletonComponent } from "../ui/design-system/app-skeleton.component";
import {
  BulkCellError,
  BulkStagedRow,
  PagedBulkRows,
} from "./bulk-data.models";
import { BulkDataService } from "./bulk-data.service";

/**
 * The staged preview. Both an Excel upload and a smart paste land here, and nothing has been written
 * to the identity tables at this point: the commit bar states exactly what will change.
 */
@Component({
  selector: "app-bulk-workspace",
  standalone: true,
  imports: [
    AppBusyOverlayComponent,
    AppErrorStateComponent,
    AppInlineAlertComponent,
    AppPageComponent,
    AppSectionComponent,
    AppSkeletonComponent,
    DxButtonModule,
    DxDataGridModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: "./bulk-workspace.component.html",
  styleUrl: "./bulk-workspace.component.scss",
})
export class BulkWorkspaceComponent implements OnDestroy {
  private readonly service = inject(BulkDataService);
  private readonly confirmations = inject(ConfirmationService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly batchKey =
    this.route.snapshot.paramMap.get("batchKey") ?? "";
  protected readonly page = signal<PagedBulkRows | null>(null);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly failure = signal("");
  protected readonly correlationId = signal("");
  protected readonly needsAttentionOnly = signal(false);
  protected readonly committed = signal<string>("");
  protected readonly pageSizes = [10, 25, 50, 100];
  protected readonly remoteOperations = { paging: true };
  protected readonly source = new DataSource<BulkStagedRow, number>({
    paginate: true,
    pageSize: 25,
    store: new CustomStore<BulkStagedRow, number>({
      key: "sourceRowNumber",
      loadMode: "processed",
      load: (options) => this.fetchPage(options.skip ?? 0, options.take ?? 25),
    }),
  });
  private loadVersion = 0;

  protected readonly columnIds = computed(() => {
    const rows = this.page()?.items ?? [];
    const seen = new Set<string>();
    for (const row of rows) {
      for (const key of Object.keys(row.values)) seen.add(key);
    }
    return [...seen];
  });

  protected readonly willWrite = computed(() => {
    const batch = this.page()?.batch;
    return batch ? batch.createRows + batch.updateRows : 0;
  });

  constructor() {
    void this.load();
  }

  ngOnDestroy(): void {
    this.loadVersion++;
    this.source.dispose();
  }

  protected async load(): Promise<void> {
    try {
      await this.source.reload();
    } catch {
      // fetchPage records a safe error; never discard the last successful preview.
    }
  }

  private async fetchPage(skip: number, take: number) {
    const version = ++this.loadVersion;
    this.loading.set(true);
    this.failure.set("");
    this.correlationId.set("");
    try {
      const options = {
        needsAttentionOnly: this.needsAttentionOnly(),
        skip,
        take,
      };
      let result = await this.service.getBatch(this.batchKey, options);
      // Correcting the last invalid row on a page can shrink the filtered result set.
      if (skip > 0 && skip >= result.totalCount) {
        const lastPage = Math.max(0, Math.ceil(result.totalCount / take) - 1);
        this.source.pageIndex(lastPage);
        result = await this.service.getBatch(this.batchKey, {
          ...options,
          skip: lastPage * take,
        });
      }
      if (version === this.loadVersion) this.page.set(result);
      return { data: [...result.items], totalCount: result.totalCount };
    } catch (error) {
      if (version === this.loadVersion)
        this.describe(error, "This batch could not be loaded.");
      throw new Error(
        "The staged rows could not be loaded. Use Refresh rows to retry.",
      );
    } finally {
      if (version === this.loadVersion) this.loading.set(false);
    }
  }

  protected async setFilter(needsAttentionOnly: boolean): Promise<void> {
    this.needsAttentionOnly.set(needsAttentionOnly);
    this.source.pageIndex(0);
    await this.load();
  }

  protected errorFor(
    row: { errors: readonly BulkCellError[] },
    columnId: string,
  ): BulkCellError | undefined {
    return row.errors.find((error) => error.columnId === columnId);
  }

  /** Only a still-staged row can be edited; an applied row is already written. */
  protected editable(row: { state: string }): boolean {
    return this.page()?.batch.state === "Staged" && row.state !== "Applied";
  }

  /**
   * Corrects one cell. The server revalidates only this row and returns its new state, so a fix here
   * cannot silently reclassify the rest of the batch.
   */
  protected async correct(
    row: BulkStagedRow,
    columnId: string,
    value: string,
  ): Promise<void> {
    if (this.busy() || this.loading() || !this.editable(row)) return;
    const next = value.trim();
    if ((row.values[columnId] ?? "") === next) return;

    await this.run(async () => {
      await this.service.correctRow(this.batchKey, row.sourceRowNumber, {
        ...row.values,
        [columnId]: next.length ? next : null,
      });
      /* Reload rather than patching in place: the counts band and the commit bar both read from the
         batch summary, and a correction changes them. */
      await this.load();
    });
  }

  protected async downloadErrors(): Promise<void> {
    await this.run(() => this.service.downloadErrors(this.batchKey));
  }

  protected async commit(): Promise<void> {
    const batch = this.page()?.batch;
    if (
      !batch ||
      this.busy() ||
      this.loading() ||
      !!this.failure() ||
      batch.state !== "Staged" ||
      this.willWrite() === 0
    )
      return;

    const left = batch.invalidRows;
    const confirmed = await this.confirmations.ask({
      title: "Apply import?",
      confirmText: "Apply import",
      tone: "danger",
      message:
        `Write ${this.willWrite()} rows: ${batch.createRows} created, ${batch.updateRows} updated.` +
        (left > 0
          ? ` ${left} invalid rows will not be written. Fix them before committing, or import the corrected rows in a new batch afterwards.`
          : "") +
        "\n\nThis cannot be undone.",
    });
    if (!confirmed) return;

    await this.run(async () => {
      const result = await this.service.commit(this.batchKey);
      this.committed.set(
        result.alreadyCommitted
          ? "This batch was already committed. Nothing was written again."
          : `Committed ${result.createdRowCount} created and ${result.updatedRowCount} updated. ` +
              (result.remainingInvalidRowCount > 0
                ? `${result.remainingInvalidRowCount} invalid rows were not written. Download the errors and import only the corrected rows in a new batch.`
                : "All valid rows were applied."),
      );
      await this.load();
    });
  }

  protected async discard(): Promise<void> {
    if (
      !(await this.confirmations.ask({
        title: "Discard import?",
        message:
          "Discard this batch? The staged rows are removed and nothing is written.",
        confirmText: "Discard batch",
        tone: "danger",
      }))
    ) {
      return;
    }

    await this.run(async () => {
      await this.service.discard(this.batchKey);
      await this.router.navigate(["/"]);
    });
  }

  private async run(action: () => Promise<void>): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.failure.set("");
    this.correlationId.set("");
    try {
      await action();
    } catch (error) {
      this.describe(error, "The request could not be completed.");
    } finally {
      this.busy.set(false);
    }
  }

  private describe(error: unknown, fallback: string): void {
    const detail = error as {
      error?: { title?: string; correlationId?: string };
      status?: number;
    };
    this.correlationId.set(detail?.error?.correlationId ?? "");
    this.failure.set(
      detail?.status === 404
        ? "This batch no longer exists. It may have been committed, discarded or expired."
        : (detail?.error?.title ?? fallback),
    );
  }
}
