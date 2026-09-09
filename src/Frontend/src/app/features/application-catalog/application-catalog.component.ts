import type { Column } from "devextreme/ui/data_grid";
import { RouterLink } from "@angular/router";
import { AppBackButtonComponent } from "../../shared/ui/design-system/app-back-button.component";
import { AppDataGridComponent } from "../../shared/ui/design-system/app-data-grid.component";
import { AppDirectoryGridComponent } from "../../shared/ui/design-system/app-directory-grid.component";
import { AppGridCellDirective } from "../../shared/ui/design-system/app-grid-cell.directive";
import { DirectoryRoute } from "../../shared/ui/design-system/directory-route";
import { ConfirmationService } from "../../shared/ui/confirmation/confirmation.service";
import { InlineFormNavigation } from "../../shared/ui/inline-form-navigation";
import { PageHeaderComponent } from "../../shared/page-header/page-header.component";
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  OnDestroy,
  computed,
  inject,
  signal,
} from "@angular/core";
import { DxButtonModule } from "devextreme-angular/ui/button";
import {
  ApplicationCatalog,
  ApplicationModuleSummary,
  ApplicationSummary,
  ApplicationUserSummary,
  CatalogResourceKind,
  CreateApplicationClientRequest,
  CreateApplicationRequest,
  CreateCapabilityRequest,
  CreateModuleRequest,
  ModuleTreeNode,
  RoleMemberCount,
} from "./application-catalog.models";
import { ApplicationCatalogService } from "./application-catalog.service";
import {
  ApplicationAccessCatalog,
  RoleSummary,
} from "../user-access/user-access.models";
import { AppFormControlsModule } from "../../shared/ui/form-controls/app-form-controls.module";
import { BulkActionsComponent } from "../../shared/bulk-data/bulk-actions.component";
import { AppEmptyStateComponent } from "../../shared/ui/design-system/app-empty-state.component";
import { AppInlineAlertComponent } from "../../shared/ui/design-system/app-inline-alert.component";
import { AppPageComponent } from "../../shared/ui/design-system/app-page.component";
import { AppSkeletonComponent } from "../../shared/ui/design-system/app-skeleton.component";
import { BulkPasteDirective } from "../../shared/bulk-data/bulk-paste.directive";
import {
  BulkColumn,
  BulkPasteRow,
} from "../../shared/bulk-data/bulk-data.models";

type FormMode = "application" | "client" | "module" | "capability";

@Component({
  selector: "app-application-catalog",
  imports: [
    RouterLink,
    PageHeaderComponent,
    DxButtonModule,
    AppFormControlsModule,
    BulkActionsComponent,
    BulkPasteDirective,
    AppEmptyStateComponent,
    AppInlineAlertComponent,
    AppPageComponent,

    AppSkeletonComponent,
    AppBackButtonComponent,
    AppDirectoryGridComponent,
    AppDataGridComponent,
    AppGridCellDirective,
  ],
  templateUrl: "./application-catalog.component.html",
  styleUrl: "./application-catalog.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { "(window:beforeunload)": "formNavigation.beforeUnload($event)" },
})
export class ApplicationCatalogComponent implements OnInit, OnDestroy {
  protected readonly directory = new DirectoryRoute(
    "/applications",
    "applicationId",
  );
  private loadGeneration = 0;
  private readonly service = inject(ApplicationCatalogService);
  private readonly confirmations = inject(ConfirmationService);
  protected readonly applications = signal<readonly ApplicationSummary[]>([]);
  protected readonly catalog = signal<ApplicationCatalog | null>(null);
  protected readonly access = signal<ApplicationAccessCatalog | null>(null);

  /** Role permission grants, live only - a revoked grant no longer counts toward a role. */
  private readonly roleCapabilityCounts = computed<ReadonlyMap<number, number>>(
    () => {
      const counts = new Map<number, number>();
      for (const permission of this.access()?.rolePermissions ?? []) {
        if (permission.revokedAt !== null) continue;
        counts.set(permission.roleId, (counts.get(permission.roleId) ?? 0) + 1);
      }
      return counts;
    },
  );

  protected readonly users = signal<readonly ApplicationUserSummary[]>([]);
  protected readonly usersTotal = signal(0);
  protected readonly usersSkip = signal(0);
  protected readonly usersSearch = signal("");
  protected readonly usersLoading = signal(false);
  private usersLoadGeneration = 0;

