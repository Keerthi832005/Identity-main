import { ConfirmationService } from "../../shared/ui/confirmation/confirmation.service";
import { Router, RouterOutlet } from "@angular/router";
import type { OrganizationEditorComponent } from "./organization-editor.component";
import { DatePipe } from "@angular/common";
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  computed,
  effect,
  inject,
  signal,
} from "@angular/core";
import { DxButtonModule } from "devextreme-angular/ui/button";
import { DxDataGridModule } from "devextreme-angular/ui/data-grid";
import type { Column } from "devextreme/ui/data_grid";
import { AppDataGridComponent } from "../../shared/ui/design-system/app-data-grid.component";
import { AppBackButtonComponent } from "../../shared/ui/design-system/app-back-button.component";
import { AppGridCellDirective } from "../../shared/ui/design-system/app-grid-cell.directive";
import { AppFormControlsModule } from "../../shared/ui/form-controls/app-form-controls.module";
import { OrganizationService } from "./organization.service";
import { BulkActionsComponent } from "../../shared/bulk-data/bulk-actions.component";
import { AppErrorStateComponent } from "../../shared/ui/design-system/app-error-state.component";
import { AppInlineAlertComponent } from "../../shared/ui/design-system/app-inline-alert.component";
import { AppPageComponent } from "../../shared/ui/design-system/app-page.component";
import { AppSkeletonComponent } from "../../shared/ui/design-system/app-skeleton.component";
import { BulkPasteDirective } from "../../shared/bulk-data/bulk-paste.directive";
import { collectAllPages } from "../../shared/bulk-data/export-paging";
import {
  BulkColumn,
  BulkPasteRow,
} from "../../shared/bulk-data/bulk-data.models";
import {
  organizationError,
  OrganizationPage,
  OrganizationCreated,
  OrganizationRow,
  OrganizationUnit,
  UnitType,
  childTypes,
  unitLabels,
  unitTypes,
} from "./organization.models";

