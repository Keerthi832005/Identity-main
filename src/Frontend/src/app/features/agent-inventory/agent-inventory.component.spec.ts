import { beforeEach, describe, it, expect, vi } from "vitest";
import { TestBed } from "@angular/core/testing";
import { HttpErrorResponse } from "@angular/common/http";
import {
  AgentInventoryComponent,
  machineStatus,
  diskSpaceLow,
  diskUsedPercent,
  uptimeLabel,
  trustExpiring,
  supportsAgentUpdates,
} from "./agent-inventory.component";
import {
  AgentMachine,
  AgentInventoryService,
  AgentDetails,
  MachinePingResult,
} from "./agent-inventory.service";
import { RUNTIME_CONFIG } from "../../core/config/runtime-config";
import { ActivatedRoute, convertToParamMap } from "@angular/router";
import { of } from "rxjs";
describe("machine reporting status", () => {
  const machine: AgentMachine = {
    installationId: "id",
    deviceId: 1,
    hostname: "HOST",
    agentVersion: "1.0",
    createdAt: "2026-08-31T00:00:00Z",
    lastReportAt: null,
    isActive: true,
    isTrusted: true,
    trustedUntil: null,
  };
  it("distinguishes never reported, stale, revoked and recently reporting", () => {
    const now = Date.parse("2026-08-31T12:00:00Z");
    expect(machineStatus(machine, now)).toBe("Awaiting first report");
    expect(
      machineStatus({ ...machine, lastReportAt: "2026-08-31T11:45:00" }, now),
    ).toBe("Reporting");
    expect(
      machineStatus({ ...machine, lastReportAt: "2026-08-31T10:00:00Z" }, now),
    ).toBe("Report overdue");
    expect(machineStatus({ ...machine, isActive: false }, now)).toBe("Revoked");
  });
});