  protected readonly usersLast = computed(() =>
    Math.min(this.usersSkip() + this.users().length, this.usersTotal()),
  );

  protected readonly userColumns: Column[] = [
    {
      dataField: "displayName",
      caption: "User",
      cellTemplate: "userNameCell",
      minWidth: 200,
    },
    { dataField: "employeeCode", caption: "Employee code", minWidth: 140 },
    {
      caption: "Roles",
      cellTemplate: "userRolesCell",
      allowSorting: false,
      minWidth: 200,
    },
    {
      dataField: "assignedAt",
      caption: "Assigned",
      dataType: "datetime",
      format: "dd MMM yyyy, HH:mm",
      minWidth: 170,
    },
    {
      dataField: "isActive",
      caption: "Status",
      cellTemplate: "userStatusCell",
      minWidth: 110,
    },
  ];

  private async loadUsers(applicationId: number, skip: number): Promise<void> {
    const generation = ++this.usersLoadGeneration;
    this.usersLoading.set(true);
    try {
      const result = await this.service.getUsers(
        applicationId,
        this.usersSearch(),
        skip,
      );
      if (generation !== this.usersLoadGeneration) return;
      this.users.set(result.items);
      this.usersTotal.set(result.totalCount);
      this.usersSkip.set(skip);
    } catch {
      if (generation === this.usersLoadGeneration)
        this.error.set("Application users could not be loaded.");
    } finally {
      if (generation === this.usersLoadGeneration) this.usersLoading.set(false);
    }
  }

  protected searchUsers(term: string): void {
    this.usersSearch.set(term);
    const applicationId = this.catalog()?.application.applicationId;
    if (applicationId) void this.loadUsers(applicationId, 0);
  }

  protected pageUsers(skip: number): void {
    const applicationId = this.catalog()?.application.applicationId;
    if (applicationId) void this.loadUsers(applicationId, skip);
  }

  protected readonly clientColumns: Column[] = [
    {
      dataField: "clientId",
      caption: "Client ID",
      cellTemplate: "clientIdCell",
      minWidth: 200,
    },
    { dataField: "clientName", caption: "Name", minWidth: 160 },
    { dataField: "clientType", caption: "Type", minWidth: 120 },
    {
      dataField: "secretVersion",
      caption: "Secret version",
      calculateCellValue: (data: { secretVersion: number }) =>
        data.secretVersion || "None",
      minWidth: 130,
    },
    {
      dataField: "createdAt",
      caption: "Created",
      dataType: "datetime",
      format: "dd MMM yyyy, HH:mm",
      minWidth: 170,
    },
    {
      caption: "Actions",
      cellTemplate: "clientActionsCell",
      alignment: "right",
      allowSorting: false,
      minWidth: 140,
    },
  ];

  protected readonly roleColumns: Column[] = [
    {
      dataField: "roleName",
      caption: "Role",
      cellTemplate: "roleNameCell",
      minWidth: 220,
    },
    {
      caption: "Capabilities",
      calculateCellValue: (data: RoleSummary) =>
        this.roleCapabilityCounts().get(data.roleId) ?? 0,
      minWidth: 130,
    },
    {
      dataField: "memberCount",
      caption: "Members",
      calculateCellValue: (data: RoleSummary & RoleMemberCount) =>
        data.memberCount ?? 0,
      minWidth: 110,
    },
    {
      dataField: "isActive",
      caption: "Status",
      cellTemplate: "roleStatusCell",
      minWidth: 110,
    },
  ];

  /* Mirrors CatalogBulkDescriptors on the server; the server stays the authority. Modules are the
     highest-volume seeding task, so they are the entity this screen pastes into. */
  protected readonly moduleColumns: readonly BulkColumn[] = [
    {
      columnId: "applicationCode",
      header: "Application code",
      type: "Text",
      required: true,
    },
    {
      columnId: "moduleCode",
      header: "Module code",
      type: "Text",
      required: true,
    },
    { columnId: "name", header: "Module name", type: "Text", required: true },
    {
      columnId: "description",
      header: "Description",
      type: "Text",
      required: false,
    },
    {
      columnId: "parentModuleCode",
      header: "Parent module code",
      type: "Text",
      required: false,
    },
    {
      columnId: "displayOrder",
      header: "Display order",
      type: "Number",
      required: false,
    },
    {
      columnId: "isSystem",
      header: "System",
      type: "Boolean",
      required: false,
      allowedValues: ["Yes", "No"],
    },
  ];

