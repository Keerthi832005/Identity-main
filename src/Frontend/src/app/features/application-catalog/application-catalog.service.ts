import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { RUNTIME_CONFIG } from "../../core/config/runtime-config";
import { ApplicationAccessCatalog } from "../user-access/user-access.models";
import {
  AdministrationResponse,
  ApplicationCatalog,
  CatalogResourceKind,
  CreateApplicationClientRequest,
  CreateApplicationRequest,
  CreateCapabilityRequest,
  CreateModuleRequest,
  PagedApplicationUsers,
  PagedApplications,
} from "./application-catalog.models";

@Injectable({ providedIn: "root" })
export class ApplicationCatalogService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(RUNTIME_CONFIG).identityBaseUrl}/api/v1/admin`;

  search(search = "", skip = 0, take = 50): Promise<PagedApplications> {
    let params = new HttpParams().set("skip", skip).set("take", take);
    if (search.trim()) params = params.set("search", search.trim());
    return firstValueFrom(
      this.http.get<PagedApplications>(`${this.baseUrl}/applications`, {
        params,
      }),
    );
  }

  getCatalog(applicationId: number): Promise<ApplicationCatalog> {
    return firstValueFrom(
      this.http.get<ApplicationCatalog>(
        `${this.baseUrl}/applications/${applicationId}/catalog`,
      ),
    );
  }

  getAccess(applicationId: number): Promise<ApplicationAccessCatalog> {
    return firstValueFrom(
      this.http.get<ApplicationAccessCatalog>(
        `${this.baseUrl}/applications/${applicationId}/access`,
      ),
    );
  }

  getUsers(
    applicationId: number,
    search = "",
    skip = 0,
  ): Promise<PagedApplicationUsers> {
    let params = new HttpParams().set("skip", skip).set("take", 20);
    if (search.trim()) params = params.set("search", search.trim());
    return firstValueFrom(
      this.http.get<PagedApplicationUsers>(
        `${this.baseUrl}/applications/${applicationId}/users`,
        { params },
      ),
    );
  }

  createApplication(
    request: CreateApplicationRequest,
  ): Promise<AdministrationResponse> {
    return firstValueFrom(
      this.http.post<AdministrationResponse>(
        `${this.baseUrl}/applications`,
        request,
      ),
    );
  }

  createClient(
    applicationId: number,
    request: CreateApplicationClientRequest,
  ): Promise<AdministrationResponse> {
    return firstValueFrom(
      this.http.post<AdministrationResponse>(
        `${this.baseUrl}/applications/${applicationId}/clients`,
        request,
      ),
    );
  }

  createModule(
    applicationId: number,
    request: CreateModuleRequest,
  ): Promise<AdministrationResponse> {
    return firstValueFrom(
      this.http.post<AdministrationResponse>(
        `${this.baseUrl}/applications/${applicationId}/modules`,
        request,
      ),
    );
  }

  createCapability(
    applicationId: number,
    moduleId: number,
    request: CreateCapabilityRequest,
  ): Promise<AdministrationResponse> {
    return firstValueFrom(
      this.http.post<AdministrationResponse>(
        `${this.baseUrl}/applications/${applicationId}/modules/${moduleId}/capabilities`,
        request,
      ),
    );
  }

  moveModule(
    applicationId: number,
    moduleId: number,
    direction: "up" | "down",
  ): Promise<AdministrationResponse> {
    return firstValueFrom(
      this.http.post<AdministrationResponse>(
        `${this.baseUrl}/applications/${applicationId}/modules/${moduleId}/move/${direction}`,
        {},
      ),
    );
  }

  revoke(
    kind: CatalogResourceKind,
    resourceId: number,
  ): Promise<AdministrationResponse> {
    return firstValueFrom(
      this.http.delete<AdministrationResponse>(
        `${this.baseUrl}/resources/${kind}/${resourceId}`,
      ),
    );
  }
}
