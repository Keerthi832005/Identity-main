import { SimpleChange } from "@angular/core";
import { describe, expect, it, vi } from "vitest";
import { AppLookupComponent } from "./app-lookup.component";

describe("Combined server-search dropdown", () => {
  it("passes typed text to the server without applying a second label filter", async () => {
    const page = new AppLookupComponent();
    page.searchOptions = vi
      .fn()
      .mockResolvedValue([{ value: 99, label: "Remote Manager (EMP-99)" }]);
    page["source"].searchValue("remote@example.com");
    const result = await page["source"].load();
    expect(page.searchOptions).toHaveBeenCalledWith("remote@example.com");
    expect(result).toEqual([{ value: 99, label: "Remote Manager (EMP-99)" }]);
    page.ngOnDestroy();
  });
  it("retains the existing selection on failed searches and retries without changing its ID", async () => {
    const page = new AppLookupComponent();
    page.value = 12;
    page.selectedLabel = "Current team";
    const emit = vi.spyOn(page.valueChange, "emit");
    const search = vi
      .fn()
      .mockRejectedValueOnce(new Error("offline"))
      .mockResolvedValueOnce([{ value: 12, label: "Current team" }]);
    page.searchOptions = search;
    await page["source"].load();
    expect(page["loadError"]()).toBe(true);
    expect(await page["source"].store().byKey(12)).toEqual({
      value: 12,
      label: "Current team",
    });
    await page["source"].reload();
    expect(page["loadError"]()).toBe(false);
    expect(page.value).toBe(12);
    expect(emit).not.toHaveBeenCalled();
    page.ngOnDestroy();
  });
  it("ignores stale responses after a parent change", async () => {
    const page = new AppLookupComponent();
    let finish!: (options: { value: number; label: string }[]) => void;
    page.searchOptions = () =>
      new Promise((resolve) => {
        finish = resolve;
      });
    const pending = page["loadOptions"]("");
    page.ngOnChanges({ contextKey: new SimpleChange(11, 21, false) });
    finish([{ value: 12, label: "Old team" }]);
    expect(await pending).toEqual([]);
    expect(page["cache"].has(12)).toBe(false);
    page.ngOnDestroy();
  });
  it("changes IDs only on explicit selection or clearing, not programmatic reloads", () => {
    const page = new AppLookupComponent();
    page.value = 12;
    const emit = vi.spyOn(page.valueChange, "emit");
    page["choose"]({ value: null });
    expect(emit).not.toHaveBeenCalled();
    page["choose"]({ value: 22, event: {} });
    expect(emit).toHaveBeenLastCalledWith(22);
    page["choose"]({ value: 0, event: {} });
    expect(emit).toHaveBeenLastCalledWith(null);
    page.disabled = true;
    page["choose"]({ value: 33, event: {} });
    expect(emit).toHaveBeenCalledTimes(2);
    page.ngOnDestroy();
  });
});
