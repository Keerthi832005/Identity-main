import {
  Component,
  ChangeDetectionStrategy,
  OnInit,
  OnDestroy,
  inject,
  signal,
  computed,
  DestroyRef,
} from "@angular/core";
import { ActivatedRoute, Router } from "@angular/router";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { DatePipe, DecimalPipe, DOCUMENT } from "@angular/common";
import { HttpErrorResponse } from "@angular/common/http";
import { DxButtonModule } from "devextreme-angular/ui/button";
import { AppPageComponent } from "../../shared/ui/design-system/app-page.component";
import { AppInlineAlertComponent } from "../../shared/ui/design-system/app-inline-alert.component";
import { AppBackButtonComponent } from "../../shared/ui/design-system/app-back-button.component";
import {
  AgentInventoryService,
  AgentDetails,
  AgentMachine,
  MachinePingResult,
  AgentControlStatus,
  AgentUpdateStatus,
} from "./agent-inventory.service";
import type { Column } from "devextreme/ui/data_grid";
import { AppDataGridComponent } from "../../shared/ui/design-system/app-data-grid.component";
import { AppGridCellDirective } from "../../shared/ui/design-system/app-grid-cell.directive";
import { InstalledSoftwareGridComponent } from "./installed-software-grid.component";
import { machineStatus } from "./agent-inventory-status";
export { machineStatus } from "./agent-inventory-status";

export function diskUsedPercent(disk: {
  totalBytes: number;
  freeBytes: number;
}): number {
  return disk.totalBytes > 0
    ? Math.max(0, Math.min(100, 100 * (1 - disk.freeBytes / disk.totalBytes)))
    : 0;
}
export function diskSpaceLow(disk: {
  totalBytes: number;
  freeBytes: number;
}): boolean {
  return (
    disk.totalBytes > 0 &&
    (disk.freeBytes / disk.totalBytes < 0.1 || disk.freeBytes < 10 * 1073741824)
  );
}
export function uptimeLabel(seconds: number): string {
  const minutes = Math.floor(Math.max(0, seconds) / 60);
  if (!minutes) return "Less than a minute";
  const days = Math.floor(minutes / 1440);
  return `${days ? days + "d " : ""}${Math.floor((minutes % 1440) / 60)}h ${minutes % 60}m`;
}
export function trustExpiring(machine: AgentMachine, now: number): boolean {
  if (!machine.isActive || !machine.isTrusted || !machine.trustedUntil)
    return false;
  return Date.parse(machine.trustedUntil) <= now + 7 * 86400000;
}
export const pingMessages: Record<MachinePingResult["status"], string> = {
  reachable: "Network reachable",
  noReply:
    "No ICMP reply — the PC may be offline or its firewall may block ping.",
  unresolved: "IAM could not resolve this machine to a usable network address.",
  unavailable:
    "Ping is unavailable on the IAM server. Check server permissions and network policy.",
  unsupportedHostname:
    "Ping requires a Windows computer name, not an IP address or fully qualified domain name.",
  busy: "IAM is processing other ping checks. Please retry shortly.",
};
export function supportsAgentUpdates(
  version: string | null | undefined,
): boolean {
  if (!version || !/^\d+\.\d+\.\d+(\.\d+)?$/.test(version)) return false;
  const [major, minor, patch] = version.split(".").map(Number);
  return major > 1 || (major === 1 && (minor > 0 || patch >= 1));
}
/* The worker answers collection on its own channel and updates itself over the signed feed, so this
   reads the worker version - the supervisor needs a local administrator and would never qualify. */
