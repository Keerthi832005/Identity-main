export interface UserSummary {
  readonly userId: number;
  readonly employeeCode: string;
  readonly displayName: string;
  readonly isActive: boolean;
  readonly securityVersion: number;
  readonly lastLoginAt: string | null;
  readonly lockoutEndAt: string | null;
  readonly createdAt: string;
  readonly updatedAt: string | null;
  readonly email: string | null;
  readonly managerUserId: number | null;
  readonly managerDisplayName: string | null;
  readonly departmentId?: number | null;
  readonly departmentName?: string | null;
  readonly teamId?: number | null;
  readonly teamName?: string | null;
  readonly branchId?: number | null;
  readonly branchName?: string | null;
  readonly stateId?: number | null;
  readonly stateName?: string | null;
  readonly regionId?: number | null;
  readonly regionName?: string | null;
  readonly countryId?: number | null;
  readonly countryName?: string | null;
}
export interface ApplicationSummary {
  readonly applicationId: number;
  readonly applicationCode: string;
  readonly applicationName: string;
  readonly tokenAudience: string;
  readonly isActive: boolean;
  readonly createdAt: string;
  readonly updatedAt: string | null;
}
export interface PagedUsers {
  readonly skip: number;
  readonly take: number;
  readonly totalCount: number;
  readonly items: readonly UserSummary[];
}
export interface PagedApplications {
  readonly skip: number;
  readonly take: number;
  readonly totalCount: number;
  readonly items: readonly ApplicationSummary[];
}
export interface UserApplicationSummary {
  readonly userId: number;
  readonly applicationId: number;
  readonly applicationCode: string;
  readonly applicationName: string;
  readonly isActive: boolean;
  readonly authorizationVersion: number;
  readonly assignedAt: string;
  readonly revokedAt: string | null;
  readonly effectiveCapabilities: readonly string[];
}
export interface UserRoleSummary {
  readonly userRoleId: number;
  readonly userId: number;
  readonly applicationId: number;
  readonly roleId: number;
  readonly roleCode: string;
  readonly roleName: string;
  readonly assignedAt: string;
  readonly revokedAt: string | null;
}
export interface UserOverrideSummary {
  readonly userPermissionOverrideId: number;
  readonly userId: number;
  readonly applicationId: number;
  readonly moduleCapabilityId: number;
  readonly capabilityCode: string;
  readonly effect: "Allow" | "Deny";
  readonly reason: string;
  readonly assignedAt: string;
  readonly expiresAt: string | null;
  readonly revokedAt: string | null;
}
export interface UserAccessCatalog {
  readonly user: UserSummary;
  readonly applications: readonly UserApplicationSummary[];
  readonly roleAssignments: readonly UserRoleSummary[];
  readonly overrides: readonly UserOverrideSummary[];
  readonly evaluatedAt: string;
}
export interface RoleSummary {
  readonly roleId: number;
  readonly applicationId: number;
  readonly roleCode: string;
  readonly roleName: string;
  readonly description: string | null;
  readonly isSystem: boolean;
  readonly isActive: boolean;
}
export interface RolePermissionSummary {
  readonly rolePermissionId: number;
  readonly applicationId: number;
  readonly roleId: number;
  readonly moduleCapabilityId: number;
  readonly capabilityCode: string;
  readonly grantedAt: string;
  readonly revokedAt: string | null;
}
export interface CapabilitySummary {
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
export interface ApplicationAccessCatalog {
  readonly applicationId: number;
  readonly roles: readonly RoleSummary[];
  readonly rolePermissions: readonly RolePermissionSummary[];
  readonly capabilities: readonly CapabilitySummary[];
}
export interface AdministrationResponse {
  readonly resourceType: string;
  readonly resourceId: number;
  readonly userId: number | null;
  readonly applicationId: number | null;
  readonly authorizationVersion: number | null;
}
export interface CreateUserRequest {
  readonly employeeCode: string;
  readonly displayName: string;
  readonly email: string | null;
  readonly managerUserId: number | null;
  readonly organizationMapping?: UserOrganizationMapping;
}
export interface UserOrganizationMapping {
  readonly departmentId: number | null;
  readonly teamId: number | null;
  readonly branchId: number | null;
}
export interface UpdateUserProfileRequest {
  readonly displayName: string;
  readonly email: string | null;
  readonly managerUserId: number | null;
  readonly organizationMapping?: UserOrganizationMapping;
}
export interface CreateRoleRequest {
  readonly roleCode: string;
  readonly roleName: string;
  readonly description: string | null;
  readonly isSystem: boolean;
}
export interface PermissionOverrideRequest {
  readonly effect: "Allow" | "Deny";
  readonly reason: string;
  readonly expiresAt: string | null;
}
export type AccessResourceKind =
  | "User"
  | "UserApplication"
  | "Role"
  | "UserRole"
  | "RolePermission"
  | "UserPermissionOverride";
