import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { RUNTIME_CONFIG } from "../../core/config/runtime-config";

export interface AgentMachine {
  currentUserName?: string | null;
  windowsUsersReported?: boolean;
  installationId: string;
  deviceId: number;
  hostname: string;
  agentVersion: string | null;
  createdAt: string;
  lastReportAt: string | null;
  isActive: boolean;
  isTrusted: boolean;
  trustedUntil: string | null;
}
export interface AgentPage {
  items: AgentMachine[];
  total: number;
  skip: number;
  take: number;
}
export interface InstalledSoftware {
  name: string;
  version: string;
  publisher: string;
  installedOn?: string | null;
  estimatedSizeBytes?: number | null;
  registryView?: "32-bit" | "64-bit" | null;
}
export interface AgentDetails {
  machine: AgentMachine;
  inventory: {
    capturedAt: string;
    hostname: string;
    os: string;
    architecture: string;
    agentVersion: string;
    hardware: {
      manufacturer: string;
      model: string;
      serialNumber: string;
      cpu: string;
      logicalProcessors: number;
      memoryBytes: number;
      disks: { name: string; totalBytes: number; freeBytes: number }[];
    };
    software: InstalledSoftware[];
    collectionWarnings: string[];
    diagnostics?: {
      systemUptimeSeconds: number;
      agentUptimeSeconds: number;
    } | null;
    windowsUsers?: {
      currentUserName: string | null;
      sessionsReported: boolean;
      profilesReported: boolean;
      sessions: {
        sessionId: number;
        userName: string;
        state: string;
        isConsole: boolean;
      }[];
      profiles: { sid: string; userName: string; loaded: boolean }[];
    } | null;
  } | null;
}
export interface MachinePingResult {
  checkedAt: string;
  status:
    | "reachable"
    | "noReply"
    | "unresolved"
    | "unavailable"
    | "unsupportedHostname"
    | "busy";
  address: string | null;
  roundTripMilliseconds: number | null;
}
export interface AgentUpdateStatus {
  requestId: string;
  requestedAt: string;
  expiresAt: string;
  deliveredAt: string | null;
  completedAt: string | null;
  status:
    | "queued"
    | "received"
    | "updated"
    | "upToDate"
    | "collected"
    | "rejected"
    | "failed"
    | "expired";
  agentVersion: string | null;
}
export interface AgentControlStatus {
  lastSeenAt: string | null;
  agentVersion: string | null;
  supervisorVersion: string | null;
  update: AgentUpdateStatus | null;
  collect: AgentUpdateStatus | null;
}
@Injectable({ providedIn: "root" })
export class AgentInventoryService {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(RUNTIME_CONFIG).identityBaseUrl}/api/v1/admin/agents`;
  search(search: string, skip = 0): Promise<AgentPage> {
    return firstValueFrom(
      this.http.get<AgentPage>(this.base, {
        params: new HttpParams()
          .set("search", search)
          .set("skip", skip)
          .set("take", 50),
      }),
    );
  }
  get(id: string): Promise<AgentDetails> {
    return firstValueFrom(
      this.http.get<AgentDetails>(`${this.base}/${encodeURIComponent(id)}`),
    );
  }
  ping(id: string): Promise<MachinePingResult> {
    return firstValueFrom(
      this.http.post<MachinePingResult>(
        `${this.base}/${encodeURIComponent(id)}/ping`,
        {},
      ),
    );
  }
  control(id: string): Promise<AgentControlStatus> {
    return firstValueFrom(
      this.http.get<AgentControlStatus>(
        `${this.base}/${encodeURIComponent(id)}/control`,
      ),
    );
  }
  requestUpdate(id: string): Promise<AgentUpdateStatus> {
    return firstValueFrom(
      this.http.post<AgentUpdateStatus>(
        `${this.base}/${encodeURIComponent(id)}/update`,
        {},
      ),
    );
  }
  requestCollect(id: string): Promise<AgentUpdateStatus> {
    return firstValueFrom(
      this.http.post<AgentUpdateStatus>(
        `${this.base}/${encodeURIComponent(id)}/collect`,
        {},
      ),
    );
  }
}
