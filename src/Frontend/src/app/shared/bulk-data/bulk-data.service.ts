import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable, inject, signal } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { RUNTIME_CONFIG } from "../../core/config/runtime-config";
import {
  BulkBatch,
  BulkColumn,
  BulkCommitResult,
  BulkDiscardResult,
  BulkPasteRow,
  PagedBulkRows,
  PendingPaste,
} from "./bulk-data.models";

@Injectable({ providedIn: "root" })
export class BulkDataService {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(RUNTIME_CONFIG).identityBaseUrl}/api/v1/admin/bulk`;

  /**
   * Rows read from the clipboard, held between the grid that captured the paste and the mapping
   * screen that confirms it. Kept in memory only: a pasted block is never persisted before the
   * administrator has approved the column mapping.
   */
  readonly pendingPaste = signal<PendingPaste | null>(null);

  downloadTemplate(entityKey: string): Promise<void> {
    return this.download(
      firstValueFrom(
        this.http.get(`${this.base}/${entityKey}/template`, {
          observe: "response",
          responseType: "blob",
        }),
      ),
    );
  }

  export(entityKey: string, rows: readonly BulkPasteRow[]): Promise<void> {
    return this.download(
      firstValueFrom(
        this.http.post(
          `${this.base}/${entityKey}/export`,
          { rows },
          { observe: "response", responseType: "blob" },
        ),
      ),
    );
  }

  stagePaste(
    entityKey: string,
    rows: readonly BulkPasteRow[],
  ): Promise<BulkBatch> {
    return firstValueFrom(
      this.http.post<BulkBatch>(`${this.base}/${entityKey}/staging`, { rows }),
    );
  }

  stageWorkbook(entityKey: string, file: File): Promise<BulkBatch> {
    const form = new FormData();
    form.append("workbook", file, file.name);
    return firstValueFrom(
      this.http.post<BulkBatch>(`${this.base}/${entityKey}/staging`, form),
    );
  }

  getBatch(
    batchKey: string,
    options: {
      needsAttentionOnly?: boolean;
      skip?: number;
      take?: number;
    } = {},
  ): Promise<PagedBulkRows> {
    let params = new HttpParams()
      .set("skip", options.skip ?? 0)
      .set("take", options.take ?? 50);
    if (options.needsAttentionOnly)
      params = params.set("filter", "needs-attention");
    return firstValueFrom(
      this.http.get<PagedBulkRows>(`${this.base}/staging/${batchKey}`, {
        params,
      }),
    );
  }

  correctRow(
    batchKey: string,
    sourceRowNumber: number,
    values: Record<string, string | null>,
  ): Promise<unknown> {
    return firstValueFrom(
      this.http.put(
        `${this.base}/staging/${batchKey}/rows/${sourceRowNumber}`,
        { values },
      ),
    );
  }

  downloadErrors(batchKey: string): Promise<void> {
    return this.download(
      firstValueFrom(
        this.http.get(`${this.base}/staging/${batchKey}/errors`, {
          observe: "response",
          responseType: "blob",
        }),
      ),
    );
  }

  commit(batchKey: string): Promise<BulkCommitResult> {
    return firstValueFrom(
      this.http.post<BulkCommitResult>(
        `${this.base}/staging/${batchKey}/commit`,
        null,
      ),
    );
  }

  discard(batchKey: string): Promise<BulkDiscardResult> {
    return firstValueFrom(
      this.http.delete<BulkDiscardResult>(`${this.base}/staging/${batchKey}`),
    );
  }

  beginPaste(
    entityKey: string,
    columns: readonly BulkColumn[],
    rows: readonly (readonly string[])[],
    hasHeaderRow: boolean,
    truncated: boolean,
  ): void {
    this.pendingPaste.set({
      entityKey,
      columns,
      rows,
      hasHeaderRow,
      truncated,
    });
  }

  clearPaste(): void {
    this.pendingPaste.set(null);
  }

  /* The server names the file; echoing a caller-supplied name would let an upload choose it. */
  private async download(
    pending: Promise<{
      body: Blob | null;
      headers: { get(name: string): string | null };
    }>,
  ): Promise<void> {
    const response = await pending;
    if (!response.body) return;
    const url = URL.createObjectURL(response.body);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileNameFrom(response.headers.get("content-disposition"));
    document.body.append(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  }
}

function fileNameFrom(disposition: string | null): string {
  const match = disposition?.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i);
  return match ? decodeURIComponent(match[1]) : "download.xlsx";
}
