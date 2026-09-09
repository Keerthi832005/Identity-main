import {
  ActivatedRoute,
  convertToParamMap,
  provideRouter,
} from "@angular/router";
import { of } from "rxjs";
import { ConfirmationService } from "../../shared/ui/confirmation/confirmation.service";
import { TestBed } from "@angular/core/testing";
import { describe, expect, it, vi } from "vitest";
import { ApplicationCatalogComponent } from "./application-catalog.component";
import { ApplicationCatalogService } from "./application-catalog.service";

describe("Inline application form", () => {
  it("keeps a pending write open, prevents duplicates and clears the secret on completion", async () => {
    let resolve!: (result: { resourceId: number }) => void;
    const service = {
      createApplication: vi.fn(
        () =>
          new Promise((done) => {
            resolve = done;
          }),
      ),
      search: vi.fn().mockResolvedValue({ items: [] }),
    };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: ConfirmationService,
          useValue: { ask: vi.fn().mockResolvedValue(true) },
        },
        { provide: ApplicationCatalogService, useValue: service },
      ],
    });
    const page = TestBed.runInInjectionContext(
      () => new ApplicationCatalogComponent(),
    );
    vi.spyOn(page["directory"], "open").mockResolvedValue(true);
    page["openForm"]("application");
    page["code"].set("new-app");
    page["name"].set("New application");
    page["tokenAudience"].set("urn:test:new");
    const pending = page["submit"]();
    await page["closeForm"]();
    expect(page["formMode"]()).toBe("application");
    expect(await page.canLeaveForm()).toBe(false);
    await page["submit"]();
    expect(service.createApplication).toHaveBeenCalledTimes(1);
    resolve({ resourceId: 42 });
    await pending;
    expect(page["formMode"]()).toBeNull();
    expect(page["notice"]()).toContain("created successfully");
    expect(page["clientSecret"]()).toBe("");
    page.ngOnDestroy();
  });
});

describe("Module hierarchy", () => {
  function module(
    id: number,
    name: string,
    parent: number | null,
    displayOrder: number,
    isSystem = false,
  ) {
    return {
      applicationModuleId: id,
      applicationId: 1,
      moduleCode: name,
      moduleName: name,
      description: null,
      parentApplicationModuleId: parent,
      displayOrder,
      isSystem,
      isActive: true,
      createdAt: "2026-01-01T00:00:00Z",
      updatedAt: null,
    };
  }

  function page(modules: ReturnType<typeof module>[], service = {}) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: ConfirmationService,
          useValue: { ask: vi.fn().mockResolvedValue(true) },
        },
        { provide: ApplicationCatalogService, useValue: service },
      ],
    });
    const component = TestBed.runInInjectionContext(
      () => new ApplicationCatalogComponent(),
    );
    component["catalog"].set({
      application: { applicationId: 1 },
      clients: [],
      modules,
      capabilities: [],
    } as never);
    return component;
  }

  it("nests children under their parent in display order", () => {
    const component = page([
      module(3, "second-child", 1, 1),
      module(2, "first-child", 1, 0),
      module(1, "parent", null, 0),
      module(4, "sibling", null, 1),
    ]);

    expect(
      component["moduleTree"]().map((node) => [
        node.module.moduleName,
        node.depth,
      ]),
    ).toEqual([
      ["parent", 0],
      ["first-child", 1],
      ["second-child", 1],
      ["sibling", 0],
    ]);
  });

  it("shows a module whose parent is not in the catalog at the root", () => {
    /* A revoked parent must not take its whole subtree off the screen. */
    const component = page([module(9, "orphan", 404, 0)]);

    expect(component["moduleTree"]().map((node) => node.depth)).toEqual([0]);
  });

  it("offers no move that would shift a system module", () => {
    const component = page([
      module(1, "first", null, 0),
      module(2, "second", null, 1),
      module(3, "system", null, 2, true),
    ]);

    expect(
      component["moduleTree"]().map((node) => [
        node.module.moduleName,
        node.canMoveUp,
        node.canMoveDown,
      ]),
    ).toEqual([
      ["first", false, true],
      /* Down would displace the system module behind it. */
      ["second", true, false],
      ["system", false, false],
    ]);
  });

  it("compares moves within a sibling group, not across the whole list", () => {
    const component = page([
      module(1, "parent", null, 0),
      module(2, "only-child", 1, 0),
    ]);

    const child = component["moduleTree"]()[1];
    expect([child.canMoveUp, child.canMoveDown]).toEqual([false, false]);
  });

  it("reloads the catalog after a move and reports a failure without one", async () => {
    const moveModule = vi.fn().mockResolvedValue({ resourceId: 2 });
    const getCatalog = vi.fn().mockResolvedValue({
      application: { applicationId: 1 },
      clients: [],
      modules: [],
      capabilities: [],
    });
    const component = page(
      [module(1, "first", null, 0), module(2, "second", null, 1)],
      { moveModule, getCatalog },
    );

    await component["moveModule"](component["moduleTree"]()[1].module, "up");
    expect(moveModule).toHaveBeenCalledWith(1, 2, "up");
    expect(getCatalog).toHaveBeenCalledWith(1);
    expect(component["error"]()).toBeNull();

    moveModule.mockRejectedValueOnce(new Error("nope"));
    await component["moveModule"](
      module(2, "second", null, 1) as never,
      "down",
    );
    expect(component["error"]()).toContain("could not be moved");
  });
});
