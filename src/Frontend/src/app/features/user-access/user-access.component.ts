import { DatePipe } from "@angular/common";
import type { Column } from "devextreme/ui/data_grid";
import { SecurityControlsService } from "../security-controls/security-controls.service";
import {
  OperationResponse,
  TotpEnrollmentResponse,
  UserSecurityCatalog,
} from "../security-controls/security-controls.models";
import {
  AppChipSelectComponent,
  ChipOption,
} from "../../shared/ui/design-system/app-chip-select.component";
import { AppBackButtonComponent } from "../../shared/ui/design-system/app-back-button.component";
import { AppDirectoryGridComponent } from "../../shared/ui/design-system/app-directory-grid.component";
import { DirectoryRoute } from "../../shared/ui/design-system/directory-route";
import { ConfirmationService } from "../../shared/ui/confirmation/confirmation.service";
import { InlineFormNavigation } from "../../shared/ui/inline-form-navigation";
import { PageHeaderComponent } from "../../shared/page-header/page-header.component";
import { AppEmptyStateComponent } from "../../shared/ui/design-system/app-empty-state.component";
import { AppInlineAlertComponent } from "../../shared/ui/design-system/app-inline-alert.component";
import { AppPageComponent } from "../../shared/ui/design-system/app-page.component";
import { AppSkeletonComponent } from "../../shared/ui/design-system/app-skeleton.component";
import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from "@angular/core";
import { DxButtonModule } from "devextreme-angular/ui/button";
import {
  ApplicationAccessCatalog,
  ApplicationSummary,
  CreateRoleRequest,
  CreateUserRequest,
  PermissionOverrideRequest,
  UserAccessCatalog,
  UserSummary,
  UpdateUserProfileRequest,
} from "./user-access.models";
import { UserAccessService } from "./user-access.service";
import { UserOrganizationMappingComponent } from "./user-organization-mapping.component";
import { UserOrganizationMapping } from "./user-access.models";
import {
  AppLookupComponent,
  LookupSearch,
} from "../../shared/ui/form-controls/app-lookup.component";
import { AppFormControlsModule } from "../../shared/ui/form-controls/app-form-controls.module";
import { BulkActionsComponent } from "../../shared/bulk-data/bulk-actions.component";
import { BulkPasteDirective } from "../../shared/bulk-data/bulk-paste.directive";
import { collectAllPages } from "../../shared/bulk-data/export-paging";
import {
  BulkColumn,
  BulkPasteRow,
} from "../../shared/bulk-data/bulk-data.models";

/** The detail column's sections, split into tabs so it stops being one long scroll. */
type DetailTab = "profile" | "access" | "roles" | "security";

type ActionMode =
  | "user"
  | "profile"
  | "grantApplication"
  | "role"
  | "assignRole"
  | "rolePermission"
  | "override";

@Component({
  selector: "app-user-access",
  imports: [
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
    AppChipSelectComponent,
    DatePipe,
    AppDirectoryGridComponent,
    UserOrganizationMappingComponent,
    AppLookupComponent,
  ],
  templateUrl: "./user-access.component.html",
  styleUrl: "./user-access.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { "(window:beforeunload)": "formNavigation.beforeUnload($event)" },
})
export class UserAccessComponent implements OnInit, OnDestroy {
  protected readonly directory = new DirectoryRoute("/users", "userId");
  private loadGeneration = 0;
  private readonly service = inject(UserAccessService);
  private readonly confirmations = inject(ConfirmationService);
  protected readonly users = signal<readonly UserSummary[]>([]);
  protected readonly detailTab = signal<DetailTab>("profile");
  protected readonly detailTabs: readonly { id: DetailTab; label: string }[] = [
    { id: "profile", label: "Profile" },
    { id: "access", label: "Access" },
    { id: "roles", label: "Roles" },
    { id: "security", label: "Security" },
  ];
  private readonly securityService = inject(SecurityControlsService);
  protected readonly security = signal<UserSecurityCatalog | null>(null);
  protected readonly securityLoading = signal(false);
  /* Guards against a slower earlier request landing after a newer selection. */
  private securityRequest = 0;

