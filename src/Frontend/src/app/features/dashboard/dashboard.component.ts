import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from "@angular/core";
import { RouterLink } from "@angular/router";
import { DxChartModule } from "devextreme-angular/ui/chart";
import { DxSparklineModule } from "devextreme-angular/ui/sparkline";
import type { Column } from "devextreme/ui/data_grid";
import { AuthState } from "../../core/auth/auth-state.service";
import { ChartPaletteService } from "../../core/theme/chart-palette";
import {
  AgentInventoryService,
  AgentMachine,
} from "../agent-inventory/agent-inventory.service";
import { machineStatus } from "../agent-inventory/agent-inventory-status";
import { AppChipSelectComponent } from "../../shared/ui/design-system/app-chip-select.component";
import { AppDataGridComponent } from "../../shared/ui/design-system/app-data-grid.component";
import { AppErrorStateComponent } from "../../shared/ui/design-system/app-error-state.component";
import { AppGridCellDirective } from "../../shared/ui/design-system/app-grid-cell.directive";
import { AppPageComponent } from "../../shared/ui/design-system/app-page.component";
import { AppSectionComponent } from "../../shared/ui/design-system/app-section.component";
import { AppSkeletonComponent } from "../../shared/ui/design-system/app-skeleton.component";
import { AdministrationDashboard } from "./dashboard.models";
import { DashboardService } from "./dashboard.service";

interface TrendPoint {
  readonly hour: string;
  readonly hourLabel: string;
  readonly succeeded: number;
  readonly failed: number;
}

interface MetricTile {
  readonly label: string;
  readonly value: number;
  readonly meta: string;
  readonly tone: "neutral" | "warning" | "danger";
  readonly route: string;
  readonly sparkline?: readonly TrendPoint[];
}

type ActivityCategory = "Authentication" | "Administration";

/* Mirrors AuthenticationAuditEventType on the backend - anything outside this set is treated as
   an administration event, so the split stays correct even if the audit source ever widens. */
const AUTHENTICATION_EVENT_TYPES = new Set([
  "LoginSucceeded",
  "LoginFailed",
  "TokenRefreshed",
  "TokenRefreshRejected",
  "LoggedOut",
  "CredentialChanged",
  "ClientAuthenticated",
  "ClientAuthenticationFailed",
  "MfaMethodAdded",
  "MfaMethodVerified",
  "MfaMethodVerificationRejected",
  "MfaMethodRevoked",
  "MfaChallengeCreated",
  "MfaChallengeVerified",
  "MfaChallengeRejected",
  "DeviceTrusted",
  "TerminalVerificationSucceeded",
  "TerminalVerificationFailed",
]);

