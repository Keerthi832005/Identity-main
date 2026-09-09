/** A page of a server-paged list, however the screen's own service names it. */
export interface ExportPage<T> {
  readonly items: readonly T[];
  readonly totalCount: number;
}

/** Stops a filter that matches an unbounded table from downloading the whole trail. */
export const EXPORT_ROW_LIMIT = 20000;

/**
 * Walk every page of a server-paged list. Export must cover the whole filtered set, and the
 * administration endpoints cap a single request at 50-100 rows.
 */
export async function collectAllPages<T>(
  fetchPage: (skip: number) => Promise<ExportPage<T>>,
): Promise<readonly T[]> {
  const rows: T[] = [];
  for (;;) {
    const page = await fetchPage(rows.length);
    if (page.items.length === 0) return rows;
    rows.push(...page.items);
    if (rows.length >= Math.min(page.totalCount, EXPORT_ROW_LIMIT)) return rows;
  }
}
