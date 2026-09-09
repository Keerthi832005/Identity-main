import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from "@angular/core";
import { DatePipe, DOCUMENT } from "@angular/common";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { DxButtonModule } from "devextreme-angular/ui/button";
import { DxDataGridModule } from "devextreme-angular/ui/data-grid";
import type { Column } from "devextreme/ui/data_grid";
import { AppPageComponent } from "../../shared/ui/design-system/app-page.component";
import { AppInlineAlertComponent } from "../../shared/ui/design-system/app-inline-alert.component";
import { AppDataGridComponent } from "../../shared/ui/design-system/app-data-grid.component";
import { AppGridCellDirective } from "../../shared/ui/design-system/app-grid-cell.directive";
import { AppFormControlsModule } from "../../shared/ui/form-controls/app-form-controls.module";
import { RUNTIME_CONFIG } from "../../core/config/runtime-config";
import { AgentInventoryService, AgentPage } from "./agent-inventory.service";
import { machineStatus } from "./agent-inventory-status";

@Component({
  selector: "app-agent-inventory-list",
  imports: [
    DatePipe,
    RouterLink,
    DxButtonModule,
    AppPageComponent,
    AppInlineAlertComponent,
    AppDataGridComponent,
    AppGridCellDirective,
  ],
  templateUrl: "./agent-inventory-list.component.html",
  styleUrl: "./agent-inventory-list.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AgentInventoryListComponent implements OnInit, OnDestroy {
  private readonly service = inject(AgentInventoryService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);
  private readonly config = inject(RUNTIME_CONFIG);
  readonly downloadUrl =
    this.config.identityBaseUrl + "/api/v1/agents/download";
  readonly installCommand = `irm '${new URL(this.config.identityBaseUrl.replace(/\/$/, "") + "/install.ps1", this.document.baseURI).href.replace(/'/g, "''")}' | iex`;
  readonly copyStatus = signal("");
  readonly page = signal<AgentPage>({ items: [], total: 0, skip: 0, take: 50 });
  readonly query = signal("");
  readonly appliedQuery = signal("");
  readonly busy = signal(false);
  readonly error = signal("");
  readonly now = signal(Date.now());
  readonly rows = computed(() =>
    this.page().items.map((machine) => ({
      ...machine,
      reportingStatus: machineStatus(machine, this.now()),
      currentWindowsUser:
        machine.currentUserName ||
        (machine.windowsUsersReported
          ? "No single active user"
          : "Not reported"),
    })),
  );
  readonly listQueryParams = computed(() => ({
    search: this.appliedQuery() || null,
    page: Math.floor(this.page().skip / 50) + 1,
  }));
  readonly columns: Column[] = [
    {
      dataField: "currentWindowsUser",
      caption: "Current Windows user",
      minWidth: 200,
      visibleIndex: 1,
    },
    {
      dataField: "hostname",
      visibleIndex: 0,
      caption: "Machine",
      minWidth: 200,
      cellTemplate: "machineName",
      allowHiding: false,
    },
    {
      dataField: "deviceId",
      caption: "Terminal ID",
      dataType: "number",
      minWidth: 105,
    },
    {
      dataField: "agentVersion",
      caption: "Agent version",
      minWidth: 110,
      customizeText: ({ value }) => value || "Pending",
    },
    {
      dataField: "reportingStatus",
      caption: "Reporting status",
      minWidth: 180,
      cellTemplate: "reportingStatus",
    },
    {
      dataField: "isTrusted",
      caption: "IAM trust",
      minWidth: 130,
      cellTemplate: "trustStatus",
    },
    {
      dataField: "lastReportAt",
      caption: "Last report",
      minWidth: 190,
      cellTemplate: "lastReport",
    },
    {
      dataField: "createdAt",
      caption: "Enrolled at",
      dataType: "datetime",
      format: "dd MMM yyyy, HH:mm",
      minWidth: 180,
      visible: false,
    },
  ];
  private generation = 0;
  private timer?: ReturnType<typeof setInterval>;

  ngOnInit(): void {
    this.route.queryParamMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((params) => {
        const page = Number(params.get("page"));
        const skip =
          Number.isSafeInteger(page) && page > 0 && page <= 1000000
            ? (page - 1) * 50
            : 0;
        this.query.set(params.get("search") ?? "");
        this.appliedQuery.set(this.query().trim());
        void this.load(skip);
      });
    this.timer = setInterval(() => {
      if (!this.busy()) void this.load(this.page().skip);
    }, 60000);
  }
  ngOnDestroy(): void {
    clearInterval(this.timer);
    this.generation++;
  }
  async load(skip = this.page().skip): Promise<void> {
    const generation = ++this.generation;
    this.busy.set(true);
    this.error.set("");
    try {
      const page = await this.service.search(this.appliedQuery(), skip);
      if (generation !== this.generation) return;
      if (skip > 0 && !page.items.length) {
        await this.navigatePage(
          Math.max(0, Math.ceil(page.total / 50) - 1) * 50,
        );
        return;
      }
      this.page.set(page);
      this.now.set(Date.now());
    } catch {
      if (generation === this.generation)
        this.error.set(
          "Machine inventory could not be refreshed. Displayed data may be stale; please retry.",
        );
    } finally {
      if (generation === this.generation) this.busy.set(false);
    }
  }
  async search(): Promise<void> {
    const query = this.query().trim();
    if (query === this.appliedQuery() && this.page().skip === 0) {
      await this.load(0);
      return;
    }
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { search: query || null, page: 1 },
    });
  }
  async navigatePage(skip: number): Promise<void> {
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        search: this.appliedQuery() || null,
        page: Math.floor(skip / 50) + 1,
      },
    });
  }
  async copyInstallCommand(): Promise<void> {
    try {
      const clipboard = this.document.defaultView?.navigator.clipboard;
      if (!clipboard) throw new Error("Clipboard unavailable");
      await clipboard.writeText(this.installCommand);
      this.copyStatus.set("Install command copied.");
    } catch {
      this.copyStatus.set("Select the command and copy it manually.");
    }
  }
}
