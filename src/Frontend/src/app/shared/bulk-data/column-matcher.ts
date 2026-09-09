import { BulkColumn } from "./bulk-data.models";

export interface ColumnMapping {
  readonly sourceIndex: number;
  readonly sourceHeader: string;
  readonly sample: string;
  /** `null` means the column is ignored and will not be imported. */
  readonly columnId: string | null;
  readonly auto: boolean;
}

/**
 * Extra spellings seen in real HR and catalogue exports. Keys are normalised forms; the value is the
 * template column id they resolve to.
 */
const synonyms: Readonly<Record<string, string>> = {
  empcode: "employeeCode",
  employeeid: "employeeCode",
  employeeno: "employeeCode",
  staffcode: "employeeCode",
  staffid: "employeeCode",
  fullname: "displayName",
  name: "displayName",
  employeename: "displayName",
  mail: "email",
  mailid: "email",
  emailid: "email",
  officialmailid: "email",
  emailaddress: "email",
  reportsto: "managerEmployeeCode",
  manager: "managerEmployeeCode",
  managercode: "managerEmployeeCode",
  reportingmanager: "managerEmployeeCode",
  active: "isActive",
  status: "isActive",
  applicationcode: "applicationCode",
  appcode: "applicationCode",
  role: "roleCode",
  rolecode: "roleCode",
  parentcode: "parentUnitCode",
  parent: "parentUnitCode",
  type: "unitType",
  code: "unitCode",
};

export function normalizeHeader(header: string): string {
  return header
    .trim()
    .replace(/\*+$/, "")
    .toLowerCase()
    .replace(/[^a-z0-9]/g, "");
}

/**
 * A first row is treated as headers only when most of it resolves to template columns. Guessing
 * wrong either way loses a record or imports a header as data, so the bar is deliberately high.
 */
export function looksLikeHeaderRow(
  row: readonly string[],
  columns: readonly BulkColumn[],
): boolean {
  const nonEmpty = row.filter((cell) => cell.trim().length > 0);
  if (nonEmpty.length === 0) return false;
  const matched = nonEmpty.filter(
    (cell) => resolveColumnId(cell, columns) !== null,
  ).length;
  return matched / nonEmpty.length >= 0.6;
}

export function resolveColumnId(
  header: string,
  columns: readonly BulkColumn[],
): string | null {
  const normalized = normalizeHeader(header);
  if (!normalized) return null;

  const direct = columns.find(
    (column) =>
      normalizeHeader(column.header) === normalized ||
      normalizeHeader(column.columnId) === normalized,
  );
  if (direct) return direct.columnId;

  const synonym = synonyms[normalized];
  return synonym && columns.some((column) => column.columnId === synonym)
    ? synonym
    : null;
}

/** Maps a detected header row to template columns, leaving unmatched sources ignored. */
export function matchByHeader(
  headers: readonly string[],
  sample: readonly string[],
  columns: readonly BulkColumn[],
): ColumnMapping[] {
  const taken = new Set<string>();
  return headers.map((header, sourceIndex) => {
    const resolved = resolveColumnId(header, columns);
    const columnId = resolved && !taken.has(resolved) ? resolved : null;
    if (columnId) taken.add(columnId);
    return {
      sourceIndex,
      sourceHeader: header,
      sample: sample[sourceIndex] ?? "",
      columnId,
      auto: columnId !== null,
    };
  });
}

/** Fallback for a headerless paste: template order, which is the order the template presents. */
export function matchByPosition(
  sample: readonly string[],
  columns: readonly BulkColumn[],
): ColumnMapping[] {
  return sample.map((value, sourceIndex) => ({
    sourceIndex,
    sourceHeader: columns[sourceIndex]?.header ?? `Column ${sourceIndex + 1}`,
    sample: value,
    columnId: columns[sourceIndex]?.columnId ?? null,
    auto: sourceIndex < columns.length,
  }));
}

export function unmappedRequiredColumns(
  mappings: readonly ColumnMapping[],
  columns: readonly BulkColumn[],
): BulkColumn[] {
  const mapped = new Set(
    mappings.map((mapping) => mapping.columnId).filter(Boolean),
  );
  return columns.filter(
    (column) => column.required && !mapped.has(column.columnId),
  );
}

export function toValues(
  row: readonly string[],
  mappings: readonly ColumnMapping[],
): Record<string, string | null> {
  const values: Record<string, string | null> = {};
  for (const mapping of mappings) {
    if (!mapping.columnId) continue;
    const value = row[mapping.sourceIndex]?.trim() ?? "";
    values[mapping.columnId] = value.length ? value : null;
  }
  return values;
}