  /* Loaded only when the tab is opened: a user's devices, MFA and credentials are a second call,
     and most visits to this screen never look at them. */
  protected async openSecurityTab(userId: number): Promise<void> {
    const request = ++this.securityRequest;
    this.security.set(null);
    this.securityLoading.set(true);
    try {
      const posture = await this.securityService.getSecurity(userId);
      if (request === this.securityRequest) this.security.set(posture);
    } catch {
      if (request === this.securityRequest) this.security.set(null);
    } finally {
      if (request === this.securityRequest) this.securityLoading.set(false);
    }
  }

  protected chooseTab(tab: DetailTab, userId: number): void {
    this.detailTab.set(tab);
    if (tab === "security") void this.openSecurityTab(userId);
  }

  protected readonly password = signal("");
  protected readonly passwordExpiry = signal("");
  protected readonly pin = signal("");
  protected readonly credentialSaving = signal(false);
  protected readonly credentialNotice = signal<string | null>(null);
  protected readonly credentialError = signal<string | null>(null);

  protected async setPassword(userId: number): Promise<void> {
    await this.runCredential(
      () =>
        this.securityService.setPassword(userId, {
          password: this.password(),
          expiresAt: this.passwordExpiry() || null,
        }),
      "Password replaced.",
      userId,
    );
  }

  protected async setPin(userId: number): Promise<void> {
    await this.runCredential(
      () => this.securityService.setPin(userId, { pin: this.pin() }),
      "Terminal PIN replaced.",
      userId,
    );
  }

  /* The typed secret is dropped as soon as the request settles, either way: it is another person's
     credential and has no reason to stay in the page. */
  /* Some of these endpoints answer with {succeeded, failureCode} and some resolve on success and
     throw otherwise, so a returned failure flag is honoured when present and a resolution is
     treated as success when it is not. */
  private async runCredential(
    action: () => Promise<unknown>,
    success: string,
    userId: number,
  ): Promise<void> {
    this.credentialSaving.set(true);
    this.credentialNotice.set(null);
    this.credentialError.set(null);
    try {
      const result = (await action()) as Partial<OperationResponse> | undefined;
      if (result && result.succeeded === false) {
        this.credentialError.set(
          result.failureCode ?? "The change was rejected.",
        );
      } else {
        this.credentialNotice.set(success);
        await this.openSecurityTab(userId);
      }
    } catch {
      this.credentialError.set("The change could not be saved.");
    } finally {
      this.clearSecrets();
      this.credentialSaving.set(false);
    }
  }

  /* The catalog already names every application this user can be granted, so the chips are that
     list rather than a second query. */
  protected readonly applicationChips = computed<ChipOption[]>(() =>
    this.applications().map((application) => ({
      value: application.applicationId.toString(),
      label: application.applicationName,
    })),
  );

  protected readonly deviceName = signal("");
  protected readonly deviceType = signal("Browser");
  protected readonly deviceFingerprint = signal("");
  protected readonly methodName = signal("Authenticator");
  protected readonly verificationCode = signal("");
  protected readonly enrollment = signal<TotpEnrollmentResponse | null>(null);
  private enrollmentTimer: ReturnType<typeof setTimeout> | null = null;

  protected async registerDevice(userId: number): Promise<void> {
    if (!this.deviceName().trim() || !this.deviceFingerprint().trim()) {
      this.credentialError.set("Device name and fingerprint are required.");
      return;
    }
    await this.runCredential(
      () =>
        this.securityService.registerDevice(userId, {
          deviceName: this.deviceName().trim(),
          deviceType: this.deviceType(),
          deviceFingerprint: this.deviceFingerprint(),
        }),
      "Device registered.",
      userId,
    );
  }

  protected async trustDevice(deviceId: number): Promise<void> {
    const userId = this.security()?.user.userId;
    if (
      userId === undefined ||
      !(await this.confirmations.ask({
        title: "Trust device?",
        message: "Trust this device and allow its approved MFA behavior?",
        confirmText: "Trust device",
        tone: "default",
      }))
    )
      return;
    await this.runCredential(
      () => this.securityService.trustDevice(deviceId),
      "Device trusted.",
      userId,
    );
  }