@Component({
  selector: "app-dashboard",
  standalone: true,
  imports: [
    AppChipSelectComponent,
    AppDataGridComponent,
    AppErrorStateComponent,
    AppGridCellDirective,
    AppPageComponent,
    AppSectionComponent,
    AppSkeletonComponent,
    DxChartModule,
    DxSparklineModule,
    RouterLink,
  ],
  templateUrl: "./dashboard.component.html",
  styleUrl: "./dashboard.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent implements OnInit {
  protected readonly auth = inject(AuthState);
  private readonly dashboardService = inject(DashboardService);
  private readonly agentInventory = inject(AgentInventoryService);
  protected readonly chartPalette = inject(ChartPaletteService);

  protected readonly dashboard = signal<AdministrationDashboard | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal("");
  protected readonly correlationId = signal("");

  protected readonly machinesNotReporting = signal(0);

  protected readonly failuresView = signal<"chart" | "table">("chart");
  protected readonly failuresViewOptions = [
    { value: "chart", label: "Chart" },
    { value: "table", label: "Table" },
  ];

  protected readonly activityFilter = signal<"all" | ActivityCategory>("all");
  protected readonly activityFilterOptions = [
    { value: "all", label: "All" },
    { value: "Authentication", label: "Authentication" },
    { value: "Administration", label: "Administration" },
  ];

  /* Server-supplied, hour-aligned series for the last 24 hours - always a complete run of
     buckets, so the chart never needs to fill gaps itself. */
  protected readonly trend = computed<readonly TrendPoint[]>(() =>
    (this.dashboard()?.authenticationTrend ?? []).map((point) => ({
      ...point,
      hourLabel: this.hourFormatter.format(new Date(point.hour)),
    })),
  );

  /* Stacked so failures read as a coloured cap on top of the (much larger) success bar - the
     eye goes straight to red instead of having to compare two same-toned series. */
  protected readonly trendSeries = computed(() => [
    {
      argumentField: "hourLabel",
      valueField: "succeeded",
      type: "bar" as const,
      color: this.chartPalette.palette().neutral,
      name: "Succeeded",
      stack: "authentication",
    },
    {
      argumentField: "hourLabel",
      valueField: "failed",
      type: "bar" as const,
      color: this.chartPalette.palette().danger,
      name: "Failed",
      stack: "authentication",
    },
  ]);

  protected readonly hourlyColumns: Column[] = [
    { dataField: "hourLabel", caption: "Hour", width: 110 },
    { dataField: "succeeded", caption: "Succeeded", dataType: "number" },
    { dataField: "failed", caption: "Failed", dataType: "number" },
  ];

  protected readonly metrics = computed<readonly MetricTile[]>(() => {
    const data = this.dashboard();
    if (!data) return [];
    const failed = data.failedAuthenticationCountLast24Hours;
    const notReporting = this.machinesNotReporting();
    const tiles: MetricTile[] = [
      {
        label: "Failed sign-ins",
        value: failed,
        meta: "in the last 24 hours",
        tone: failed > 0 ? "warning" : "neutral",
        route: "/audit",
        sparkline: this.trend(),
      },
    ];
    /* Older API responses omit these fields entirely; render nothing rather than a false zero. */
    if (data.lockedOutUserCount != null) {
      tiles.push({
        label: "Locked-out users",
        value: data.lockedOutUserCount,
        meta: "still within their lockout window",
        tone: data.lockedOutUserCount > 0 ? "danger" : "neutral",
        route: "/users",
      });
    }
    tiles.push({
      label: "Machines not reporting",
      value: notReporting,
      meta: "overdue, or awaiting a first report",
      tone: notReporting > 0 ? "danger" : "neutral",
      route: "/agents",
    });
    if (data.usersWithoutMfaCount != null) {
      tiles.push({
        label: "Users without MFA",
        value: data.usersWithoutMfaCount,
        meta: "active accounts with no verified method",
        tone: data.usersWithoutMfaCount > 0 ? "warning" : "neutral",
        route: "/users",
      });
    }
    if (data.expiringClientSecretCount != null) {
      tiles.push({
        label: "Expiring client secrets",
        value: data.expiringClientSecretCount,
        meta: "within 30 days",
        tone: data.expiringClientSecretCount > 0 ? "warning" : "neutral",
        route: "/applications",
      });
    }
    tiles.push(
      {
        label: "Live sessions",
        value: data.activeSessionCount,
        meta: "refresh families in use",
        tone: "neutral",
        route: "/audit",
      },
      {
        label: "Applications",
        value: data.applicationCount,
        meta: `${data.activeApplicationCount} active`,
        tone: "neutral",
        route: "/applications",
      },
      {
        label: "Users",
        value: data.userCount,
        meta: `${data.activeUserCount} active`,
        tone: "neutral",
        route: "/users",
      },
    );
    return tiles;
  });

  /* Fixed widths for everything but the identifier: a time, an event name and a resolved name
     have a known size, so the correlation id takes the rest of the row instead of wrapping. */
  protected readonly activityColumns: Column[] = [
    {
      dataField: "occurredAt",
      caption: "Time",
      dataType: "datetime",
      format: "dd MMM, HH:mm:ss",
      sortOrder: "desc",
      width: 170,
    },
    { dataField: "eventType", caption: "Event type", width: 200 },
    { dataField: "principalName", caption: "Principal", width: 170 },
    { dataField: "applicationLabel", caption: "Application", width: 160 },
    {
      dataField: "outcome",
      caption: "Outcome",
      cellTemplate: "outcomeChip",
      width: 120,
      allowFiltering: false,
    },
    { dataField: "correlationId", caption: "Correlation ID", minWidth: 300 },
  ];

  /* The API resolves the names; an event with no principal or application shows a dash. */
  private readonly activityRows = computed(() =>
    (this.dashboard()?.recentAuditEvents ?? []).map((event) => ({
      ...event,
      principalName:
        event.userDisplayName ||
        event.employeeCode ||
        (event.userId === null ? "—" : `User ${event.userId}`),
      applicationLabel:
        event.applicationName ||
        (event.applicationId === null
          ? "—"
          : `Application ${event.applicationId}`),
      outcome: event.succeeded ? "Succeeded" : "Failed",
      category: this.categorize(event.eventType),
    })),
  );

  protected readonly filteredActivityRows = computed(() => {
    const filter = this.activityFilter();
    const rows = this.activityRows();
    return filter === "all"
      ? rows
      : rows.filter((row) => row.category === filter);
  });

  private readonly hourFormatter = new Intl.DateTimeFormat(undefined, {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  });

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set("");
    const [dashboardResult, machinesResult] = await Promise.allSettled([
      this.dashboardService.getDashboard(10),
      this.loadMachinesNotReporting(),
    ]);

    if (dashboardResult.status === "fulfilled") {
      this.dashboard.set(dashboardResult.value);
    } else {
      const detail = dashboardResult.reason as {
        error?: { title?: string; correlationId?: string };
        status?: number;
      };
      this.correlationId.set(detail?.error?.correlationId ?? "");
      this.error.set(
        detail?.status === 0
          ? "The identity service could not be reached. Check that it is running."
          : (detail?.error?.title ??
              "Administration data could not be loaded."),
      );
    }

    /* Enriches the health strip; a failure here should not blank out a dashboard that otherwise
       loaded fine, so it defaults quietly rather than joining the error state above. */
    this.machinesNotReporting.set(
      machinesResult.status === "fulfilled" ? machinesResult.value : 0,
    );

    this.loading.set(false);
  }

  protected setFailuresView(value: string): void {
    this.failuresView.set(value === "table" ? "table" : "chart");
  }

  protected setActivityFilter(value: string): void {
    this.activityFilter.set(
      value === "Authentication" || value === "Administration" ? value : "all",
    );
  }

  protected formatDate(value: string | null): string {
    return value
      ? new Intl.DateTimeFormat(undefined, {
          dateStyle: "medium",
          timeStyle: "short",
        }).format(new Date(value))
      : "Never";
  }

  private categorize(eventType: string): ActivityCategory {
    return AUTHENTICATION_EVENT_TYPES.has(eventType)
      ? "Authentication"
      : "Administration";
  }

  /* AgentInventoryService pages at 50; a fleet larger than that is walked page by page so the
     count is exact rather than a first-page estimate. Bounded so a runaway total cannot hang the
     dashboard load. */
  private async loadMachinesNotReporting(): Promise<number> {
    const first = await this.agentInventory.search("", 0);
    const machines: AgentMachine[] = [...first.items];
    while (machines.length < first.total && machines.length < 2000) {
      const page = await this.agentInventory.search("", machines.length);
      if (!page.items.length) break;
      machines.push(...page.items);
    }
    return machines.filter((machine) => {
      const status = machineStatus(machine);
      return status === "Report overdue" || status === "Awaiting first report";
    }).length;
  }
}