export function supportsAgentCollection(
  version: string | null | undefined,
): boolean {
  if (!version || !/^\d+\.\d+\.\d+(\.\d+)?$/.test(version)) return false;
  const [major, minor, patch] = version.split(".").map(Number);
  return major > 1 || (major === 1 && (minor > 0 || patch >= 6));
}
export const collectMessages: Record<AgentUpdateStatus["status"], string> = {
  queued: "Queued — waiting for the agent to connect",
  received: "Received — the machine is collecting its inventory now",
  collected: "Inventory received",
  updated: "Inventory received",
  upToDate: "Inventory received",
  rejected:
    "Report rejected — the inventory was collected but this service would not accept it",
  failed: "Collection failed — the agent could not read its inventory",
  expired:
    "Request expired — the machine did not connect within the request window",
};
export const updateMessages: Record<AgentUpdateStatus["status"], string> = {
  queued: "Queued — waiting for the agent to connect",
  received:
    "Received — checking the signed release and applying any newer version",
  updated: "Updated successfully",
  upToDate: "Already up to date",
  collected: "Inventory received",
  rejected:
    "Report rejected — this service would not accept the agent's report",
  failed:
    "Update failed — review the local agent update log; the previous healthy version is retained when rollback succeeds",
  expired:
    "Request expired — the agent did not finish within the request window",
};

