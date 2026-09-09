import { describe, expect, it } from "vitest";
import { BulkColumn } from "./bulk-data.models";
import {
  looksLikeHeaderRow,
  matchByHeader,
  matchByPosition,
  toValues,
  unmappedRequiredColumns,
} from "./column-matcher";

const columns: readonly BulkColumn[] = [
  {
    columnId: "employeeCode",
    header: "Employee code",
    type: "Text",
    required: true,
  },
  {
    columnId: "displayName",
    header: "Display name",
    type: "Text",
    required: true,
  },
  { columnId: "email", header: "Contact email", type: "Text", required: false },
  {
    columnId: "managerEmployeeCode",
    header: "Manager employee code",
    type: "Text",
    required: false,
  },
];

describe("Column matching", () => {
  it("matches headers that differ only in case, spacing and punctuation", () => {
    const mappings = matchByHeader(
      ["EMPLOYEE_CODE", "  display name  ", "Contact Email"],
      ["INDE03275", "Vinothkumar S", "v@in.fujitec.com"],
      columns,
    );

    expect(mappings.map((mapping) => mapping.columnId)).toEqual([
      "employeeCode",
      "displayName",
      "email",
    ]);
    expect(mappings.every((mapping) => mapping.auto)).toBe(true);
  });

  it("matches the spellings real exports actually use", () => {
    const mappings = matchByHeader(
      ["Emp Code", "Full Name", "Official Mail ID", "Reports To"],
      ["INDE03275", "Vinothkumar S", "v@in.fujitec.com", "INDE02904"],
      columns,
    );

    expect(mappings.map((mapping) => mapping.columnId)).toEqual([
      "employeeCode",
      "displayName",
      "email",
      "managerEmployeeCode",
    ]);
  });

  it("ignores a column the template has no home for", () => {
    const mappings = matchByHeader(
      ["Emp Code", "Cost Centre"],
      ["INDE03275", "CC-4410"],
      columns,
    );

    expect(mappings[1].columnId).toBeNull();
    expect(mappings[1].auto).toBe(false);
  });

  it("never maps two source columns onto the same template column", () => {
    const mappings = matchByHeader(
      ["Emp Code", "Employee ID"],
      ["INDE03275", "INDE03275"],
      columns,
    );

    expect(mappings[0].columnId).toBe("employeeCode");
    expect(mappings[1].columnId).toBeNull();
  });

  it("survives reordered columns", () => {
    const mappings = matchByHeader(
      ["Contact email", "Employee code"],
      ["v@in.fujitec.com", "INDE03275"],
      columns,
    );

    expect(mappings.map((mapping) => mapping.columnId)).toEqual([
      "email",
      "employeeCode",
    ]);
  });

  it("detects a header row and rejects a data row", () => {
    expect(
      looksLikeHeaderRow(
        ["Emp Code", "Full Name", "Official Mail ID"],
        columns,
      ),
    ).toBe(true);
    expect(
      looksLikeHeaderRow(
        ["INDE03275", "Vinothkumar S", "v@in.fujitec.com"],
        columns,
      ),
    ).toBe(false);
    expect(looksLikeHeaderRow([], columns)).toBe(false);
  });

  it("falls back to template order when there is no header row", () => {
    const mappings = matchByPosition(
      ["INDE03275", "Vinothkumar S", "v@in.fujitec.com"],
      columns,
    );

    expect(mappings.map((mapping) => mapping.columnId)).toEqual([
      "employeeCode",
      "displayName",
      "email",
    ]);
  });

  it("reports a required column that nothing maps to", () => {
    const mappings = matchByHeader(["Emp Code"], ["INDE03275"], columns);

    expect(
      unmappedRequiredColumns(mappings, columns).map(
        (column) => column.columnId,
      ),
    ).toEqual(["displayName"]);
  });

  it("builds values from the mapping and blanks empty cells to null", () => {
    const mappings = matchByHeader(
      ["Emp Code", "Full Name", "Official Mail ID"],
      ["INDE03275", "Vinothkumar S", ""],
      columns,
    );

    expect(toValues(["INDE05512", "Harish Venkat", "  "], mappings)).toEqual({
      employeeCode: "INDE05512",
      displayName: "Harish Venkat",
      email: null,
    });
  });
});