@Component({
  selector: "app-organization-management",
  imports: [
    BulkActionsComponent,
    BulkPasteDirective,
    AppErrorStateComponent,
    AppInlineAlertComponent,
    AppPageComponent,
    AppSkeletonComponent,
    AppDataGridComponent,
    AppBackButtonComponent,
    AppGridCellDirective,
    RouterOutlet,
    DatePipe,
    DxButtonModule,
    AppFormControlsModule,
  ],
  templateUrl: "./organization-management.component.html",
  styleUrl: "./organization-management.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrganizationManagementComponent implements OnInit, OnDestroy {
  protected readonly detailView = signal(false);
  protected readonly unitColumns: Column[] = [
    {
      dataField: "unit.unitName",
      caption: "Name",
      cellTemplate: "unitName",
      minWidth: 240,
      allowHiding: false,
    },
    {
      dataField: "unit.unitCode",
      caption: "Code",
      minWidth: 130,
    },
    {
      dataField: "unit.unitType",
      caption: "Type",
      minWidth: 120,
    },
    {
      dataField: "unit.organizationId",
      caption: "Organization ID",
      minWidth: 130,
    },
    {
      dataField: "unit.isActive",
      caption: "Status",
      cellTemplate: "unitState",
      minWidth: 100,
    },
  ];
  protected backToDirectory(): void {
    this.detailVersion++;
    this.detailView.set(false);
  }
  protected readonly service = inject(OrganizationService);
  private readonly confirmations = inject(ConfirmationService);
  protected readonly types = unitTypes;
  protected readonly labels = unitLabels;
  protected readonly childTypes = childTypes;
  protected readonly type = signal<UnitType | null>("Organization");
  protected readonly organization = signal<{ id: number; name: string } | null>(
    null,
  );
  protected readonly parent = signal<OrganizationUnit | null>(null);
  protected readonly search = signal("");
  protected readonly state = signal("");
  protected readonly page = signal<OrganizationPage>({
    items: [],
    totalCount: 0,
    skip: 0,
    take: 20,
  });
  protected readonly detail = signal<OrganizationUnit | null>(null);
  protected readonly ancestors = signal<readonly OrganizationUnit[]>([]);

  /* Per-type record counts for the chip row. Scoped identically to load()'s organization/parent/
     active-state filters (never unitType or search) so a count only ever describes the current
     scope; a per-type failure just leaves that chip without a number. */
  protected readonly counts = signal<
    Readonly<Partial<Record<UnitType, number>>>
  >({});
  private countsVersion = 0;
  private readonly countsEffect = effect(() => {
    this.refreshCounts();
  });
  private refreshCounts(): void {
    const scope = {
      organizationId: this.organization()?.id,
      parentOrganizationUnitId: this.parent()?.organizationUnitId,
      isActive: this.state() === "" ? undefined : this.state() === "true",
    };
    this.loadCounts(scope);
  }
  private loadCounts(scope: {
    readonly organizationId: number | undefined;
    readonly parentOrganizationUnitId: number | undefined;
    readonly isActive: boolean | undefined;
  }): void {
    const version = ++this.countsVersion;
    void Promise.all(
      unitTypes.map(async (unitType) => {
        try {
          const page = await this.service.search({
            ...scope,
            unitType,
            skip: 0,
            take: 1,
          });
          return [unitType, page.totalCount] as const;
        } catch {
          return [unitType, undefined] as const;
        }
      }),
    ).then((entries) => {
      if (version !== this.countsVersion) return;
      const next: Partial<Record<UnitType, number>> = {};
      for (const [unitType, total] of entries)
        if (total !== undefined) next[unitType] = total;
      this.counts.set(next);
    });
  }

  /* Mirrors OrganizationBulkDescriptor. The eight types and their parent rules are enforced by the
     server against the domain, so this list only drives the paste mapping. */
  protected readonly bulkColumns: readonly BulkColumn[] = [
    {
      columnId: "unitType",
      header: "Unit type",
      type: "Enumeration",
      required: true,
      allowedValues: [...unitTypes],
    },
    { columnId: "unitCode", header: "Unit code", type: "Text", required: true },
    { columnId: "unitName", header: "Unit name", type: "Text", required: true },
    {
      columnId: "parentUnitCode",
      header: "Parent unit code",
      type: "Text",
      required: false,
    },
    {
      columnId: "description",
      header: "Description",
      type: "Text",
      required: false,
    },
    {
      columnId: "addressLine1",
      header: "Address line 1",
      type: "Text",
      required: false,
    },
    { columnId: "city", header: "City", type: "Text", required: false },
    { columnId: "stateName", header: "State", type: "Text", required: false },
    {
      columnId: "postalCode",
      header: "Postal code",
      type: "Text",
      required: false,
    },
    {
      columnId: "countryCode",
      header: "Country code",
      type: "Text",
      required: false,
    },
  ];

  /* Exports every page of the branch on screen, not only the rows currently visible. */
  protected readonly exportUnits = async (): Promise<
    readonly BulkPasteRow[]
  > => {
    const units = await collectAllPages((skip) =>
      this.service.search({
        organizationId: this.organization()?.id,
        unitType: this.type() ?? undefined,
        parentOrganizationUnitId: this.parent()?.organizationUnitId,
        isActive: this.state() === "" ? undefined : this.state() === "true",
        search: this.search(),
        skip,
        take: OrganizationManagementComponent.branchSize,
      }),
    );
    const parents = new Map(
      this.ancestors().map((unit) => [unit.organizationUnitId, unit.unitCode]),
    );
    for (const unit of units) {
      parents.set(unit.organizationUnitId, unit.unitCode);
    }
    return units.map((unit, index) => ({
      sourceRowNumber: index + 3,
      values: {
        unitType: unit.unitType,
        unitCode: unit.unitCode,
        unitName: unit.unitName,
        parentUnitCode: unit.parentOrganizationUnitId
          ? (parents.get(unit.parentOrganizationUnitId) ?? null)
          : null,
        description: unit.description,
        addressLine1: unit.address.addressLine1,
        city: unit.address.city,
        stateName: unit.address.stateName,
        postalCode: unit.address.postalCode,
        countryCode: unit.address.countryCode,
      },
    }));
  };
  /* The drill-down is lazy: a branch is fetched the first time it is opened and kept, because
     browsing back up a tree revisits the same nodes constantly. */
  private readonly branches = signal<
    Readonly<
      Record<number, { items: readonly OrganizationUnit[]; total: number }>
    >
  >({});
  private readonly openIds = signal<readonly number[]>([]);
  private readonly loadingIds = signal<readonly number[]>([]);
  /* The server refuses a page larger than 50 for this query. */
  private static readonly branchSize = 50;

  protected readonly rows = computed<readonly OrganizationRow[]>(() => {
    const branches = this.branches();
    const open = new Set(this.openIds());
    const busy = new Set(this.loadingIds());
    const rows: OrganizationRow[] = [];
    const walk = (units: readonly OrganizationUnit[], depth: number): void => {
      for (const unit of units) {
        const id = unit.organizationUnitId;
        const branch = branches[id];
        const isOpen = open.has(id);
        rows.push({
          unit,
          depth,
          expandable: childTypes[unit.unitType].length > 0,
          expanded: isOpen,
          loading: busy.has(id),
          hiddenChildren: branch ? branch.total - branch.items.length : 0,
        });
        if (isOpen && branch) walk(branch.items, depth + 1);
      }
    };
    walk(this.page().items, 0);
    return rows;
  });

  protected async toggle(unit: OrganizationUnit): Promise<void> {
    const id = unit.organizationUnitId;
    if (this.openIds().includes(id)) {
      this.openIds.update((ids) => ids.filter((open) => open !== id));
      return;
    }
    this.openIds.update((ids) => [...ids, id]);
    if (this.branches()[id]) return;
    this.loadingIds.update((ids) => [...ids, id]);
    try {
      const result = await this.service.search({
        organizationId: unit.organizationId,
        parentOrganizationUnitId: id,
        skip: 0,
        take: OrganizationManagementComponent.branchSize,
      });
      this.branches.update((branches) => ({
        ...branches,
        [id]: { items: result.items, total: result.totalCount },
      }));
    } catch {
      this.openIds.update((ids) => ids.filter((open) => open !== id));
      this.error.set(`Children of ${unit.unitName} could not be loaded.`);
    } finally {
      this.loadingIds.update((ids) => ids.filter((open) => open !== id));
    }
  }

  /** Every unit the screen has seen, so a path can be named without refetching it. */
  private known(): ReadonlyMap<number, OrganizationUnit> {
    const units = new Map<number, OrganizationUnit>();
    for (const unit of [
      ...this.ancestors(),
      ...this.page().items,
      ...Object.values(this.branches()).flatMap((branch) => branch.items),
    ]) {
      units.set(unit.organizationUnitId, unit);
    }
    const detail = this.detail();
    if (detail) units.set(detail.organizationUnitId, detail);
    return units;
  }

  private pathTo(unit: OrganizationUnit): readonly OrganizationUnit[] {
    const units = this.known();
    const path = unit.hierarchyPath
      .split("/")
      .filter(Boolean)
      .map(Number)
      .filter((id) => id !== unit.organizationUnitId)
      .map((id) => units.get(id))
      /* A path segment the screen has never loaded is skipped rather than shown as an id: the
         trail is for stepping back, and a step it cannot take is worse than a shorter trail. */
      .filter(
        (ancestor): ancestor is OrganizationUnit => ancestor !== undefined,
      );
    return [...path, unit];
  }

  protected readonly trail = signal<readonly OrganizationUnit[]>([]);

  protected readonly loading = signal(false);
  protected readonly detailLoading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly detailError = signal<string | null>(null);
  protected readonly title = computed(() =>
    this.type() ? unitLabels[this.type()!] : "Child units",
  );
  protected readonly pageNumber = computed(
    () => Math.floor(this.page().skip / this.page().take) + 1,
  );
  protected readonly pageCount = computed(() =>
    Math.max(1, Math.ceil(this.page().totalCount / this.page().take)),
  );
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly router = inject(Router);
  protected readonly editorActive = signal(false);
  protected readonly saving = signal(false);
  protected readonly notice = signal<string | null>(null);
  private returnFocus: HTMLElement | null = null;
  protected readonly conflict = signal<{
    readonly unit: OrganizationUnit;
    readonly changes: readonly string[];
  } | null>(null);
  private listVersion = 0;
  private detailVersion = 0;
  private selected: Pick<
    OrganizationUnit,
    "organizationId" | "organizationUnitId"
  > | null = null;
  ngOnInit(): void {
    void this.load();
  }
  ngOnDestroy(): void {
    this.listVersion++;
    this.detailVersion++;
  }
  protected async load(skip = 0): Promise<void> {
    this.detailView.set(false);
    const version = ++this.listVersion;
    this.detailVersion++;
    this.loading.set(true);
    this.error.set(null);
    this.detail.set(null);
    this.detailError.set(null);
    this.detailLoading.set(false);
    this.ancestors.set([]);
    this.selected = null;
    this.openIds.set([]);
    this.branches.set({});
    try {
      const result = await this.service.search({
        organizationId: this.organization()?.id,
        unitType: this.type() ?? undefined,
        parentOrganizationUnitId: this.parent()?.organizationUnitId,
        isActive: this.state() === "" ? undefined : this.state() === "true",
        search: this.search(),
        skip,
        take: 20,
      });
      if (version !== this.listVersion) return;
      this.page.set(result);
    } catch {
      if (version === this.listVersion) {
        this.error.set("Units could not be loaded. Retry the search.");
        this.page.set({ items: [], totalCount: 0, skip: 0, take: 20 });
      }
    } finally {
      if (version === this.listVersion) this.loading.set(false);
    }
  }
  protected chooseType(type: UnitType): void {
    this.type.set(type);
    this.parent.set(null);
    this.trail.set([]);
    this.search.set("");
    void this.load();
  }
  protected reset(): void {
    this.organization.set(null);
    this.parent.set(null);
    this.trail.set([]);
    this.type.set("Organization");
    this.search.set("");
    this.state.set("");
    void this.load();
  }
  protected async select(
    unit: Pick<OrganizationUnit, "organizationId" | "organizationUnitId">,
  ): Promise<void> {
    this.detailView.set(true);
    if (this.conflict()) {
      this.conflict.set(null);
      this.error.set(null);
    }
    const version = ++this.detailVersion;
    this.selected = unit;
    this.detail.set(null);
    this.ancestors.set([]);
    this.detailError.set(null);
    this.detailLoading.set(true);
    try {
      const detail = await this.service.get(
        unit.organizationId,
        unit.organizationUnitId,
      );
      const ids = detail.hierarchyPath
        .split("/")
        .filter(Boolean)
        .map(Number)
        .filter((id) => id !== detail.organizationUnitId);
      const ancestors = await Promise.all(
        ids.map((id) => this.service.get(unit.organizationId, id)),
      );
      if (version !== this.detailVersion) return;
      this.detail.set(detail);
      this.ancestors.set(ancestors);
    } catch {
      if (version === this.detailVersion)
        this.detailError.set(
          "Unit details could not be loaded. Retry or select another unit.",
        );
    } finally {
      if (version === this.detailVersion) this.detailLoading.set(false);
    }
  }
  protected retryDetail(): void {
    if (this.selected) void this.select(this.selected);
  }
  protected browse(unit: OrganizationUnit): void {
    const root =
      unit.unitType === "Organization"
        ? unit
        : this.ancestors().find((item) => item.unitType === "Organization");
    this.organization.set({
      id: unit.organizationId,
      name: root?.unitName ?? `Organization ${unit.organizationId}`,
    });
    const path = this.pathTo(unit);
    this.parent.set(unit);
    this.type.set(null);
    this.search.set("");
    this.state.set("");
    void this.load();
    /* Set after load(), which clears the browsing state it does not own. */
    this.trail.set(path);
  }
  protected scope(unit: OrganizationUnit): void {
    const root =
      unit.unitType === "Organization"
        ? unit
        : this.ancestors().find((item) => item.unitType === "Organization");
    this.organization.set({
      id: unit.organizationId,
      name: root?.unitName ?? `Organization ${unit.organizationId}`,
    });
    this.parent.set(null);
    this.trail.set([]);
    void this.load();
  }
  protected addressLines(unit: OrganizationUnit): readonly string[] {
    return [
      unit.address.addressLine1,
      unit.address.addressLine2,
      unit.address.addressLine3,
      unit.address.city,
      unit.address.district,
      unit.address.stateName,
      unit.address.postalCode,
      unit.address.countryCode,
    ].filter((value): value is string => Boolean(value));
  }
  protected openCreate(
    type: UnitType = "Organization",
    parent: OrganizationUnit | null = null,
  ): void {
    if (this.saving()) return;
    if (
      type !== "Organization" &&
      (!parent?.isActive || !childTypes[parent.unitType].includes(type))
    )
      return;
    void this.router.navigate(
      type === "Organization"
        ? ["/organizations/new"]
        : [
            "/organizations",
            parent!.organizationId,
            "units",
            parent!.organizationUnitId,
            "new",
            type,
          ],
    );
  }
  protected openEdit(): void {
    const unit = this.detail();
    if (!unit || this.saving()) return;
    void this.router.navigate([
      "/organizations",
      unit.organizationId,
      "units",
      unit.organizationUnitId,
      "edit",
    ]);
  }
  protected activateEditor(editor: OrganizationEditorComponent): void {
    this.returnFocus =
      document.activeElement instanceof HTMLElement
        ? document.activeElement
        : null;
    this.editorActive.set(true);
    editor.saved.subscribe((unit) => {
      this.notice.set(`${unit.unitType} saved.`);
      void this.refreshSaved(unit);
    });
  }
  protected deactivateEditor(): void {
    this.editorActive.set(false);
    setTimeout(() => {
      if (this.returnFocus?.isConnected) this.returnFocus.focus();
      else
        this.host.nativeElement
          .querySelector<HTMLElement>('[role="button"]')
          ?.focus();
    });
  }
  private async refreshSaved(unit: OrganizationCreated): Promise<void> {
    await this.load(this.page().skip);
    this.refreshCounts();
    await this.select(unit);
  }
  protected async toggleActive(): Promise<void> {
    const target = this.detail();
    if (
      !target ||
      this.saving() ||
      !(await this.confirmations.ask({
        title: "Change unit status?",
        message: `${target.isActive ? "Deactivate" : "Activate"} ${target.unitName}? This changes only this unit; descendants and permissions stay unchanged.`,
        confirmText: "Confirm change",
        tone: "danger",
      }))
    )
      return;
    this.saving.set(true);
    this.error.set(null);
    this.conflict.set(null);
    try {
      await this.service.setActive(target, !target.isActive);
      this.notice.set("Active state saved. Descendants are unchanged.");
      await this.load(this.page().skip);
      this.refreshCounts();
      await this.select(target);
    } catch (failure) {
      if (
        (failure as { error?: { code?: string } }).error?.code ===
        "organization_concurrency_conflict"
      ) {
        await this.describeConflict(target);
      } else {
        this.error.set(organizationError(failure));
      }
    } finally {
      this.saving.set(false);
    }
  }

  /* A conflict is not an error the administrator caused, so the screen names what the other person
     changed instead of repeating the server's message. */
  private async describeConflict(mine: OrganizationUnit): Promise<void> {
    let theirs: OrganizationUnit;
    try {
      theirs = await this.service.get(
        mine.organizationId,
        mine.organizationUnitId,
      );
    } catch {
      this.conflict.set({ unit: mine, changes: [] });
      return;
    }
    const changes: string[] = [];
    if (theirs.isActive !== mine.isActive) {
      changes.push(`It is now ${theirs.isActive ? "active" : "inactive"}.`);
    }
    if (theirs.unitName !== mine.unitName) {
      changes.push(`The name is now "${theirs.unitName}".`);
    }
    if (theirs.unitCode !== mine.unitCode) {
      changes.push(`The code is now "${theirs.unitCode}".`);
    }
    if ((theirs.description ?? "") !== (mine.description ?? "")) {
      changes.push("The description changed.");
    }
    if (JSON.stringify(theirs.address) !== JSON.stringify(mine.address)) {
      changes.push("The address changed.");
    }
    this.conflict.set({ unit: theirs, changes });
  }

  protected conflictMessage(): string {
    const conflict = this.conflict();
    if (!conflict) return "";
    if (!conflict.changes.length) {
      /* Either the fetch failed or the visible fields match: the row version still moved, so the
         write was correctly refused. */
      return `${conflict.unit.unitName} was saved by someone else while this screen was open. Nothing on this screen was changed.`;
    }
    return `${conflict.unit.unitName} was saved by someone else. ${conflict.changes.join(" ")} Your change was not applied.`;
  }

  protected async reloadState(): Promise<void> {
    const conflict = this.conflict();
    if (conflict) {
      await this.select(conflict.unit);
      this.conflict.set(null);
      this.error.set(null);
    }
  }
  protected hasStateConflict(): boolean {
    return this.conflict() !== null;
  }
}
