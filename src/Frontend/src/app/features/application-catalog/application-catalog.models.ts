export interface ApplicationSummary {
  readonly applicationId: number;
  readonly applicationCode: string;
  readonly applicationName: string;
  readonly tokenAudience: string;
  readonly isActive: boolean;
  readonly createdAt: string;
  readonly updatedAt: string | null;
  readonly description?: string | null;
  readonly clientCount?: number;
  readonly moduleCount?: number;
  readonly roleCount?: number;
  readonly userCount?: number;
}

export interface ApplicationUserSummary {
  readonly userId: number;
  readonly employeeCode: string;
  readonly displayName: string;
  readonly email: string | null;
  readonly isActive: boolean;
  readonly assignedAt: string;
  readonly revokedAt: string | null;
  readonly roles: readonly string[];
}

export interface PagedApplicationUsers {
  readonly skip: number;
  readonly take: number;
  readonly totalCount: number;
  readonly items: readonly ApplicationUserSummary[];
}

/** The memberCount the /access endpoint now returns per role; kept local since
    user-access.models.ts (RoleSummary's home) is owned by another agent. */
export interface RoleMemberCount {
  readonly memberCount: number;
}

export interface ApplicationClientSummary {
  readonly applicationClientId: number;
  readonly applicationId: number;
  readonly clientId: string;
  readonly clientName: string;
  readonly clientType: "Public" | "Confidential" | "Service";
  readonly secretVersion: number;
  readonly isActive: boolean;
  readonly createdAt: string;
  readonly expiresAt: string | null;
  readonly revokedAt: string | null;
}

export interface ApplicationModuleSummary {
  readonly applicationModuleId: number;
  readonly applicationId: number;
  readonly moduleCode: string;
  readonly moduleName: string;
  readonly description: string | null;
  readonly parentApplicationModuleId: number | null;
  readonly displayOrder: number;
  readonly isSystem: boolean;
  readonly isActive: boolean;
  readonly createdAt: string;
  readonly updatedAt: string | null;
}

export interface ModuleCapabilitySummary {
  readonly moduleCapabilityId: number;
  readonly applicationId: number;
  readonly applicationModuleId: number;
  readonly capabilityCode: string;
  readonly capabilityName: string;
  readonly description: string | null;
  readonly isActive: boolean;
  readonly createdAt: string;
  readonly updatedAt: string | null;
}

export interface ApplicationCatalog {
  readonly application: ApplicationSummary;
  readonly clients: readonly ApplicationClientSummary[];
  readonly modules: readonly ApplicationModuleSummary[];
  readonly capabilities: readonly ModuleCapabilitySummary[];
}

export interface PagedApplications {
  readonly skip: number;
  readonly take: number;
  readonly totalCount: number;
  readonly items: readonly ApplicationSummary[];
}

export interface AdministrationResponse {
  readonly resourceType: string;
  readonly resourceId: number;
  readonly userId: number | null;
  readonly applicationId: number | null;
  readonly authorizationVersion: number | null;
}

export interface CreateApplicationRequest {
  readonly applicationCode: string;
  readonly applicationName: string;
  readonly description: string | null;
  readonly tokenAudience: string;
  readonly accessTokenLifetimeMinutes: number;
  readonly refreshTokenLifetimeDays: number;
}

export interface CreateApplicationClientRequest {
  readonly clientId: string;
  readonly clientName: string;
  readonly clientType: "Public" | "Confidential" | "Service";
  readonly clientSecret: string | null;
  readonly expiresAt: string | null;
}

export interface CreateModuleRequest {
  readonly moduleCode: string;
  readonly moduleName: string;
  readonly description: string | null;
  readonly parentApplicationModuleId: number | null;
  readonly displayOrder: number;
  readonly isSystem: boolean;
}

export interface CreateCapabilityRequest {
  readonly capabilityCode: string;
  readonly capabilityName: string;
  readonly description: string | null;
}

export type CatalogResourceKind =
  "Application" | "ApplicationClient" | "Module" | "Capability";

/** A module placed in its hierarchy, with the moves the administrator may actually make. */
export interface ModuleTreeNode {
  readonly module: ApplicationModuleSummary;
  readonly depth: number;
  readonly canMoveUp: boolean;
  readonly canMoveDown: boolean;
}
