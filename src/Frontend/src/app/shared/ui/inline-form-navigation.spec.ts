import { afterEach, describe, expect, it, vi } from "vitest";
import { InlineFormNavigation } from "./inline-form-navigation";

describe("Inline form navigation", () => {
  afterEach(() => vi.restoreAllMocks());

  it.each(["changed draft", "pending write"])(
    "rejects a stale decision after a %s",
    async (change) => {
      let draft = "initial";
      let saving = false;
      let decide!: (approved: boolean) => void;
      const navigation = new InlineFormNavigation(
        () => true,
        () => saving,
        () => draft,
        () => new Promise<boolean>((resolve) => (decide = resolve)),
      );
      navigation.begin();
      draft = "edited";
      const pending = navigation.canLeave();
      if (change === "changed draft") draft = "edited again";
      else saving = true;
      decide(true);
      expect(await pending).toBe(false);
      navigation.destroy();
    },
  );

  it("requires confirmation only for changes and prevents departure during a write", async () => {
    let open = true;
    let saving = false;
    let draft = "initial";
    const confirm = vi.fn().mockResolvedValue(false);
    const navigation = new InlineFormNavigation(
      () => open,
      () => saving,
      () => draft,
      confirm,
    );
    navigation.begin();
    expect(await navigation.canLeave()).toBe(true);
    expect(confirm).not.toHaveBeenCalled();
    draft = "changed";
    expect(await navigation.canLeave()).toBe(false);
    confirm.mockResolvedValue(true);
    expect(await navigation.canLeave()).toBe(true);
    saving = true;
    expect(await navigation.canLeave()).toBe(false);
    const event = new Event("beforeunload", { cancelable: true });
    navigation.beforeUnload(event as BeforeUnloadEvent);
    expect(event.defaultPrevented).toBe(true);
    saving = false;
    open = false;
    navigation.finish();
    expect(await navigation.canLeave()).toBe(true);
    const clean = new Event("beforeunload", { cancelable: true });
    navigation.beforeUnload(clean as BeforeUnloadEvent);
    expect(clean.defaultPrevented).toBe(false);
    navigation.destroy();
  });
});
