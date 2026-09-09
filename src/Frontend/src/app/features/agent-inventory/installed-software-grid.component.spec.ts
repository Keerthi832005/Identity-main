import { describe, expect, it } from "vitest";
import {
  softwareGridRows,
  softwareInstalledDate,
} from "./installed-software-grid.component";

describe("installed software metadata", () => {
  it("keeps calendar dates local without inventing a timestamp", () => {
    const date = softwareInstalledDate("2024-02-29")!;
    expect([date.getFullYear(), date.getMonth(), date.getDate()]).toEqual([
      2024, 1, 29,
    ]);
    expect(date.getHours()).toBe(0);
    for (const invalid of [
      undefined,
      null,
      "",
      "2023-02-29",
      "2026-13-01",
      "2026-08-31T12:30:00Z",
      "0000-01-01",
    ])
      expect(softwareInstalledDate(invalid)).toBeNull();
  });
  it("accepts old reports and preserves unknown metadata as null", () => {
    const input = [{ name: "Legacy", version: "", publisher: "" }];
    expect(softwareGridRows(input)[0]).toMatchObject({
      rowId: 0,
      installedOn: null,
      estimatedSizeMiB: null,
      registryView: null,
      version: null,
    });
    expect(input[0]).not.toHaveProperty("rowId");
  });
  it("keeps duplicate names distinct and size numeric for sorting/filtering", () => {
    const rows = softwareGridRows([
      {
        name: "Tool",
        version: "1",
        publisher: "Vendor",
        installedOn: "2026-08-31",
        estimatedSizeBytes: 1572864,
        registryView: "64-bit",
      },
      {
        name: "Tool",
        version: "1",
        publisher: "Vendor",
        estimatedSizeBytes: 0,
        registryView: "32-bit",
      },
    ]);
    expect(rows.map((row) => row.rowId)).toEqual([0, 1]);
    expect(rows.map((row) => row.estimatedSizeMiB)).toEqual([1.5, 0]);
    expect(rows[0].installedOn).toBeInstanceOf(Date);
  });
});
