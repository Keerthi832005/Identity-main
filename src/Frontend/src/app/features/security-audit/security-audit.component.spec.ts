import { ConfirmationService } from "../../shared/ui/confirmation/confirmation.service";
import { TestBed } from "@angular/core/testing";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SecurityAuditComponent } from "./security-audit.component";
import { SecurityAuditService } from "./security-audit.service";
import { UserAccessService } from "../user-access/user-access.service";

const correlationId = "aa11bb22-cc33-4d44-8e55-ff6677889900";

function operations(totalAuditCount: number, skip = 0) {
  return {
    skip,
    take: 50,
    totalAuditCount,
    audits: [
      {
        authenticationAuditId: 1,
        userId: 42,
        applicationId: 7,
        eventType: "LoginFailed",
        succeeded: false,
        failureCode: "invalid_credentials",
        correlationId,
        occurredAt: "2026-08-31T00:00:00Z",
      },
    ],
    sessions: [],
    generatedAt: "2026-08-31T00:00:00Z",
  };
}

describe("Audit timeline", () => {
  const service = { load: vi.fn(), revoke: vi.fn() };
  const writeText = vi.fn(async () => {});

  beforeEach(() => {
    vi.resetAllMocks();
    service.load.mockResolvedValue(operations(120));
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: { writeText },
    });
    TestBed.configureTestingModule({
      providers: [
        {
          provide: ConfirmationService,
          useValue: { ask: vi.fn().mockResolvedValue(true) },
        },
        { provide: SecurityAuditService, useValue: service },
        /* The principal and application pickers search through this; these tests never open them,
           so a stub keeps the spec off RUNTIME_CONFIG and the real HTTP stack. */
        {
          provide: UserAccessService,
          useValue: {
            searchUsers: vi.fn().mockResolvedValue({ items: [] }),
            searchApplications: vi.fn().mockResolvedValue({ items: [] }),
          },
        },
      ],
    });
  });

  function screen() {
    return TestBed.runInInjectionContext(() => new SecurityAuditComponent());
  }

  it("asks the server for one page and reports the window it is showing", async () => {
    const page = screen();
    await page["load"]();

    expect(service.load).toHaveBeenCalledWith(
      expect.objectContaining({ skip: 0, take: 50 }),
    );
    expect([page["firstShown"](), page["lastShown"]()]).toEqual([1, 1]);
    expect(page["total"]()).toBe(120);
    expect(page["hasMore"]()).toBe(true);
  });

  it("keeps the filter while paging and returns to the first page when it changes", async () => {
    const page = screen();
    page["eventType"].set("LoginFailed");
    await page["load"]();

    await page["load"](50);
    expect(service.load).toHaveBeenLastCalledWith(
      expect.objectContaining({ skip: 50, eventType: "LoginFailed" }),
    );

    /* Applying a filter from page two must not leave the reader beyond the new result count. */
    await page["load"]();
    expect(page["skip"]()).toBe(0);
  });

  it("stops offering an older page at the end of the trail", async () => {
    const page = screen();
    service.load.mockResolvedValueOnce(operations(40));
    await page["load"]();

    expect(page["hasMore"]()).toBe(false);
  });

  it("copies a correlation id and confirms against that id alone", async () => {
    const page = screen();
    await page["load"]();

    await page["copyCorrelationId"](correlationId);

    expect(writeText).toHaveBeenCalledWith(correlationId);
    expect(page["copied"]()).toBe(correlationId);
  });

  it("explains a blocked clipboard rather than raising an error", async () => {
    const page = screen();
    writeText.mockRejectedValueOnce(new Error("denied"));

    await page["copyCorrelationId"](correlationId);

    expect(page["copied"]()).toBeNull();
    expect(page["error"]()).toBeNull();
    expect(page["notice"]()).toContain("Select the id to copy it");
  });

  it("traces every event in one request from the event itself", async () => {
    const page = screen();
    await page["load"](50);

    page["traceCorrelation"](correlationId);
    await Promise.resolve();
    await Promise.resolve();

    expect(service.load).toHaveBeenLastCalledWith(
      expect.objectContaining({ skip: 0, correlationId }),
    );
  });
});
