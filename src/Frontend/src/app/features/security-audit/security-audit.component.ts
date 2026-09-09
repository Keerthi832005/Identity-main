import { ConfirmationService } from "../../shared/ui/confirmation/confirmation.service";
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from "@angular/core";
import { DxButtonModule } from "devextreme-angular/ui/button";
import { DxDataGridModule } from "devextreme-angular/ui/data-grid";
import type { Column } from "devextreme/ui/data_grid";
import {
  AuditSummary,
  SecurityOperations,
  SecurityOperationsFilter,
} from "./security-audit.models";
import { SecurityAuditService } from "./security-audit.service";
import { UserAccessService } from "../user-access/user-access.service";
import {
  AppLookupComponent,
  LookupSearch,
} from "../../shared/ui/form-controls/app-lookup.component";
import { AppFormControlsModule } from "../../shared/ui/form-controls/app-form-controls.module";
import { BulkActionsComponent } from "../../shared/bulk-data/bulk-actions.component";
import { AppInlineAlertComponent } from "../../shared/ui/design-system/app-inline-alert.component";
import { AppPageComponent } from "../../shared/ui/design-system/app-page.component";
import { AppSkeletonComponent } from "../../shared/ui/design-system/app-skeleton.component";
import { AppDataGridComponent } from "../../shared/ui/design-system/app-data-grid.component";
import { AppGridCellDirective } from "../../shared/ui/design-system/app-grid-cell.directive";
import {
  AppChipSelectComponent,
  ChipOption,
} from "../../shared/ui/design-system/app-chip-select.component";
import {
  BulkColumn,
  BulkPasteRow,
} from "../../shared/bulk-data/bulk-data.models";
import { collectAllPages } from "../../shared/bulk-data/export-paging";

