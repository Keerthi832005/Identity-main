import { describe, expect, it } from "vitest";
import { isMultiCell, parseClipboard } from "./clipboard-parser";

function clipboard(parts: Record<string, string>): DataTransfer {
  return {
    getData: (type: string) => parts[type] ?? "",
  } as unknown as DataTransfer;
}

describe("Clipboard parsing", () => {
  it("prefers the HTML table Excel puts on the clipboard", () => {
    const html = `<table><tr><td>Emp Code</td><td>Full Name</td></tr>
      <tr><td>INDE03275</td><td>Vinothkumar S</td></tr></table>`;

    const parsed = parseClipboard(clipboard({ "text/html": html }), 500);

    expect(parsed?.source).toBe("html");
    expect(parsed?.rows).toEqual([
      ["Emp Code", "Full Name"],
      ["INDE03275", "Vinothkumar S"],
    ]);
  });

  it("keeps an in-cell line break that Excel wrote as a break tag", () => {
    const html = `<table><tr><td>Line one<br>Line two</td><td>B</td></tr></table>`;

    const parsed = parseClipboard(clipboard({ "text/html": html }), 500);

    expect(parsed?.rows[0][0]).toBe("Line one\nLine two");
  });

  it("falls back to tab separated text", () => {
    const text = "Emp Code\tFull Name\nINDE03275\tVinothkumar S";

    const parsed = parseClipboard(clipboard({ "text/plain": text }), 500);

    expect(parsed?.source).toBe("tsv");
    expect(parsed?.rows[1]).toEqual(["INDE03275", "Vinothkumar S"]);
  });

  it("reads a quoted comma inside a CSV value as data, not a separator", () => {
    const text = 'Code,Name\nINDE03275,"Singaravel, Vinothkumar"';

    const parsed = parseClipboard(clipboard({ "text/plain": text }), 500);

    expect(parsed?.source).toBe("csv");
    expect(parsed?.rows[1]).toEqual(["INDE03275", "Singaravel, Vinothkumar"]);
  });

  it("reads a quoted newline inside a CSV value as one cell", () => {
    const text = 'Code,Address\nINDE03275,"Line one\nLine two"';

    const parsed = parseClipboard(clipboard({ "text/plain": text }), 500);

    expect(parsed?.rows).toHaveLength(2);
    expect(parsed?.rows[1][1]).toBe("Line one\nLine two");
  });

  it("unescapes a doubled quote", () => {
    const text = 'Code,Name\nX,"She said ""yes"""';

    const parsed = parseClipboard(clipboard({ "text/plain": text }), 500);

    expect(parsed?.rows[1][1]).toBe('She said "yes"');
  });

  it("prefers tabs over commas when both appear", () => {
    const text = "Code\tName\nINDE03275\tSingaravel, Vinothkumar";

    const parsed = parseClipboard(clipboard({ "text/plain": text }), 500);

    expect(parsed?.source).toBe("tsv");
    expect(parsed?.rows[1][1]).toBe("Singaravel, Vinothkumar");
  });

  it("truncates beyond the row limit and says so", () => {
    const text = Array.from({ length: 12 }, (_, index) => `R${index}\tX`).join(
      "\n",
    );

    const parsed = parseClipboard(clipboard({ "text/plain": text }), 5);

    expect(parsed?.rows).toHaveLength(5);
    expect(parsed?.truncated).toBe(true);
  });

  it("returns nothing for an empty clipboard", () => {
    expect(parseClipboard(clipboard({ "text/plain": "   " }), 500)).toBeNull();
    expect(parseClipboard(null, 500)).toBeNull();
  });

  it("treats a single cell as an ordinary edit rather than a bulk paste", () => {
    const single = parseClipboard(
      clipboard({ "text/plain": "INDE03275" }),
      500,
    );

    expect(isMultiCell(single)).toBe(false);
    expect(
      isMultiCell(parseClipboard(clipboard({ "text/plain": "A\tB" }), 500)),
    ).toBe(true);
  });
});