  protected readonly exportModules = (): readonly BulkPasteRow[] => {
    const loaded = this.catalog();
    if (!loaded) return [];
    const byId = new Map(
      loaded.modules.map((module) => [
        module.applicationModuleId,
        module.moduleCode,
      ]),
    );
    return loaded.modules.map((module, index) => ({
      sourceRowNumber: index + 3,
      values: {
        applicationCode: loaded.application.applicationCode,
        moduleCode: module.moduleCode,
        name: module.moduleName,
        description: module.description,
        parentModuleCode: module.parentApplicationModuleId
          ? (byId.get(module.parentApplicationModuleId) ?? null)
          : null,
        displayOrder: String(module.displayOrder),
        isSystem: module.isSystem ? "Yes" : "No",
      },
    }));
  };
  /* The API returns a flat list; the parent link and display order are the hierarchy. Building it
     here rather than server-side keeps the catalog response one shape for every consumer. */
  protected readonly moduleTree = computed<readonly ModuleTreeNode[]>(() => {
    const modules = this.catalog()?.modules ?? [];
    const children = new Map<number | null, ApplicationModuleSummary[]>();
    const known = new Set(modules.map((module) => module.applicationModuleId));
    for (const module of modules) {
      /* A parent outside this application, or a revoked one that no longer loads, would otherwise
         hide its whole subtree; such a module is shown at the root instead. */
      const parent =
        module.parentApplicationModuleId !== null &&
        known.has(module.parentApplicationModuleId)
          ? module.parentApplicationModuleId
          : null;
      const siblings = children.get(parent);
      if (siblings) siblings.push(module);
      else children.set(parent, [module]);
    }
    for (const siblings of children.values()) {
      siblings.sort(
        (left, right) =>
          left.displayOrder - right.displayOrder ||
          left.applicationModuleId - right.applicationModuleId,
      );
    }

    const nodes: ModuleTreeNode[] = [];
    const walk = (parent: number | null, depth: number): void => {
      const siblings = children.get(parent) ?? [];
      siblings.forEach((module, index) => {
        const movable = (offset: number): boolean => {
          const neighbour = siblings[index + offset];
          return (
            !module.isSystem && neighbour !== undefined && !neighbour.isSystem
          );
        };
        nodes.push({
          module,
          depth,
          canMoveUp: movable(-1),
          canMoveDown: movable(1),
        });
        walk(module.applicationModuleId, depth + 1);
      });
    };
    walk(null, 0);
    return nodes;
  });

  protected readonly moduleGridColumns: Column[] = [
    {
      caption: "Module",
      cellTemplate: "moduleNameCell",
      minWidth: 260,
      allowHiding: false,
    },
    {
      caption: "Capabilities",
      cellTemplate: "moduleCapabilitiesCell",
      minWidth: 220,
      allowSorting: false,
    },
    {
      caption: "Order",
      cellTemplate: "moduleOrderCell",
      alignment: "center",
      allowSorting: false,
      minWidth: 90,
    },
    {
      caption: "Status",
      cellTemplate: "moduleStatusCell",
      minWidth: 110,
    },
  ];

  protected capabilitiesOf(moduleId: number) {
    return (this.catalog()?.capabilities ?? []).filter(
      (capability) => capability.applicationModuleId === moduleId,
    );
  }

  protected async moveModule(
    module: ApplicationModuleSummary,
    direction: "up" | "down",
  ): Promise<void> {
    const applicationId = this.catalog()?.application.applicationId;
    if (!applicationId || this.saving()) return;
    this.saving.set(true);
    this.error.set(null);
    try {
      await this.service.moveModule(
        applicationId,
        module.applicationModuleId,
        direction,
      );
      this.catalog.set(await this.service.getCatalog(applicationId));
    } catch {
      this.error.set(`${module.moduleName} could not be moved.`);
    } finally {
      this.saving.set(false);
    }
  }