describe("agent diagnostics", () => {
  const machine: AgentMachine = {
    installationId: "one",
    deviceId: 1,
    hostname: "PC-ONE",
    agentVersion: "1.0.0",
    createdAt: "2026-08-31T00:00:00Z",
    lastReportAt: null,
    isActive: true,
    isTrusted: true,
    trustedUntil: "2026-09-02T00:00:00Z",
  };
  const result: MachinePingResult = {
    checkedAt: "2026-08-31T12:00:00Z",
    status: "reachable",
    address: "192.0.2.1",
    roundTripMilliseconds: 0,
  };
  const service = {
    search: vi.fn(),
    get: vi.fn(),
    ping: vi.fn(),
    control: vi.fn(),
    requestUpdate: vi.fn(),
  };
  beforeEach(() => {
    vi.resetAllMocks();
    service.get.mockImplementation(
      async (id: string): Promise<AgentDetails> => ({
        machine: { ...machine, installationId: id },
        inventory: null,
      }),
    );
    service.ping.mockResolvedValue(result);
    service.control.mockResolvedValue({
      lastSeenAt: new Date().toISOString(),
      agentVersion: "1.0.1",
      supervisorVersion: "1.0.1",
      update: null,
    });
    TestBed.configureTestingModule({
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(convertToParamMap({ installationId: "one" })),
            snapshot: { queryParams: {} },
          },
        },
        { provide: AgentInventoryService, useValue: service },
        { provide: RUNTIME_CONFIG, useValue: { identityBaseUrl: "/identity" } },
      ],
    });
  });
  const create = () =>
    TestBed.runInInjectionContext(() => new AgentInventoryComponent());
  it("requires a capable supervisor and active enrollment before requesting an update", async () => {
    const component = create();
    await component.select("one");
    expect(component.agentOnline()).toBe(true);
    component.controlStatus.update((s) => ({
      ...s!,
      supervisorVersion: "1.0.0",
    }));
    await component.requestUpdate();
    expect(service.requestUpdate).not.toHaveBeenCalled();
    expect(supportsAgentUpdates("1.0.1")).toBe(true);
    expect(supportsAgentUpdates("2.0.0")).toBe(true);
    expect(supportsAgentUpdates("unknown")).toBe(false);
    component.controlStatus.update((s) => ({
      ...s!,
      supervisorVersion: "1.0.1",
    }));
    component.details.update((d) => ({
      ...d!,
      machine: { ...d!.machine, isActive: false },
    }));
    await component.requestUpdate();
    expect(service.requestUpdate).not.toHaveBeenCalled();
  });
  it("ignores a late update response after selecting another machine", async () => {
    let complete!: (value: unknown) => void;
    service.requestUpdate.mockReturnValue(
      new Promise((resolve) => {
        complete = resolve;
      }),
    );
    const component = create();
    await component.select("one");
    const pending = component.requestUpdate();
    await component.requestUpdate();
    expect(service.requestUpdate).toHaveBeenCalledTimes(1);
    await component.select("two");
    complete({ requestId: "old", status: "queued" });
    await pending;
    expect(component.controlStatus()?.update).toBeNull();
    expect(component.updateBusy()).toBe(false);
    component.ngOnDestroy();
  });
  it("refreshes persisted update state after an uncertain request response", async () => {
    const component = create();
    await component.select("one");
    service.requestUpdate.mockRejectedValue(
      new HttpErrorResponse({ status: 503 }),
    );
    service.control.mockResolvedValue({
      lastSeenAt: new Date().toISOString(),
      agentVersion: "1.0.1",
      supervisorVersion: "1.0.1",
      update: {
        requestId: "queued",
        status: "queued",
        expiresAt: new Date(Date.now() + 60000).toISOString(),
      },
    });
    await component.requestUpdate();
    expect(component.controlStatus()?.update?.requestId).toBe("queued");
    expect(component.updatePending()).toBe(true);
    expect(component.updateError()).toContain("may already be queued");
    component.ngOnDestroy();
  });
  it("pings the selected enrolled machine and keeps zero-millisecond replies", async () => {
    const component = create();
    await component.select("one");
    await component.ping();
    expect(service.ping).toHaveBeenCalledWith("one");
    expect(component.pingResult()?.roundTripMilliseconds).toBe(0);
    expect(component.pingBusy()).toBe(false);
  });
  it("does not allow a revoked machine to be pinged", async () => {
    const component = create();
    await component.select("one");
    component.details.set({
      machine: { ...machine, isActive: false },
      inventory: null,
    });
    await component.ping();
    expect(service.ping).not.toHaveBeenCalled();
  });
  it("ignores an earlier machine's ping after the selection changes", async () => {
    let complete!: (value: MachinePingResult) => void;
    service.ping.mockReturnValue(
      new Promise<MachinePingResult>((resolve) => {
        complete = resolve;
      }),
    );
    const component = create();
    await component.select("one");
    const pending = component.ping();
    await component.ping();
    expect(service.ping).toHaveBeenCalledTimes(1);
    await component.select("two");
    complete(result);
    await pending;
    expect(component.pingResult()).toBeNull();
    expect(component.pingBusy()).toBe(false);
  });
  it("does not apply a reply after component destruction", async () => {
    let complete!: (value: MachinePingResult) => void;
    service.ping.mockReturnValue(
      new Promise<MachinePingResult>((resolve) => {
        complete = resolve;
      }),
    );
    const component = create();
    await component.select("one");
    const pending = component.ping();
    component.ngOnDestroy();
    complete(result);
    await pending;
    expect(component.pingResult()).toBeNull();
  });
  it("clears an old success and explains rate limiting", async () => {
    const component = create();
    await component.select("one");
    await component.ping();
    service.ping.mockRejectedValue(new HttpErrorResponse({ status: 429 }));
    await component.ping();
    expect(component.pingResult()).toBeNull();
    expect(component.pingError()).toContain("Wait one minute");
  });
  it("keeps inventory report status distinct from a failed ICMP check", async () => {
    service.ping.mockResolvedValue({
      ...result,
      status: "noReply",
      roundTripMilliseconds: null,
    });
    const component = create();
    await component.select("one");
    await component.ping();
    expect(component.pingResult()?.status).toBe("noReply");
    expect(machineStatus(component.details()!.machine)).toBe(
      "Awaiting first report",
    );
  });
  it("flags low percentage or low absolute space without flagging unknown disks", () => {
    const gib = 1073741824;
    expect(diskSpaceLow({ totalBytes: 100 * gib, freeBytes: 9 * gib })).toBe(
      true,
    );
    expect(diskSpaceLow({ totalBytes: 1000 * gib, freeBytes: 20 * gib })).toBe(
      true,
    );
    expect(diskSpaceLow({ totalBytes: 20 * gib, freeBytes: 9 * gib })).toBe(
      true,
    );
    expect(diskSpaceLow({ totalBytes: 100 * gib, freeBytes: 10 * gib })).toBe(
      false,
    );
    expect(diskSpaceLow({ totalBytes: 0, freeBytes: 0 })).toBe(false);
    expect(diskUsedPercent({ totalBytes: 100, freeBytes: 25 })).toBe(75);
    expect(diskUsedPercent({ totalBytes: 0, freeBytes: 0 })).toBe(0);
  });
  it("formats captured uptime and flags expiring trust without calling it current uptime", () => {
    expect(uptimeLabel(90061)).toBe("1d 1h 1m");
    expect(uptimeLabel(20)).toBe("Less than a minute");
    const now = Date.parse("2026-08-31T00:00:00Z");
    expect(trustExpiring(machine, now)).toBe(true);
    expect(
      trustExpiring({ ...machine, trustedUntil: "2026-10-01T00:00:00Z" }, now),
    ).toBe(false);
    expect(trustExpiring({ ...machine, isActive: false }, now)).toBe(false);
  });
});
