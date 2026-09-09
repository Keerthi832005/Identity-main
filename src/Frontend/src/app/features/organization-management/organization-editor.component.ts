import { ConfirmationService } from "../../shared/ui/confirmation/confirmation.service";
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  OnDestroy,
  OnInit,
  computed,
  inject,
  output,
  signal,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute, ParamMap, Router } from "@angular/router";
import { DxButtonModule } from "devextreme-angular/ui/button";
import { AppBackButtonComponent } from "../../shared/ui/design-system/app-back-button.component";
import { AppPageComponent } from "../../shared/ui/design-system/app-page.component";
import { AppFormControlsModule } from "../../shared/ui/form-controls/app-form-controls.module";
import { OrganizationService } from "./organization.service";
import {
  OrganizationDraft,
  OrganizationCreated,
  OrganizationUnit,
  UnitType,
  addressFields,
  childTypes,
  organizationDraft,
  organizationError,
  organizationFields,
  unitTypes,
} from "./organization.models";

type Editor =
  | { kind: "create"; type: UnitType; parent: OrganizationUnit | null }
  | { kind: "edit"; target: OrganizationUnit; type: UnitType };

@Component({
  selector: "app-organization-editor",
  imports: [
    AppBackButtonComponent,
    AppPageComponent,
    DxButtonModule,
    AppFormControlsModule,
  ],
  templateUrl: "./organization-editor.component.html",
  styleUrl: "./organization-editor.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { "(window:beforeunload)": "beforeUnload($event)" },
})
export class OrganizationEditorComponent implements OnInit, OnDestroy {
  private readonly service = inject(OrganizationService);
  private readonly confirmations = inject(ConfirmationService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  readonly saved = output<OrganizationCreated>();
  protected readonly editor = signal<Editor | null>(null);
  protected readonly draft = signal<OrganizationDraft>(organizationDraft());
  private readonly initialDraft =
    signal<OrganizationDraft>(organizationDraft());
  protected readonly addressFields = addressFields;
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);
  protected readonly conflict = signal(false);
  protected readonly completed = signal(false);
  protected readonly dirty = computed(
    () => JSON.stringify(this.draft()) !== JSON.stringify(this.initialDraft()),
  );
  protected readonly title = computed(() => {
    const editor = this.editor();
    return editor
      ? `${editor.kind === "edit" ? "Edit" : "New"} ${editor.type}`
      : "Organization form";
  });
  private loadVersion = 0;

  ngOnInit(): void {
    this.route.paramMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((params) => void this.loadEditor(params));
  }
  ngOnDestroy(): void {
    this.loadVersion++;
  }