  protected readonly search = signal("");
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly formMode = signal<FormMode | null>(null);
  protected readonly code = signal("");
  protected readonly name = signal("");
  protected readonly description = signal("");
  protected readonly tokenAudience = signal("");
  protected readonly clientType = signal<"Public" | "Confidential" | "Service">(
    "Public",
  );
  protected readonly clientSecret = signal("");
  protected readonly parentModuleId = signal<number | null>(null);
  protected readonly targetModuleId = signal<number | null>(null);
  protected readonly displayOrder = signal(0);

  protected readonly formNavigation = new InlineFormNavigation(
    () => this.formMode() !== null,
    () => this.saving(),
    () =>
      JSON.stringify([
        this.code(),
        this.name(),
        this.description(),
        this.tokenAudience(),
        this.clientType(),
        this.clientSecret(),
        this.parentModuleId(),
        this.targetModuleId(),
        this.displayOrder(),
      ]),
    () =>
      this.confirmations.ask({
        title: "Discard unsaved changes?",
        message: "Your changes have not been saved. Return without saving?",
        confirmText: "Discard changes",
        tone: "danger",
      }),
  );
  canLeaveForm(): Promise<boolean> {
    return this.formNavigation.canLeave();
  }
  ngOnDestroy(): void {
    this.loadGeneration++;
    this.formNavigation.destroy();
    this.clientSecret.set("");
  }

  ngOnInit(): void {
    this.directory.start(() => {
      this.catalog.set(null);
      this.access.set(null);
      this.resetUsers();
      this.search.set(this.directory.query());
      void this.loadApplications();
    });
  }

  private resetUsers(): void {
    this.users.set([]);
    this.usersTotal.set(0);
    this.usersSkip.set(0);
    this.usersSearch.set("");
  }

  protected async loadApplications(preferredId?: number): Promise<void> {
    if (preferredId && preferredId !== this.directory.id()) {
      await this.directory.open(preferredId);
      return;
    }
    const generation = ++this.loadGeneration;
    this.loading.set(true);
    this.error.set(null);
    try {
      const result = await this.service.search(
        this.search(),
        this.directory.skip(),
      );
      if (generation !== this.loadGeneration) return;
      this.applications.set(result.items);
      this.directory.total.set(result.totalCount);
      const selected = this.directory.id();
      if (selected) await this.select(selected);
      else {
        this.catalog.set(null);
        this.access.set(null);
        this.resetUsers();
      }
    } catch {
      if (generation === this.loadGeneration)
        this.error.set("The application catalog could not be loaded.");
    } finally {
      if (generation === this.loadGeneration) this.loading.set(false);
    }
  }

  protected async select(applicationId: number): Promise<void> {
    const generation = this.loadGeneration;
    this.error.set(null);
    try {
      const [catalog, access] = await Promise.all([
        this.service.getCatalog(applicationId),
        this.service.getAccess(applicationId),
      ]);
      if (generation === this.loadGeneration) {
        this.catalog.set(catalog);
        this.access.set(access);
        this.usersSearch.set("");
        void this.loadUsers(applicationId, 0);
      }
    } catch {
      if (generation === this.loadGeneration)
        this.error.set("Application detail could not be loaded.");
    }
  }
  protected readonly directoryColumns: Column[] = [
    {
      dataField: "applicationName",
      caption: "Application",
      cellTemplate: "directoryEntity",
      minWidth: 280,
      allowHiding: false,
    },
    {
      dataField: "clientCount",
      caption: "Clients",
      dataType: "number",
      alignment: "center",
      minWidth: 90,
    },
    {
      dataField: "moduleCount",
      caption: "Modules",
      dataType: "number",
      alignment: "center",
      minWidth: 90,
    },
    {
      dataField: "roleCount",
      caption: "Roles",
      dataType: "number",
      alignment: "center",
      minWidth: 90,
    },
    {
      dataField: "userCount",
      caption: "Users",
      dataType: "number",
      alignment: "center",
      minWidth: 90,
    },
    {
      dataField: "isActive",
      caption: "Status",
      cellTemplate: "directoryStatus",
      minWidth: 110,
    },
  ];
  protected searchDirectory(): void {
    this.directory.search(this.search(), () => void this.loadApplications());
  }
  protected pageDirectory(skip: number): void {
    this.directory.page(
      skip,
      this.directory.query(),
      () => void this.loadApplications(),
    );
  }

