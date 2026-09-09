export type BulkColumnType =
  "Text" | "Number" | "Boolean" | "Date" | "Enumeration";

export interface BulkColumn {
  readonly columnId: string;
  readonly header: string;
  readonly type: BulkColumnType;
  readonly required: boolean;
  readonly allowedValues?: readonly string[];
}

export type BulkRowState = "Create" | "Update" | "Invalid" | "Applied";
export type BulkBatchState = "Staged" | "Committed" | "Discarded";
export type BulkSource = "Excel" | "Paste";

export interface BulkCellError {
  readonly sourceRowNumber: number;
  readonly columnId: string;
  readonly code: string;
  readonly message: string;
}

export interface BulkStagedRow {
  readonly sourceRowNumber: number;
  readonly state: BulkRowState;
  readonly values: Readonly<Record<string, string | null>>;
  readonly errors: readonly BulkCellError[];
}

export interface BulkBatch {
  readonly batchKey: string;
  readonly entityKey: string;
  readonly source: BulkSource;
  readonly fileName: string | null;
  readonly state: BulkBatchState;
  readonly submittedAt: string;
  readonly expiresAt: string;
  readonly totalRows: number;
  readonly createRows: number;
  readonly updateRows: number;
  readonly invalidRows: number;
  readonly appliedRows: number;
}

export interface PagedBulkRows {
  readonly batch: BulkBatch;
  readonly skip: number;
  readonly take: number;
  readonly totalCount: number;
  readonly items: readonly BulkStagedRow[];
}

export interface BulkCommitResult {
  readonly batchKey: string;
  readonly createdRowCount: number;
  readonly updatedRowCount: number;
  readonly remainingInvalidRowCount: number;
  readonly alreadyCommitted: boolean;
}

export interface BulkDiscardResult {
  readonly batchKey: string;
  readonly discardedRowCount: number;
}

export interface BulkPasteRow {
  readonly sourceRowNumber: number;
  readonly values: Readonly<Record<string, string | null>>;
}

/** Rows read from the clipboard, before the administrator confirms the column mapping. */
export interface PendingPaste {
  readonly entityKey: string;
  readonly columns: readonly BulkColumn[];
  readonly rows: readonly (readonly string[])[];
  readonly hasHeaderRow: boolean;
  readonly truncated: boolean;
}
