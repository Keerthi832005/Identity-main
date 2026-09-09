import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { RUNTIME_CONFIG } from "../../core/config/runtime-config";
import {
  AccessResourceKind,
  AdministrationResponse,
  ApplicationAccessCatalog,
  CreateRoleRequest,
  CreateUserRequest,
  PagedApplications,
  PagedUsers,
  PermissionOverrideRequest,
  UserAccessCatalog,
  UpdateUserProfileRequest,
} from "./user-access.models";

@Injectable({ providedIn: "root" })
export class UserAccessService {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(RUNTIME_CONFIG).identityBaseUrl}/api/v1/admin`;
  searchUsers(search = "", skip = 0): Promise<PagedUsers> {
    let params = new HttpParams().set("take", 50).set("skip", skip);
    if (search.trim()) params = params.set("search", search.trim());
    return firstValueFrom(
      this.http.get<PagedUsers>(`${this.base}/users`, { params }),
    );
  }
  searchApplications(search = "", skip = 0): Promise<PagedApplications> {
    let params = new HttpParams().set("take", 50).set("skip", skip);
    if (search.trim()) params = params.set("search", search.trim());
    return firstValueFrom(
      this.http.get<PagedApplications>(`${this.base}/applications`, { params }),
    );
  }
  getUserAccess(userId: number): Promise<UserAccessCatalog> {
    return firstValueFrom(
      this.http.get<UserAccessCatalog>(`${this.base}/users/${userId}/access`),
    );
  }
  getApplicationAccess(
    applicationId: number,
  ): Promise<ApplicationAccessCatalog> {
    return firstValueFrom(
      this.http.get<ApplicationAccessCatalog>(
        `${this.base}/applications/${applicationId}/access`,
      ),
    );
  }
  createUser(request: CreateUserRequest): Promise<AdministrationResponse> {
    return firstValueFrom(
      this.http.post<AdministrationResponse>(`${this.base}/users`, request),
    );
  }
  updateUserProfile(
    userId: number,
    request: UpdateUserProfileRequest,
  ): Promise<AdministrationResponse> {
    return firstValueFrom(
      this.http.put<AdministrationResponse>(
        `${this.base}/users/${userId}/profile`,
        request,
      ),
    );
  }
  createRole(
    applicationId: number,
    request: CreateRoleRequest,
  ): Promise<AdministrationResponse> {
    return firstValueFrom(
      this.http.post<AdministrationResponse>(
        `${this.base}/applications/${applicationId}/roles`,
        request,
      ),
    );
  }
  grantApplication(
    userId: number,
    applicationId: number,
  ): Promise<AdministrationResponse> {
    return firstValueFrom(
      this.http.post<AdministrationResponse>(
        `${this.base}/users/${userId}/applications/${applicationId}`,
        null,
      ),
    );
  }
  assignRole(
    userId: number,
    applicationId: number,
    roleId: number,
  ): Promise<AdministrationResponse> {
    return firstValueFrom(
      this.http.post<AdministrationResponse>(
        `${this.base}/users/${userId}/applications/${applicationId}/roles/${roleId}`,
        null,
      ),
    );
  }
  grantRolePermission(
    applicationId: number,
    roleId: number,
    capabilityId: number,
  ): Promise<AdministrationResponse> {
    return firstValueFrom(
      this.http.post<AdministrationResponse>(
        `${this.base}/applications/${applicationId}/roles/${roleId}/capabilities/${capabilityId}`,
        null,
      ),
    );
  }
  setOverride(
    userId: number,
    applicationId: number,
    capabilityId: number,
    request: PermissionOverrideRequest,
  ): Promise<AdministrationResponse> {
    return firstValueFrom(
      this.http.post<AdministrationResponse>(
        `${this.base}/users/${userId}/applications/${applicationId}/capabilities/${capabilityId}/override`,
        request,
      ),
    );
  }
  revoke(
    kind: AccessResourceKind,
    resourceId: number,
    userId?: number,
    applicationId?: number,
  ): Promise<AdministrationResponse> {
    let params = new HttpParams();
    if (userId) params = params.set("userId", userId);
    if (applicationId) params = params.set("applicationId", applicationId);
    return firstValueFrom(
      this.http.delete<AdministrationResponse>(
        `${this.base}/resources/${kind}/${resourceId}`,
        { params },
      ),
    );
  }
}
