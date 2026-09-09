export type ClipboardSource = "html" | "tsv" | "csv";

export interface ParsedClipboard {
  readonly rows: readonly (readonly string[])[];
  readonly source: ClipboardSource;
  readonly truncated: boolean;
}

/**
 * Reads a pasted block into a grid of cells.
 *
 * `text/html` is preferred because Excel and Sheets both put a real table there, which survives
 * values containing tabs or newlines. The plain-text fallbacks cannot recover those cases as
 * reliably, so they are a second choice rather than the primary path.
 */
export function parseClipboard(
  data: DataTransfer | null,
  maxRows: number,
): ParsedClipboard | null {
  if (!data) return null;

  const html = data.getData("text/html");
  const fromHtml = html ? parseHtmlTable(html) : null;
  if (fromHtml?.length) return limit(fromHtml, "html", maxRows);

  const text = data.getData("text/plain");
  if (!text.trim()) return null;

  const delimiter = pickDelimiter(text);
  const rows = parseDelimited(text, delimiter);
  return rows.length
    ? limit(rows, delimiter === "\t" ? "tsv" : "csv", maxRows)
    : null;
}

/** A single cell is an ordinary edit, not a bulk paste; the caller must not hijack it. */
export function isMultiCell(parsed: ParsedClipboard | null): boolean {
  if (!parsed || parsed.rows.length === 0) return false;
  return parsed.rows.length > 1 || parsed.rows[0].length > 1;
}

function limit(
  rows: string[][],
  source: ClipboardSource,
  maxRows: number,
): ParsedClipboard {
  const truncated = rows.length > maxRows;
  return { rows: truncated ? rows.slice(0, maxRows) : rows, source, truncated };
}

function parseHtmlTable(html: string): string[][] | null {
  const table = new DOMParser()
    .parseFromString(html, "text/html")
    .querySelector("table");
  if (!table) return null;

  const rows: string[][] = [];
  for (const row of Array.from(table.rows)) {
    const cells = Array.from(row.cells).map((cell) =>
      // Excel writes <br> for an in-cell line break; keep it as a newline, not a join artefact.
      (
        cell.innerHTML.replace(/<br\s*\/?>/gi, "\n").replace(/<[^>]*>/g, "") ??
        ""
      )
        .replace(/&nbsp;/g, " ")
        .replace(/&amp;/g, "&")
        .replace(/&lt;/g, "<")
        .replace(/&gt;/g, ">")
        .replace(/&quot;/g, '"')
        .trim(),
    );
    if (cells.some((cell) => cell.length > 0)) rows.push(cells);
  }
  return rows.length ? rows : null;
}

/* Tabs win when both appear: a comma inside a spreadsheet cell is ordinary text, whereas a tab is
   almost always Excel's own column separator. */
function pickDelimiter(text: string): "\t" | "," {
  const firstLine = text.split(/\r?\n/, 1)[0] ?? "";
  return firstLine.includes("\t") ? "\t" : ",";
}

function parseDelimited(text: string, delimiter: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let value = "";
  let quoted = false;

  for (let index = 0; index < text.length; index++) {
    const character = text[index];

    if (quoted) {
      if (character !== '"') {
        value += character;
      } else if (text[index + 1] === '"') {
        value += '"';
        index++;
      } else {
        quoted = false;
      }
      continue;
    }

    if (character === '"' && value.length === 0) {
      quoted = true;
    } else if (character === delimiter) {
      row.push(value);
      value = "";
    } else if (character === "\n" || character === "\r") {
      if (character === "\r" && text[index + 1] === "\n") index++;
      row.push(value);
      value = "";
      if (row.some((cell) => cell.length > 0)) rows.push(row);
      row = [];
    } else {
      value += character;
    }
  }

  row.push(value);
  if (row.some((cell) => cell.length > 0)) rows.push(row);
  return rows.map((cells) => cells.map((cell) => cell.trim()));
}
