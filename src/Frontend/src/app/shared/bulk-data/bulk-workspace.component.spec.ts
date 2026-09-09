import { TestBed } from "@angular/core/testing";
import { ActivatedRoute, convertToParamMap, Router } from "@angular/router";
import { describe, expect, it, vi } from "vitest";
import { ConfirmationService } from "../ui/confirmation/confirmation.service";
import { BulkDataService } from "./bulk-data.service";
import { BulkWorkspaceComponent } from "./bulk-workspace.component";
import { PagedBulkRows } from "./bulk-data.models";

describe("Bulk workspace confirmations", () => {
  it.each(["commit", "discard"] as const)(
    "%s requires an explicit decision",
    async (action) => {
      const batch: PagedBulkRows = {
        batch: {
          batchKey: "review-batch",
          entityKey: "users",
          source: "Excel",
          fileName: "users.xlsx",
          state: "Staged",
          submittedAt: "2026-08-31T00:00:00Z",
          expiresAt: "2026-09-01T00:00:00Z",
          totalRows: 6,
          createRows: 2,
          updateRows: 3,
          invalidRows: 1,
          appliedRows: 0,
        },
        items: [],
        skip: 0,
        take: 200,
        totalCount: 6,
      };
      const ask = vi.fn().mockResolvedValue(false);
      const service = {
        getBatch: vi.fn().mockResolvedValue(batch),
        commit: vi.fn().mockResolvedValue({
          createdRowCount: 2,
          updatedRowCount: 3,
          remainingInvalidRowCount: 1,
        }),
        discard: vi.fn().mockResolvedValue({}),
      };
      const navigate = vi.fn().mockResolvedValue(true);
      TestBed.configureTestingModule({
        providers: [
          { provide: BulkDataService, useValue: service },
          { provide: ConfirmationService, useValue: { ask } },
          { provide: Router, useValue: { navigate } },
          {
            provide: ActivatedRoute,
            useValue: {
              snapshot: {
                paramMap: convertToParamMap({ batchKey: "review-batch" }),
              },
            },
          },
        ],
      });
      const page = TestBed.runInInjectionContext(
        () => new BulkWorkspaceComponent(),
      );
      await page["load"]();
      await page[action]();
      expect(service[action]).not.toHaveBeenCalled();
      expect(navigate).not.toHaveBeenCalled();
      if (action === "commit") {
        expect(ask).toHaveBeenCalledWith(
          expect.objectContaining({
            confirmText: "Apply import",
            message:
              "Write 5 rows: 2 created, 3 updated. 1 invalid rows will not be written. Fix them before committing, or import the corrected rows in a new batch afterwards.\n\nThis cannot be undone.",
          }),
        );
      }
      ask.mockResolvedValue(true);
      await page[action]();
      expect(service[action]).toHaveBeenCalledExactlyOnceWith("review-batch");
      expect(page["failure"]()).toBe("");
      if (action === "commit")
        expect(page["committed"]()).toContain("2 created and 3 updated");
      else expect(navigate).toHaveBeenCalledExactlyOnceWith(["/"]);
      page.ngOnDestroy();
    },
  );
});

describe("Bulk review paging and recovery", () => {
  async function setup() {
    const batch: PagedBulkRows = {
      batch: {
        batchKey: "review-batch",
        entityKey: "organization-units",
        source: "Paste",
        fileName: null,
        state: "Staged",
        submittedAt: "2026-08-31T00:00:00Z",
        expiresAt: "2026-09-01T00:00:00Z",
        totalRows: 253,
        createRows: 251,
        updateRows: 1,
        invalidRows: 1,
        appliedRows: 0,
      },
      items: [
        {
          sourceRowNumber: 3,
          state: "Update",
          values: { unitCode: "ROOT" },
          errors: [],
        },
      ],
      skip: 0,
      take: 25,
      totalCount: 253,
    };
    const service = {
      getBatch: vi.fn().mockResolvedValue(batch),
      commit: vi.fn(),
      correctRow: vi.fn().mockResolvedValue({}),
    };
    TestBed.configureTestingModule({
      providers: [
        { provide: BulkDataService, useValue: service },
        {
          provide: ConfirmationService,
          useValue: { ask: vi.fn().mockResolvedValue(true) },
        },
        { provide: Router, useValue: { navigate: vi.fn() } },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ batchKey: "review-batch" }),
            },
          },
        },
      ],
    });
    const page = TestBed.runInInjectionContext(
      () => new BulkWorkspaceComponent(),
    );
    await page["load"]();
    return { page, service, batch };
  }

  it("loads pages beyond 200 rows and resets the page when filtering", async () => {
    const { page, service } = await setup();
    page["source"].pageIndex(9);
    await page["load"]();
    expect(service.getBatch).toHaveBeenLastCalledWith("review-batch", {
      skip: 225,
      take: 25,
      needsAttentionOnly: false,
    });
    expect(page["willWrite"]()).toBe(252);
    page["source"].pageSize(50);
    page["source"].pageIndex(4);
    await page["load"]();
    expect(service.getBatch).toHaveBeenLastCalledWith("review-batch", {
      skip: 200,
      take: 50,
      needsAttentionOnly: false,
    });
    await page["setFilter"](true);
    expect(service.getBatch).toHaveBeenLastCalledWith("review-batch", {
      skip: 0,
      take: 50,
      needsAttentionOnly: true,
    });
    expect(page["willWrite"]()).toBe(252);
    page.ngOnDestroy();
  });

  it("retains the preview and reference after commit failure, then refreshes safely", async () => {
    const { page, service, batch } = await setup();
    service.commit.mockRejectedValue({
      status: 400,
      error: {
        title: "The request is invalid.",
        correlationId: "test-reference",
      },
    });
    await page["commit"]();
    expect(page["page"]()).toBe(batch);
    expect(page["failure"]()).toBe("The request is invalid.");
    expect(page["correlationId"]()).toBe("test-reference");
    expect(page["committed"]()).toBe("");
    expect(page["busy"]()).toBe(false);
    service.getBatch.mockRejectedValueOnce({ status: 503 });
    await page["load"]();
    expect(page["page"]()).toBe(batch);
    await page["load"]();
    expect(page["failure"]()).toBe("");
    service.commit.mockResolvedValue({
      createdRowCount: 251,
      updatedRowCount: 1,
      remainingInvalidRowCount: 1,
    });
    await page["commit"]();
    expect(page["committed"]()).toContain("251 created and 1 updated");
    page.ngOnDestroy();
  });

  it("clamps an emptied final page after a correction", async () => {
    const { page, service, batch } = await setup();
    page["source"].pageIndex(1);
    service.getBatch.mockResolvedValue({ ...batch, totalCount: 25, items: [] });
    await page["load"]();
    expect(page["source"].pageIndex()).toBe(0);
    expect(service.getBatch).toHaveBeenLastCalledWith("review-batch", {
      skip: 0,
      take: 25,
      needsAttentionOnly: false,
    });
    page.ngOnDestroy();
  });
});