  protected openForm(mode: FormMode, moduleId?: number): void {
    if (this.saving()) return;
    this.formMode.set(mode);
    this.code.set("");
    this.name.set("");
    this.description.set("");
    this.tokenAudience.set("");
    this.clientType.set("Public");
    this.clientSecret.set("");
    this.parentModuleId.set(null);
    this.targetModuleId.set(
      moduleId ?? this.catalog()?.modules[0]?.applicationModuleId ?? null,
    );
    this.displayOrder.set(0);
    this.error.set(null);
    this.formNavigation.begin();
  }

  protected async closeForm(): Promise<void> {
    if (!(await this.canLeaveForm())) return;
    this.error.set(null);
    this.formNavigation.finish();
    this.formMode.set(null);
    this.clientSecret.set("");
  }

  protected async submit(): Promise<void> {
    const mode = this.formMode();
    const current = this.catalog();
    if (!mode || this.saving() || (mode !== "application" && !current)) return;
    if (!this.code().trim() || !this.name().trim()) {
      this.error.set("Code and name are required.");
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    try {
      let applicationId = current?.application.applicationId;
      if (mode === "application") {
        if (!this.tokenAudience().trim())
          throw new Error("Token audience is required.");
        const request: CreateApplicationRequest = {
          applicationCode: this.code().trim(),
          applicationName: this.name().trim(),
          description: this.optional(this.description()),
          tokenAudience: this.tokenAudience().trim(),
          accessTokenLifetimeMinutes: 15,
          refreshTokenLifetimeDays: 7,
        };
        applicationId = (await this.service.createApplication(request))
          .resourceId;
      } else if (mode === "client") {
        const type = this.clientType();
        if (type !== "Public" && this.clientSecret().length < 16) {
          throw new Error(
            "Confidential and service clients require a secret of at least 16 characters.",
          );
        }
        const request: CreateApplicationClientRequest = {
          clientId: this.code().trim(),
          clientName: this.name().trim(),
          clientType: type,
          clientSecret: type === "Public" ? null : this.clientSecret(),
          expiresAt: null,
        };
        await this.service.createClient(applicationId!, request);
      } else if (mode === "module") {
        const request: CreateModuleRequest = {
          moduleCode: this.code().trim(),
          moduleName: this.name().trim(),
          description: this.optional(this.description()),
          parentApplicationModuleId: this.parentModuleId(),
          displayOrder: this.displayOrder(),
          isSystem: false,
        };
        await this.service.createModule(applicationId!, request);
      } else {
        const moduleId = this.targetModuleId();
        if (!moduleId) throw new Error("Select a module for the capability.");
        if (!this.code().includes("."))
          throw new Error("Capability code must use module.action format.");
        const request: CreateCapabilityRequest = {
          capabilityCode: this.code().trim(),
          capabilityName: this.name().trim(),
          description: this.optional(this.description()),
        };
        await this.service.createCapability(applicationId!, moduleId, request);
      }
      this.notice.set(`${this.title(mode)} created successfully.`);
      this.formMode.set(null);
      this.clientSecret.set("");
      this.formNavigation.finish();
      this.saving.set(false);
      await this.loadApplications(applicationId);
    } catch (failure) {
      this.clientSecret.set("");
      const problem = failure as {
        error?: { detail?: string; title?: string };
        message?: string;
      };
      this.error.set(
        problem.error?.detail ??
          problem.error?.title ??
          problem.message ??
          "The change could not be saved.",
      );
    } finally {
      this.saving.set(false);
    }
  }

  protected async revoke(
    kind: CatalogResourceKind,
    resourceId: number,
    label: string,
  ): Promise<void> {
    if (
      !(await this.confirmations.ask({
        title: "Revoke access?",
        message: `Revoke ${label}? This action is audited and may remove access.`,
        confirmText: "Revoke",
        tone: "danger",
      }))
    )
      return;
    this.saving.set(true);
    try {
      await this.service.revoke(kind, resourceId);
      this.notice.set(`${label} revoked.`);
      const selectedId =
        kind === "Application"
          ? undefined
          : this.catalog()?.application.applicationId;
      await this.loadApplications(selectedId);
    } catch {
      this.error.set(`${label} could not be revoked.`);
    } finally {
      this.saving.set(false);
    }
  }

  protected title(mode: FormMode): string {
    return {
      application: "Application",
      client: "Client",
      module: "Module",
      capability: "Capability",
    }[mode];
  }

  private optional(value: string): string | null {
    return value.trim() || null;
  }
}
