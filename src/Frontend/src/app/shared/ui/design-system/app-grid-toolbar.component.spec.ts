import type { Column } from "devextreme/ui/data_grid";
import { describe, expect, it } from "vitest";
import { buildFilterChips, nextSearchText } from "./app-grid-toolbar.component";

describe("buildFilterChips", () => {
  it("has nothing to show when no filter is set", () => {
    expect(
      buildFilterChips("", [{ dataField: "hostname", caption: "Hostname" }]),
    ).toEqual([]);
  });

  it("names the column and reads back the filter-row operation", () => {
    const columns = [
      {
        dataField: "hostname",
        caption: "Hostname",
        filterValue: "fujitec",
        selectedFilterOperation: "contains",
      },
    ] as Column[];

    expect(buildFilterChips("", columns)).toEqual([
      {
        key: "filterValue:hostname",
        field: "hostname",
        kind: "filterValue",
        label: "Hostname",
        value: "contains fujitec",
      },
    ]);
  });

  it("joins the header-filter selection into one chip", () => {
    const columns = [
      {
        dataField: "status",
        caption: "Status",
        filterValues: ["Active", true],
      },
    ] as Column[];

    expect(buildFilterChips("", columns)[0].value).toBe("Active, Yes");
  });

  it("puts the search term first so clearing it is always the same chip", () => {
    const columns = [
      { dataField: "status", caption: "Status", filterValue: "Closed" },
    ] as Column[];

    expect(
      buildFilterChips("client 2", columns).map((chip) => chip.key),
    ).toEqual(["search", "filterValue:status"]);
  });

  /* A template-only column has no dataField, and columnOption cannot address one without a name,
     so a chip for it could never be removed. */
  it("skips a column that cannot be addressed", () => {
    const columns = [
      { caption: "Actions", filterValue: "x" },
      { name: "trust", caption: "Trust", filterValue: "Trusted" },
    ] as Column[];

    expect(buildFilterChips("", columns).map((chip) => chip.field)).toEqual([
      "trust",
    ]);
  });
});

describe("nextSearchText", () => {
  /* The regression: the typed term vanished from the box while the grid showed its results. */
  it("keeps what is typed when the screen echoes the term back", () => {
    expect(nextSearchText("bala", "bala", "bala")).toBe("bala");
  });

  it("keeps newer keystrokes when a slower echo of an older term arrives", () => {
    expect(nextSearchText("bala", "balaj", "bala")).toBe("balaj");
  });

  it("adopts a term the screen changed itself, such as a cleared filter", () => {
    expect(nextSearchText("", "bala", "bala")).toBe("");
    expect(nextSearchText("chennai", "bala", "bala")).toBe("chennai");
  });

  /* First render: there is no previous value to keep, so the route's own term must show. */
  it("adopts the term on the first render", () => {
    expect(nextSearchText("bala", undefined, "bala")).toBe("bala");
  });
});