  protected async revokeDevice(deviceId: number): Promise<void> {
    const userId = this.security()?.user.userId;
    if (
      userId === undefined ||
      !(await this.confirmations.ask({
        title: "Revoke device?",
        message: "Revoke this device immediately?",
        confirmText: "Revoke device",
        tone: "danger",
      }))
    )
      return;
    await this.runCredential(
      () => this.securityService.revokeDevice(deviceId),
      "Device revoked.",
      userId,
    );
  }

  protected async revokeMfa(methodId: number): Promise<void> {
    const userId = this.security()?.user.userId;
    if (
      userId === undefined ||
      !(await this.confirmations.ask({
        title: "Revoke MFA method?",
        message: "Revoke this MFA method immediately?",
        confirmText: "Revoke method",
        tone: "danger",
      }))
    )
      return;
    await this.runCredential(
      () => this.securityService.revokeMfa(methodId),
      "MFA method revoked.",
      userId,
    );
  }

  /* The secret is displayed once and never retrievable, so it is also dropped on a timer rather
     than left on screen for whoever walks past next. */
  protected async enrollTotp(userId: number): Promise<void> {
    if (!this.methodName().trim()) return;
    this.credentialSaving.set(true);
    this.credentialError.set(null);
    this.clearEnrollment();
    try {
      const enrollment = await this.securityService.enrollTotp(userId, {
        methodName: this.methodName().trim(),
        isPrimary: true,
      });
      this.enrollment.set(enrollment);
      this.enrollmentTimer = setTimeout(() => this.clearEnrollment(), 120_000);
      await this.openSecurityTab(userId);
    } catch {
      this.clearSecrets();
      this.credentialError.set(
        "TOTP enrolment failed; sensitive values were cleared.",
      );
    } finally {
      this.credentialSaving.set(false);
    }
  }

  protected async verifyTotp(userId: number): Promise<void> {
    const enrollment = this.enrollment();
    if (!enrollment || !/^\d{6}$/.test(this.verificationCode())) {
      this.credentialError.set("Enter the current six-digit code.");
      return;
    }
    await this.runCredential(
      async () => {
        const result = await this.securityService.verifyTotp(
          enrollment.userMfaMethodId,
          { code: this.verificationCode() },
        );
        return result;
      },
      "Authenticator verified.",
      userId,
    );
  }

  private clearEnrollment(): void {
    if (this.enrollmentTimer) clearTimeout(this.enrollmentTimer);
    this.enrollmentTimer = null;
    this.enrollment.set(null);
    this.verificationCode.set("");
  }

  protected clearSecrets(): void {
    this.password.set("");
    this.passwordExpiry.set("");
    this.pin.set("");
    this.deviceFingerprint.set("");
    this.clearEnrollment();
  }

  /* Mirrors UsersBulkDescriptor on the server. The server remains the authority: these columns only
     drive paste mapping and the export payload. */
  protected readonly bulkColumns: readonly BulkColumn[] = [
    {
      columnId: "employeeCode",
      header: "Employee code",
      type: "Text",
      required: true,
    },
    {
      columnId: "displayName",
      header: "Display name",
      type: "Text",
      required: true,
    },
    {
      columnId: "email",
      header: "Contact email",
      type: "Text",
      required: false,
    },
    {
      columnId: "managerEmployeeCode",
      header: "Manager employee code",
      type: "Text",
      required: false,
    },
  ];