@Component({
  selector: "app-agent-inventory",
  imports: [
    AppPageComponent,
    AppInlineAlertComponent,
    AppBackButtonComponent,
    DxButtonModule,
    DatePipe,
    DecimalPipe,
    InstalledSoftwareGridComponent,
    AppDataGridComponent,
    AppGridCellDirective,
  ],
  templateUrl: "./agent-inventory.component.html",
  styleUrl: "./agent-inventory.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AgentInventoryComponent implements OnInit, OnDestroy {
  private readonly document = inject(DOCUMENT);
  readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly service = inject(AgentInventoryService);
  protected readonly sessionColumns: Column[] = [
    { dataField: "userName", caption: "User", minWidth: 120 },
    { dataField: "state", caption: "State", width: 110 },
    { dataField: "sessionId", caption: "Session", width: 90 },
    {
      dataField: "isConsole",
      caption: "Console",
      dataType: "boolean",
      width: 90,
    },
  ];
  protected readonly profileColumns: Column[] = [
    { dataField: "userName", caption: "Profile", minWidth: 160 },
    {
      dataField: "loaded",
      caption: "State",
      width: 120,
      cellTemplate: "profileLoaded",
    },
  ];
  readonly details = signal<AgentDetails | null>(null);
  readonly detailBusy = signal(false);
  readonly error = signal("");
  readonly selectedId = signal<string | null>(null);
  readonly now = signal(Date.now());
  readonly pingBusy = signal(false);
  readonly pingResult = signal<MachinePingResult | null>(null);
  readonly pingError = signal("");
  readonly exportStatus = signal("");
  readonly controlStatus = signal<AgentControlStatus | null>(null);
  readonly controlError = signal("");
  readonly updateBusy = signal(false);
  readonly updateError = signal("");
  readonly updateMessages = updateMessages;
  readonly collectBusy = signal(false);
  readonly collectError = signal("");
  readonly collectMessages = collectMessages;
  readonly supportsUpdates = computed(() =>
    supportsAgentUpdates(this.controlStatus()?.supervisorVersion),
  );
  readonly supportsCollection = computed(() =>
    supportsAgentCollection(this.controlStatus()?.agentVersion),
  );
  /* The heartbeat is minutes old at worst; the inventory report can be hours behind. */
  readonly runningVersion = computed(
    () =>
      this.controlStatus()?.agentVersion ||
      this.details()?.machine.agentVersion ||
      "",
  );
  readonly collectPending = computed(() => {
    const collect = this.controlStatus()?.collect;
    return (
      !!collect &&
      ["queued", "received"].includes(collect.status) &&
      Date.parse(collect.expiresAt) > this.now()
    );
  });
  readonly updatePending = computed(() => {
    const update = this.controlStatus()?.update;
    return (
      !!update &&
      ["queued", "received"].includes(update.status) &&
      Date.parse(update.expiresAt) > this.now()
    );
  });
  readonly agentOnline = computed(() => {
    const seen = this.controlStatus()?.lastSeenAt;
    return (
      !!this.details()?.machine.isActive &&
      !!seen &&
      this.now() - Date.parse(seen) < 90_000
    );
  });
  readonly pingMessages = pingMessages;
  readonly diskUsedPercent = diskUsedPercent;
  readonly diskSpaceLow = diskSpaceLow;
  readonly uptimeLabel = uptimeLabel;
  readonly trustExpiring = trustExpiring;
  readonly status = machineStatus;
  private timer?: ReturnType<typeof setInterval>;
  private controlTimer?: ReturnType<typeof setTimeout>;
  private controlGeneration = 0;
  private updateGeneration = 0;
  private generation = 0;
  private pingGeneration = 0;
  private alive = true;
  ngOnInit(): void {
    this.route.paramMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((params) => {
        const id = params.get("installationId");
        if (id) void this.select(id);
      });
    this.timer = setInterval(() => {
      this.now.set(Date.now());
      if (!this.detailBusy()) void this.refresh();
    }, 60_000);
  }
  ngOnDestroy(): void {
    clearInterval(this.timer);
    clearTimeout(this.controlTimer);
    this.controlGeneration++;
    this.updateGeneration++;
    this.generation++;
    this.pingGeneration++;
    this.alive = false;
  }
  backToMachines(): void {
    void this.router.navigate(["/agents"], {
      queryParams: this.route.snapshot.queryParams,
    });
  }
  async refresh(): Promise<void> {
    const id = this.selectedId();
    if (id && !this.detailBusy()) await this.select(id);
  }
  async select(id: string): Promise<void> {
    this.error.set("");
    const generation = ++this.generation;
    if (id !== this.selectedId()) {
      clearTimeout(this.controlTimer);
      this.controlGeneration++;
      this.updateGeneration++;
      this.controlStatus.set(null);
      this.controlError.set("");
      this.updateError.set("");
      this.updateBusy.set(false);
      this.collectError.set("");
      this.collectBusy.set(false);
      this.pingGeneration++;
      this.pingBusy.set(false);
      this.pingResult.set(null);
      this.pingError.set("");
      this.exportStatus.set("");
      this.details.set(null);
    }
    this.selectedId.set(id);
    this.detailBusy.set(true);
    try {
      const data = await this.service.get(id);
      if (generation === this.generation) {
        this.details.set(data);
        await this.loadControl(id);
      }
    } catch {
      if (generation === this.generation)
        this.error.set("Machine details could not be loaded. Please retry.");
    } finally {
      if (generation === this.generation) this.detailBusy.set(false);
    }
  }

  async loadControl(id: string): Promise<void> {
    if (
      !this.alive ||
      id !== this.selectedId() ||
      this.updateBusy() ||
      this.collectBusy()
    )
      return;
    clearTimeout(this.controlTimer);
    const generation = ++this.controlGeneration;
    try {
      const status = await this.service.control(id);
      if (!this.alive || generation !== this.controlGeneration) return;
      this.now.set(Date.now());
      this.controlStatus.set(status);
      this.controlError.set("");
    } catch {
      if (this.alive && generation === this.controlGeneration)
        this.controlError.set(
          "Agent connection and update status could not be refreshed. Displayed status may be stale.",
        );
    } finally {
      if (this.alive && generation === this.controlGeneration) {
        const status = this.controlStatus();
        const inFlight = [status?.update, status?.collect].some(
          (command) =>
            command &&
            ["queued", "received"].includes(command.status) &&
            Date.parse(command.expiresAt) > Date.now(),
        );
        if (inFlight)
          this.controlTimer = setTimeout(
            () => void this.loadControl(id),
            5_000,
          );
      }
    }
  }

  async requestUpdate(): Promise<void> {
    const machine = this.details()?.machine;
    if (
      !machine?.isActive ||
      !this.supportsUpdates() ||
      this.updateBusy() ||
      this.updatePending() ||
      this.detailBusy()
    )
      return;
    const generation = ++this.updateGeneration;
    this.controlGeneration++;
    clearTimeout(this.controlTimer);
    this.updateBusy.set(true);
    this.updateError.set("");
    try {
      const update = await this.service.requestUpdate(machine.installationId);
      if (!this.alive || generation !== this.updateGeneration) return;
      this.controlStatus.update((status) =>
        status ? { ...status, update } : status,
      );
    } catch (error) {
      if (this.alive && generation === this.updateGeneration)
        this.updateError.set(
          error instanceof HttpErrorResponse && error.status === 429
            ? "Update request limit reached. Wait one minute, then retry."
            : "The update request could not be confirmed. Refresh status before retrying; the request may already be queued.",
        );
    } finally {
      if (this.alive && generation === this.updateGeneration) {
        this.updateBusy.set(false);
        await this.loadControl(machine.installationId);
      }
    }
  }

  async requestCollect(): Promise<void> {
    const machine = this.details()?.machine;
    if (
      !machine?.isActive ||
      !this.supportsCollection() ||
      this.collectBusy() ||
      this.collectPending() ||
      this.detailBusy()
    )
      return;
    const generation = ++this.updateGeneration;
    this.controlGeneration++;
    clearTimeout(this.controlTimer);
    this.collectBusy.set(true);
    this.collectError.set("");
    try {
      const collect = await this.service.requestCollect(machine.installationId);
      if (!this.alive || generation !== this.updateGeneration) return;
      this.controlStatus.update((status) =>
        status ? { ...status, collect } : status,
      );
    } catch (error) {
      if (this.alive && generation === this.updateGeneration)
        this.collectError.set(
          error instanceof HttpErrorResponse && error.status === 429
            ? "Report request limit reached. Wait one minute, then retry."
            : "The report request could not be confirmed. Refresh status before retrying; the request may already be queued.",
        );
    } finally {
      if (this.alive && generation === this.updateGeneration) {
        this.collectBusy.set(false);
        await this.loadControl(machine.installationId);
      }
    }
  }

  async ping(): Promise<void> {
    const machine = this.details()?.machine;
    if (!machine?.isActive || this.pingBusy() || this.detailBusy()) return;
    const generation = ++this.pingGeneration;
    this.pingBusy.set(true);
    this.pingResult.set(null);
    this.pingError.set("");
    try {
      const result = await this.service.ping(machine.installationId);
      if (this.alive && generation === this.pingGeneration)
        this.pingResult.set(result);
    } catch (error) {
      if (this.alive && generation === this.pingGeneration)
        this.pingError.set(
          error instanceof HttpErrorResponse && error.status === 429
            ? "Ping limit reached. Wait one minute, then retry."
            : "Ping could not be completed. Refresh the machine and retry.",
        );
    } finally {
      if (this.alive && generation === this.pingGeneration)
        this.pingBusy.set(false);
    }
  }

  exportInventory(): void {
    const details = this.details();
    const window = this.document.defaultView;
    if (!details || !window) return;
    let url: string | undefined;
    try {
      const data = JSON.stringify(
        {
          exportedAt: new Date().toISOString(),
          ...details,
          networkPing: this.pingResult(),
          agentControl: this.controlStatus(),
        },
        null,
        2,
      );
      url = window.URL.createObjectURL(
        new Blob([data], { type: "application/json" }),
      );
      const link = this.document.createElement("a");
      link.href = url;
      link.download = `IAM-${details.machine.hostname.replace(/[^a-zA-Z0-9_-]/g, "_")}-inventory.json`;
      link.click();
      this.exportStatus.set(
        "Inventory downloaded. Handle this machine information as internal data.",
      );
    } catch {
      this.exportStatus.set("Inventory could not be downloaded. Please retry.");
    } finally {
      if (url) window.setTimeout(() => window.URL.revokeObjectURL(url!), 0);
    }
  }
}