  protected async loadEditor(params: ParamMap): Promise<void> {
    const version = ++this.loadVersion;
    this.loading.set(true);
    this.loadError.set(null);
    this.editor.set(null);
    this.completed.set(false);
    this.setDraft(organizationDraft());
    try {
      const editing = this.route.snapshot.data["mode"] === "edit";
      if (!editing && !params.has("organizationId") && !params.has("unitId")) {
        this.openCreate();
        return;
      }
      const organizationId = Number(params.get("organizationId"));
      const unitId = Number(params.get("unitId"));
      if (
        !Number.isSafeInteger(organizationId) ||
        organizationId <= 0 ||
        !Number.isSafeInteger(unitId) ||
        unitId <= 0
      )
        throw new Error(
          "The organization form address is invalid. Return to the list and select a unit.",
        );
      const type = params.get("unitType") as UnitType;
      if (!editing && (!unitTypes.includes(type) || type === "Organization"))
        throw new Error(
          "This child unit type is invalid. Return to the list and select an allowed type.",
        );
      const unit = await this.service.get(organizationId, unitId);
      if (version !== this.loadVersion) return;
      if (
        unit.organizationId !== organizationId ||
        unit.organizationUnitId !== unitId
      )
        throw new Error(
          "The unit does not belong to the requested organization.",
        );
      if (editing) this.openEdit(unit);
      else {
        if (!unit.isActive || !childTypes[unit.unitType].includes(type))
          throw new Error(
            "The parent must be active and allow this child type. Return to the list and choose a valid parent.",
          );
        this.openCreate(type, unit);
      }
    } catch (error) {
      if (version === this.loadVersion)
        this.loadError.set(
          error instanceof Error
            ? error.message
            : "The organization form could not be loaded. Retry or return to the list.",
        );
    } finally {
      if (version === this.loadVersion) this.loading.set(false);
    }
  }
  protected retry(): void {
    void this.loadEditor(this.route.snapshot.paramMap);
  }
  protected openCreate(
    type: UnitType = "Organization",
    parent: OrganizationUnit | null = null,
  ): void {
    if (
      type !== "Organization" &&
      (!parent?.isActive || !childTypes[parent.unitType].includes(type))
    )
      return;
    this.editor.set({ kind: "create", type, parent });
    this.setDraft(organizationDraft());
    this.prepareEditor();
  }
  protected openEdit(target: OrganizationUnit): void {
    this.editor.set({ kind: "edit", target, type: target.unitType });
    this.setDraft(organizationDraft(target));
    this.prepareEditor();
  }
  private setDraft(draft: OrganizationDraft): void {
    this.draft.set(draft);
    this.initialDraft.set(draft);
  }
  private prepareEditor(): void {
    this.saveError.set(null);
    this.conflict.set(false);
    setTimeout(() =>
      this.host.nativeElement
        .querySelector<HTMLInputElement>(".editor input")
        ?.focus(),
    );
  }
  protected back(): void {
    if (!this.saving()) void this.router.navigate(["/organizations"]);
  }
  async canLeave(): Promise<boolean> {
    if (this.saving()) return false;
    if (this.completed() || !this.dirty()) return true;
    const draft = this.draft();
    const approved = await this.confirmations.ask({
      title: "Discard organization changes?",
      message:
        "Your organization changes have not been saved. Leave this page without saving?",
      confirmText: "Discard changes",
      tone: "danger",
    });
    return approved && !this.saving() && this.draft() === draft;
  }
  protected beforeUnload(event: BeforeUnloadEvent): void {
    if (!this.completed() && (this.saving() || this.dirty())) {
      event.preventDefault();
      event.returnValue = "";
    }
  }
  protected editField(
    key: "unitCode" | "unitName" | "description" | "latitude" | "longitude",
    value: string,
  ): void {
    this.draft.update((draft) => ({ ...draft, [key]: value }));
  }
  protected editAddress(
    key: (typeof addressFields)[number]["key"],
    value: string,
  ): void {
    this.draft.update((draft) => ({
      ...draft,
      address: { ...draft.address, [key]: value },
    }));
  }

  protected async save(): Promise<void> {
    const editor = this.editor();
    if (!editor || this.saving() || this.conflict() || this.completed()) return;
    this.saveError.set(null);
    this.saving.set(true);
    try {
      const fields = organizationFields(this.draft());
      const saved =
        editor.kind === "edit"
          ? await this.service.update(editor.target, {
              ...fields,
              rowVersion: editor.target.rowVersion,
            })
          : editor.type === "Organization"
            ? await this.service.create(fields)
            : await this.service.createChild(editor.parent!.organizationId, {
                ...fields,
                unitType: editor.type,
                parentOrganizationUnitId: editor.parent!.organizationUnitId,
              });
      this.completed.set(true);
      this.saved.emit(saved);
    } catch (failure) {
      this.saveError.set(organizationError(failure));
      this.conflict.set(
        (failure as { error?: { code?: string } }).error?.code ===
          "organization_concurrency_conflict",
      );
      setTimeout(() =>
        this.host.nativeElement
          .querySelector<HTMLElement>("[role=alert]")
          ?.focus(),
      );
    } finally {
      this.saving.set(false);
    }
    // Navigation failure after a committed write must never invite a duplicate create.
    if (this.completed()) {
      try {
        await this.router.navigate(["/organizations"], { replaceUrl: true });
      } catch {
        this.saveError.set(
          "Saved successfully. Use Back to organizations to return to the list.",
        );
      }
    }
  }
  protected async reloadEditor(): Promise<void> {
    const editor = this.editor();
    if (
      editor?.kind !== "edit" ||
      this.saving() ||
      !(await this.confirmations.ask({
        title: "Reload saved details?",
        message: "Discard this draft and load the latest saved details?",
        confirmText: "Discard and reload",
        tone: "danger",
      }))
    )
      return;
    this.saving.set(true);
    try {
      const current = await this.service.get(
        editor.target.organizationId,
        editor.target.organizationUnitId,
      );
      this.editor.set({
        kind: "edit",
        target: current,
        type: current.unitType,
      });
      this.setDraft(organizationDraft(current));
      this.conflict.set(false);
      this.saveError.set(null);
    } catch (failure) {
      this.saveError.set(organizationError(failure));
    } finally {
      this.saving.set(false);
    }
  }
}