  /* Export keeps the active filter but walks every page, not just the rows on screen. */
  protected readonly exportRows = async (): Promise<
    readonly BulkPasteRow[]
  > => {
    const users = await collectAllPages((skip) =>
      this.service.searchUsers(this.search(), skip),
    );
    return users.map((user, index) => ({
      sourceRowNumber: index + 3,
      values: {
        employeeCode: user.employeeCode,
        displayName: user.displayName,
        email: user.email,
        managerEmployeeCode: null,
      },
    }));
  };
  protected readonly applications = signal<readonly ApplicationSummary[]>([]);
  protected readonly userAccess = signal<UserAccessCatalog | null>(null);
  protected readonly applicationAccess =
    signal<ApplicationAccessCatalog | null>(null);
  protected readonly search = signal("");
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly actionMode = signal<ActionMode | null>(null);
  protected readonly selectedApplicationId = signal<number | null>(null);
  protected readonly selectedRoleId = signal<number | null>(null);
  protected readonly selectedCapabilityId = signal<number | null>(null);
  protected readonly code = signal("");
  protected readonly name = signal("");
  protected readonly email = signal("");
  protected readonly profileUser = signal<UserSummary | null>(null);
  protected readonly organizationMapping = signal<UserOrganizationMapping>({
    departmentId: null,
    teamId: null,
    branchId: null,
  });
  protected readonly managerUserId = signal<number | null>(null);
  protected readonly searchManagers: LookupSearch = async (search) => {
    const editedUserId = this.profileUser()?.userId;
    const result = await this.service.searchUsers(search);
    return result.items
      .filter((user) => user.isActive && user.userId !== editedUserId)
      .map((user) => ({
        value: user.userId,
        label: `${user.displayName} (${user.employeeCode})`,
      }));
  };
  protected readonly description = signal("");
  protected readonly effect = signal<"Allow" | "Deny">("Allow");
  protected readonly reason = signal("");
  protected readonly expiresAt = signal("");
  protected readonly formNavigation = new InlineFormNavigation(
    () => this.actionMode() !== null,
    () => this.saving(),
    () =>
      JSON.stringify([
        this.code(),
        this.name(),
        this.email(),
        this.managerUserId(),
        this.organizationMapping(),
        this.description(),
        this.effect(),
        this.reason(),
        this.expiresAt(),
        this.selectedApplicationId(),
        this.selectedRoleId(),
        this.selectedCapabilityId(),
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
  }

  ngOnInit(): void {
    this.directory.start(() => {
      this.userAccess.set(null);
      this.applicationAccess.set(null);
      this.search.set(this.directory.query());
      void this.load();
    });
  }
  protected async load(preferredUserId?: number): Promise<void> {
    if (preferredUserId && preferredUserId !== this.directory.id()) {
      await this.directory.open(preferredUserId);
      return;
    }
    const generation = ++this.loadGeneration;
    this.loading.set(true);
    this.error.set(null);
    try {
      const [users, applications] = await Promise.all([
        this.service.searchUsers(this.search(), this.directory.skip()),
        this.service.searchApplications(),
      ]);
      if (generation !== this.loadGeneration) return;
      this.users.set(users.items);
      this.directory.total.set(users.totalCount);
      this.applications.set(applications.items);
      const selected = this.directory.id();
      if (selected) await this.selectUser(selected);
      else this.userAccess.set(null);
    } catch {
      if (generation === this.loadGeneration)
        this.error.set("User access data could not be loaded.");
    } finally {
      if (generation === this.loadGeneration) this.loading.set(false);
    }
  }
  protected async selectUser(userId: number): Promise<void> {
    const generation = this.loadGeneration;
    try {
      const detail = await this.service.getUserAccess(userId);
      if (generation !== this.loadGeneration) return;
      this.userAccess.set(detail);
      const appId =
        detail.applications.find((x) => x.isActive)?.applicationId ??
        this.applications()[0]?.applicationId ??
        null;
      this.selectedApplicationId.set(appId);
      if (appId) await this.loadApplicationAccess(appId);
      else this.applicationAccess.set(null);
    } catch {
      if (generation === this.loadGeneration)
        this.error.set("User access detail could not be loaded.");
    }
  }
  protected readonly directoryColumns: Column[] = [
    {
      dataField: "displayName",
      caption: "User",
      cellTemplate: "directoryName",
      minWidth: 200,
      allowHiding: false,
    },
    { dataField: "employeeCode", caption: "Employee code", minWidth: 140 },
    { dataField: "email", caption: "Email", minWidth: 200 },
    { dataField: "departmentName", caption: "Department", minWidth: 140 },
    { dataField: "teamName", caption: "Team", minWidth: 140 },
    { dataField: "branchName", caption: "Branch", minWidth: 140 },
    {
      dataField: "isActive",
      caption: "Status",
      cellTemplate: "directoryStatus",
      minWidth: 100,
    },
    {
      dataField: "lastLoginAt",
      caption: "Last login",
      dataType: "datetime",
      format: "dd MMM yyyy, HH:mm",
      minWidth: 170,
    },
  ];
  protected searchDirectory(): void {
    this.directory.search(this.search(), () => void this.load());
  }
  protected pageDirectory(skip: number): void {
    this.directory.page(skip, this.directory.query(), () => void this.load());
  }
  protected async chooseApplication(value: string): Promise<void> {
    const id = +value || null;
    this.selectedApplicationId.set(id);
    if (id) await this.loadApplicationAccess(id);
  }
  protected open(mode: ActionMode): void {
    if (this.saving()) return;
    this.actionMode.set(mode);
    const profileUser =
      mode === "profile" ? (this.userAccess()?.user ?? null) : null;
    this.profileUser.set(profileUser);
    this.code.set("");
    this.name.set("");
    this.email.set(profileUser?.email ?? "");
    this.managerUserId.set(profileUser?.managerUserId ?? null);
    this.organizationMapping.set({
      departmentId: profileUser?.departmentId ?? null,
      teamId: profileUser?.teamId ?? null,
      branchId: profileUser?.branchId ?? null,
    });
    if (profileUser) this.name.set(profileUser.displayName);
    this.description.set("");
    this.reason.set("");
    this.effect.set("Allow");
    this.expiresAt.set("");
    this.selectedRoleId.set(
      this.applicationAccess()?.roles.find((x) => x.isActive)?.roleId ?? null,
    );
    this.selectedCapabilityId.set(
      this.applicationAccess()?.capabilities.find((x) => x.isActive)
        ?.moduleCapabilityId ?? null,
    );
    this.error.set(null);
    this.formNavigation.begin();
  }
  protected async close(): Promise<void> {
    if (!(await this.canLeaveForm())) return;
    this.error.set(null);
    this.formNavigation.finish();
    this.actionMode.set(null);
  }
  protected async submit(): Promise<void> {
    const mode = this.actionMode();
    const user =
      mode === "profile" ? this.profileUser() : this.userAccess()?.user;
    const appId = this.selectedApplicationId();
    if (!mode || this.saving()) return;
    this.saving.set(true);
    this.error.set(null);
    try {
      let userId = user?.userId;
      if (mode === "user") {
        if (!this.code().trim() || !this.name().trim())
          throw new Error("Employee code and display name are required.");
        const request: CreateUserRequest = {
          employeeCode: this.code().trim(),
          displayName: this.name().trim(),
          email: this.email().trim() || null,
          managerUserId: this.managerUserId(),
          organizationMapping: this.organizationMapping(),
        };
        userId = (await this.service.createUser(request)).resourceId;
      } else if (mode === "profile") {
        if (!user || !this.name().trim())
          throw new Error("Select a user and enter a display name.");
        const request: UpdateUserProfileRequest = {
          displayName: this.name().trim(),
          email: this.email().trim() || null,
          managerUserId: this.managerUserId(),
          organizationMapping: this.organizationMapping(),
        };
        await this.service.updateUserProfile(user.userId, request);
      } else {
        if (!user || !appId) throw new Error("Select a user and application.");
        if (mode === "grantApplication")
          await this.service.grantApplication(user.userId, appId);
        else if (mode === "role") {
          if (!this.code().trim() || !this.name().trim())
            throw new Error("Role code and name are required.");
          const request: CreateRoleRequest = {
            roleCode: this.code().trim(),
            roleName: this.name().trim(),
            description: this.description().trim() || null,
            isSystem: false,
          };
          await this.service.createRole(appId, request);
        } else if (mode === "assignRole") {
          if (!this.selectedRoleId()) throw new Error("Select a role.");
          await this.service.assignRole(
            user.userId,
            appId,
            this.selectedRoleId()!,
          );
        } else if (mode === "rolePermission") {
          if (!this.selectedRoleId() || !this.selectedCapabilityId())
            throw new Error("Select a role and capability.");
          await this.service.grantRolePermission(
            appId,
            this.selectedRoleId()!,
            this.selectedCapabilityId()!,
          );
        } else {
          if (!this.selectedCapabilityId() || !this.reason().trim())
            throw new Error("Capability and reason are required.");
          if (
            this.effect() === "Deny" &&
            !(await this.confirmations.ask({
              title: "Apply Deny override?",
              message:
                "Apply this Deny override? Deny takes precedence over all role grants and Allow overrides.",
              confirmText: "Apply Deny",
              tone: "danger",
            }))
          )
            return;
          const request: PermissionOverrideRequest = {
            effect: this.effect(),
            reason: this.reason().trim(),
            expiresAt: this.expiresAt()
              ? new Date(this.expiresAt()).toISOString()
              : null,
          };
          await this.service.setOverride(
            user.userId,
            appId,
            this.selectedCapabilityId()!,
            request,
          );
        }
      }
      this.notice.set(
        mode === "profile"
          ? "User profile saved. Permissions are unchanged."
          : "Access change saved.",
      );
      this.actionMode.set(null);
      this.formNavigation.finish();
      this.saving.set(false);
      await this.load(userId);
    } catch (failure) {
      const problem = failure as {
        error?: { detail?: string; title?: string };
        message?: string;
      };
      this.error.set(
        problem.error?.detail ??
          problem.error?.title ??
          problem.message ??
          "The access change failed.",
      );
    } finally {
      this.saving.set(false);
    }
  }
  protected async revoke(
    kind:
      | "User"
      | "UserApplication"
      | "Role"
      | "UserRole"
      | "RolePermission"
      | "UserPermissionOverride",
    resourceId: number,
    label: string,
  ): Promise<void> {
    const user = this.userAccess()?.user;
    const applicationId = this.selectedApplicationId();
    if (
      !user ||
      this.saving() ||
      !(await this.confirmations.ask({
        title: "Revoke access?",
        message: `Revoke ${label}? This access change is audited.`,
        confirmText: "Revoke",
        tone: "danger",
      }))
    )
      return;
    this.saving.set(true);
    try {
      await this.service.revoke(
        kind,
        resourceId,
        kind === "UserApplication" ? user.userId : undefined,
        kind === "UserApplication" ? (applicationId ?? undefined) : undefined,
      );
      this.notice.set(`${label} revoked.`);
      await this.load(kind === "User" ? undefined : user.userId);
    } catch (failure) {
      const problem = failure as {
        error?: { detail?: string; title?: string };
        message?: string;
      };
      this.error.set(
        problem.error?.detail ??
          problem.error?.title ??
          problem.message ??
          `${label} could not be revoked.`,
      );
    } finally {
      this.saving.set(false);
    }
  }
  protected formContext(): string {
    const user =
      this.actionMode() === "profile"
        ? this.profileUser()
        : this.userAccess()?.user;
    const application =
      this.actionMode() === "profile"
        ? null
        : this.applications().find(
            (item) => item.applicationId === this.selectedApplicationId(),
          );
    return [
      user ? `${user.displayName} (${user.employeeCode})` : "No user selected",
      application?.applicationName,
    ]
      .filter(Boolean)
      .join(" · ");
  }
  protected actionTitle(mode: ActionMode): string {
    return {
      user: "Create user",
      profile: "Edit user profile",
      grantApplication: "Grant application",
      role: "Create role",
      assignRole: "Assign role",
      rolePermission: "Grant role capability",
      override: "Set user override",
    }[mode];
  }
  protected activeRoles(): readonly {
    roleId: number;
    roleName: string;
    userRoleId: number;
  }[] {
    const appId = this.selectedApplicationId();
    return (this.userAccess()?.roleAssignments ?? [])
      .filter((x) => x.applicationId === appId && x.revokedAt === null)
      .map((x) => ({
        roleId: x.roleId,
        roleName: x.roleName,
        userRoleId: x.userRoleId,
      }));
  }
  protected overrides() {
    const appId = this.selectedApplicationId();
    return (this.userAccess()?.overrides ?? []).filter(
      (x) => x.applicationId === appId && x.revokedAt === null,
    );
  }
  protected selectedAccess() {
    return this.userAccess()?.applications.find(
      (x) => x.applicationId === this.selectedApplicationId(),
    );
  }
  protected permissionCodes(roleId: number): readonly string[] {
    return (this.applicationAccess()?.rolePermissions ?? [])
      .filter((x) => x.roleId === roleId && x.revokedAt === null)
      .map((x) => x.capabilityCode);
  }
  private async loadApplicationAccess(id: number): Promise<void> {
    const generation = this.loadGeneration;
    const access = await this.service.getApplicationAccess(id);
    if (
      generation === this.loadGeneration &&
      id === this.selectedApplicationId()
    )
      this.applicationAccess.set(access);
  }
}