@Component({
  selector: "app-security-audit",
  imports: [
    BulkActionsComponent,
    AppInlineAlertComponent,
    AppPageComponent,
    AppSkeletonComponent,
    DxButtonModule,
    AppDataGridComponent,
    AppGridCellDirective,
    AppChipSelectComponent,
    AppFormControlsModule,
    AppLookupComponent,
  ],
  templateUrl: "./security-audit.component.html",
  styleUrl: "./security-audit.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SecurityAuditComponent implements OnInit {
  private readonly service = inject(SecurityAuditService);
  private readonly users = inject(UserAccessService);
  private readonly confirmations = inject(ConfirmationService);

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.filterTimer));
  }
  protected readonly data = signal<SecurityOperations | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly sessionRows = computed(() =>
    (this.data()?.sessions ?? []).map((session) => ({
      ...session,
      userName:
        session.userDisplayName ||
        session.employeeCode ||
        `User ${session.userId}`,
      applicationName:
        session.applicationName || `Application ${session.applicationId}`,
      clientName: session.clientName || `Client ${session.applicationClientId}`,
      status: session.isActive ? "Active" : "Closed",
    })),
  );
  protected readonly sessionColumns: Column[] = [
    {
      dataField: "userId",
      caption: "User ID",
      dataType: "number",
      visible: false,
    },
    { dataField: "employeeCode", caption: "Employee code", visible: false },
    {
      dataField: "applicationId",
      caption: "Application ID",
      dataType: "number",
      visible: false,
    },
    {
      dataField: "applicationClientId",
      caption: "Client ID",
      dataType: "number",
      visible: false,
    },
    {
      dataField: "userName",
      caption: "User",
      minWidth: 160,
    },
    {
      dataField: "applicationName",
      caption: "Application",
      minWidth: 190,
    },
    {
      dataField: "clientName",
      caption: "Client",
      minWidth: 160,
    },
    {
      dataField: "issuedAt",
      caption: "Issued at",
      dataType: "datetime",
      format: "dd MMM yyyy, HH:mm:ss",
      minWidth: 180,
      sortOrder: "desc",
    },
    {
      dataField: "expiresAt",
      caption: "Expires at",
      dataType: "datetime",
      format: "dd MMM yyyy, HH:mm:ss",
      minWidth: 180,
    },
    {
      dataField: "status",
      caption: "Status",
      minWidth: 100,
      cellTemplate: "sessionStatus",
      lookup: { dataSource: ["Active", "Closed"] },
    },
    {
      name: "actions",
      caption: "Actions",
      width: 165,
      cellTemplate: "sessionActions",
      allowSorting: false,
      allowFiltering: false,
      allowSearch: false,
      allowHiding: false,
    },
    {
      dataField: "deviceId",
      caption: "Device ID",
      dataType: "number",
      visible: false,
    },
    {
      dataField: "lastConsumedAt",
      caption: "Last used",
      dataType: "datetime",
      format: "dd MMM yyyy, HH:mm:ss",
      minWidth: 180,
      visible: false,
    },
    {
      dataField: "revokedAt",
      caption: "Revoked at",
      dataType: "datetime",
      format: "dd MMM yyyy, HH:mm:ss",
      minWidth: 180,
      visible: false,
    },
  ];

  /* The API resolves the names; an event with no principal or application still shows a dash. */
  protected readonly auditRows = computed(() =>
    (this.data()?.audits ?? []).map((event) => ({
      ...event,
      principalName:
        event.userDisplayName ||
        event.employeeCode ||
        (event.userId === null ? "" : `User ${event.userId}`),
      applicationLabel:
        event.applicationName ||
        (event.applicationId === null
          ? ""
          : `Application ${event.applicationId}`),
    })),
  );
  protected readonly auditGridColumns: Column[] = [
    {
      dataField: "occurredAt",
      caption: "Time",
      dataType: "datetime",
      format: "dd MMM yyyy, HH:mm:ss",
      minWidth: 170,
      sortOrder: "desc",
    },
    {
      dataField: "eventType",
      caption: "Event",
      cellTemplate: "auditEvent",
      minWidth: 170,
    },
    {
      dataField: "principalName",
      caption: "Principal",
      cellTemplate: "auditPrincipal",
      minWidth: 160,
    },
    {
      dataField: "applicationLabel",
      caption: "Application",
      cellTemplate: "auditApplication",
      minWidth: 160,
    },
    {
      dataField: "succeeded",
      caption: "Result",
      dataType: "boolean",
      cellTemplate: "auditResult",
      minWidth: 120,
    },
    {
      dataField: "correlationId",
      caption: "Correlation",
      cellTemplate: "auditCorrelation",
      allowSorting: false,
      minWidth: 280,
    },
  ];

  protected readonly outcomeOptions: readonly ChipOption[] = [
    { value: "", label: "All events" },
    { value: "success", label: "Succeeded" },
    { value: "failure", label: "Failed" },
  ];
  protected readonly windowOptions: readonly ChipOption[] = [
    { value: "", label: "All time" },
    { value: "24h", label: "Last 24 hours" },
    { value: "7d", label: "Last 7 days" },
    { value: "30d", label: "Last 30 days" },
  ];
  private static readonly WINDOW_HOURS: Readonly<Record<string, number>> = {
    "24h": 24,
    "7d": 24 * 7,
    "30d": 24 * 30,
  };

  /* Export only. The server refuses an import of this entity outright, so there is no import
     affordance to hide here - the exclusion is structural, not a matter of UI. */
  protected readonly auditColumns: readonly BulkColumn[] = [
    {
      columnId: "occurredAt",
      header: "Occurred at",
      type: "Date",
      required: false,
    },
    { columnId: "eventType", header: "Event", type: "Text", required: false },
    {
      columnId: "succeeded",
      header: "Succeeded",
      type: "Boolean",
      required: false,
    },
    {
      columnId: "failureCode",
      header: "Failure code",
      type: "Text",
      required: false,
    },
    {
      columnId: "correlationId",
      header: "Correlation id",
      type: "Text",
      required: false,
    },
  ];

  /* Mirrors the filtered view an auditor is looking at, not the whole trail. */
  protected readonly exportAudit = async (): Promise<
    readonly BulkPasteRow[]
  > => {
    const audits = await this.allAudits();
    return audits.map((event, index) => ({
      sourceRowNumber: index + 3,
      values: {
        occurredAt: event.occurredAt,
        eventType: event.eventType,
        succeeded: event.succeeded ? "Yes" : "No",
        failureCode: event.failureCode,
        correlationId: event.correlationId,
      },
    }));
  };
  /* Pickers, not id fields: narrowing to one person is a normal thing to want here, and nobody
     knows their own users by primary key. Selecting is what applies the filter - typing only
     searches - so these need no debounce. */
  protected readonly userId = signal<number | null>(null);
  protected readonly applicationId = signal<number | null>(null);

  protected readonly searchUsers: LookupSearch = async (search) => {
    const page = await this.users.searchUsers(search);
    return page.items.map((user) => ({
      value: user.userId,
      label: `${user.displayName} · ${user.employeeCode}`,
    }));
  };

  protected readonly searchApplications: LookupSearch = async (search) => {
    const page = await this.users.searchApplications(search);
    return page.items.map((application) => ({
      value: application.applicationId,
      label: application.applicationName,
    }));
  };

  protected choosePrincipal(userId: number | null): void {
    this.userId.set(userId);
    this.skip.set(0);
    void this.load();
  }

  protected chooseApplication(applicationId: number | null): void {
    this.applicationId.set(applicationId);
    this.skip.set(0);
    void this.load();
  }

  protected readonly eventType = signal("");
  protected readonly result = signal("");
  protected readonly correlationId = signal("");
  protected readonly from = signal("");
  protected readonly to = signal("");
  protected readonly timeWindow = signal("");
  private filterTimer?: ReturnType<typeof setTimeout>;

  /* The trail is append-only and unbounded, so the screen never asks for all of it. */
  protected readonly pageSize = 50;
  protected readonly skip = signal(0);
  protected readonly loading = signal(false);
  protected readonly copied = signal<string | null>(null);
  protected readonly total = computed(() => this.data()?.totalAuditCount ?? 0);
  protected readonly firstShown = computed(() =>
    this.total() === 0 ? 0 : this.skip() + 1,
  );
  protected readonly lastShown = computed(() =>
    Math.min(this.skip() + (this.data()?.audits.length ?? 0), this.total()),
  );
  protected readonly hasMore = computed(
    () => this.skip() + this.pageSize < this.total(),
  );

  ngOnInit(): void {
    void this.load();
  }

  /** Applying a filter starts at the first page; paging keeps the filter. */
  protected async load(skip = 0): Promise<void> {
    this.error.set(null);
    this.loading.set(true);
    this.skip.set(skip);
    try {
      this.data.set(await this.service.load(this.filter()));
    } catch {
      this.error.set("Security operations could not be loaded.");
    } finally {
      this.loading.set(false);
    }
  }

  protected async copyCorrelationId(correlationId: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(correlationId);
      this.copied.set(correlationId);
    } catch {
      /* A denied clipboard is not a failure worth an error banner; the id is on screen to select. */
      this.copied.set(null);
      this.notice.set("Copying was blocked. Select the id to copy it.");
    }
  }

  /** One click from an event to every other event in the same request. */
  protected traceCorrelation(correlationId: string): void {
    this.correlationId.set(correlationId);
    void this.load();
  }

  /** Chip choices apply immediately - there is nothing to debounce a click against. */
  protected chooseOutcome(value: string): void {
    this.result.set(value);
    void this.load();
  }
  protected chooseTimeWindow(token: string): void {
    this.timeWindow.set(token);
    const hours = SecurityAuditComponent.WINDOW_HOURS[token];
    if (!hours) {
      this.from.set("");
      this.to.set("");
    } else {
      const to = new Date();
      this.to.set(to.toISOString());
      this.from.set(new Date(to.getTime() - hours * 3_600_000).toISOString());
    }
    void this.load();
  }

  /** Typed filters apply on their own, but only after the reader pauses - not on every keystroke. */
  protected onEventTypeInput(value: string): void {
    this.eventType.set(value);
    this.debounceLoad();
  }
  protected onCorrelationIdInput(value: string): void {
    this.correlationId.set(value);
    this.debounceLoad();
  }
  private debounceLoad(): void {
    clearTimeout(this.filterTimer);
    this.filterTimer = setTimeout(() => void this.load(), 350);
  }
  protected async revoke(tokenFamilyId: string): Promise<void> {
    if (
      !(await this.confirmations.ask({
        title: "Revoke session?",
        message: "Revoke every active refresh token in this session family?",
        confirmText: "Revoke session",
        tone: "danger",
      }))
    )
      return;
    try {
      const result = await this.service.revoke(tokenFamilyId);
      if (!result.succeeded) throw new Error();
      this.notice.set("Session family revoked.");
      await this.load();
    } catch {
      this.error.set("Session revocation failed.");
    }
  }
  protected async exportCsv(): Promise<void> {
    const audits = await this.allAudits();
    const header =
      "OccurredAt,EventType,Succeeded,FailureCode,UserId,ApplicationId,CorrelationId";
    const rows = audits.map((value) =>
      [
        value.occurredAt,
        value.eventType,
        value.succeeded,
        value.failureCode ?? "",
        value.userId ?? "",
        value.applicationId ?? "",
        value.correlationId,
      ]
        .map(this.csv)
        .join(","),
    );
    const url = URL.createObjectURL(
      new Blob([[header, ...rows].join("\n")], { type: "text/csv" }),
    );
    const link = document.createElement("a");
    link.href = url;
    link.download = "iam-security-audit.csv";
    link.click();
    URL.revokeObjectURL(url);
  }
  /* Both exports cover the whole filtered trail; the endpoint caps one request at 100 rows. */
  private allAudits(): Promise<readonly AuditSummary[]> {
    return collectAllPages((skip) =>
      this.service.load({ ...this.filter(), skip, take: 100 }).then((page) => ({
        items: page.audits,
        totalCount: page.totalAuditCount,
      })),
    );
  }
  private filter(): SecurityOperationsFilter {
    return {
      skip: this.skip(),
      take: this.pageSize,
      userId: this.userId(),
      applicationId: this.applicationId(),
      eventType: this.eventType().trim(),
      succeeded:
        this.result() === "success"
          ? true
          : this.result() === "failure"
            ? false
            : null,
      correlationId: this.correlationId().trim(),
      from: this.from(),
      to: this.to(),
    };
  }
  private readonly csv = (value: unknown): string =>
    `"${String(value).replaceAll('"', '""')}"`;
}
