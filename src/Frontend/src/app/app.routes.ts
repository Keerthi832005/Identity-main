import { canLeaveInlineForm } from "./shared/ui/inline-form-navigation";
import { Routes } from "@angular/router";
import { administrationGuard, authGuard } from "./core/auth/auth.guard";

import { canLeaveOrganizationEditor } from "./features/organization-management/organization-editor.guard";

export const routes: Routes = [
  {
    path: "login",
    loadComponent: () =>
      import("./features/sign-in/sign-in.component").then(
        (module) => module.SignInComponent,
      ),
  },
  {
    path: "access-denied",
    loadComponent: () =>
      import("./features/access-denied/access-denied.component").then(
        (module) => module.AccessDeniedComponent,
      ),
  },
  {
    path: "",
    canActivate: [authGuard, administrationGuard],
    loadComponent: () =>
      import("./shared/shell/shell.component").then(
        (module) => module.ShellComponent,
      ),
    children: [
      {
        path: "",
        loadComponent: () =>
          import("./features/dashboard/dashboard.component").then(
            (module) => module.DashboardComponent,
          ),
      },
      {
        path: "applications/:applicationId",
        canDeactivate: [canLeaveInlineForm],
        loadComponent: () =>
          import("./features/application-catalog/application-catalog.component").then(
            (m) => m.ApplicationCatalogComponent,
          ),
      },
      {
        path: "applications",
        canDeactivate: [canLeaveInlineForm],
        loadComponent: () =>
          import("./features/application-catalog/application-catalog.component").then(
            (module) => module.ApplicationCatalogComponent,
          ),
      },
      {
        path: "organizations",
        loadComponent: () =>
          import("./features/organization-management/organization-management.component").then(
            (module) => module.OrganizationManagementComponent,
          ),
        children: [
          {
            path: "new",
            data: { mode: "create" },
            canDeactivate: [canLeaveOrganizationEditor],
            loadComponent: () =>
              import("./features/organization-management/organization-editor.component").then(
                (module) => module.OrganizationEditorComponent,
              ),
          },
          {
            path: ":organizationId/units/:unitId/new/:unitType",
            data: { mode: "create" },
            canDeactivate: [canLeaveOrganizationEditor],
            loadComponent: () =>
              import("./features/organization-management/organization-editor.component").then(
                (module) => module.OrganizationEditorComponent,
              ),
          },
          {
            path: ":organizationId/units/:unitId/edit",
            data: { mode: "edit" },
            canDeactivate: [canLeaveOrganizationEditor],
            loadComponent: () =>
              import("./features/organization-management/organization-editor.component").then(
                (module) => module.OrganizationEditorComponent,
              ),
          },
        ],
      },
      {
        path: "users/:userId",
        canDeactivate: [canLeaveInlineForm],
        loadComponent: () =>
          import("./features/user-access/user-access.component").then(
            (m) => m.UserAccessComponent,
          ),
      },
      {
        path: "users",
        canDeactivate: [canLeaveInlineForm],
        loadComponent: () =>
          import("./features/user-access/user-access.component").then(
            (module) => module.UserAccessComponent,
          ),
      },
      {
        path: "bulk/map",
        loadComponent: () =>
          import("./shared/bulk-data/bulk-mapping.component").then(
            (module) => module.BulkMappingComponent,
          ),
      },
      {
        path: "bulk/:batchKey",
        loadComponent: () =>
          import("./shared/bulk-data/bulk-workspace.component").then(
            (module) => module.BulkWorkspaceComponent,
          ),
      },
      {
        path: "agents",
        loadComponent: () =>
          import("./features/agent-inventory/agent-inventory-list.component").then(
            (m) => m.AgentInventoryListComponent,
          ),
      },
      {
        path: "agents/:installationId",
        loadComponent: () =>
          import("./features/agent-inventory/agent-inventory.component").then(
            (m) => m.AgentInventoryComponent,
          ),
      },
      {
        path: "audit",
        loadComponent: () =>
          import("./features/security-audit/security-audit.component").then(
            (module) => module.SecurityAuditComponent,
          ),
      },
    ],
  },
  { path: "**", redirectTo: "" },
];
