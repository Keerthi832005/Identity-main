import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { RUNTIME_CONFIG } from "../../core/config/runtime-config";
import {
  OrganizationCreated,
  OrganizationFields,
  CreateOrganizationChild,
  UpdateOrganizationFields,
  OrganizationPage,
  OrganizationSearch,
  OrganizationUnit,
} from "./organization.models";

@Injectable({ providedIn: "root" })
export class OrganizationService {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(RUNTIME_CONFIG).identityBaseUrl}/api/v1/admin`;
  search(query: OrganizationSearch): Promise<OrganizationPage> {
    let params = new HttpParams()
      .set("skip", query.skip ?? 0)
      .set("take", query.take ?? 20);
    if (query.organizationId !== undefined)
      params = params.set("organizationId", query.organizationId);
    if (query.parentOrganizationUnitId !== undefined)
      params = params.set(
        "parentOrganizationUnitId",
        query.parentOrganizationUnitId,
      );
    if (query.unitType) params = params.set("unitType", query.unitType);
    if (query.isActive !== undefined)
      params = params.set("isActive", query.isActive);
    if (query.search?.trim())
      params = params.set("search", query.search.trim());
    return firstValueFrom(
      this.http.get<OrganizationPage>(`${this.base}/organization-units`, {
        params,
      }),
    );
  }
  get(organizationId: number, unitId: number): Promise<OrganizationUnit> {
    return firstValueFrom(
      this.http.get<OrganizationUnit>(
        `${this.base}/organizations/${organizationId}/units/${unitId}`,
      ),
    );
  }
  create(request: OrganizationFields): Promise<OrganizationCreated> {
    return firstValueFrom(
      this.http.post<OrganizationCreated>(
        `${this.base}/organizations`,
        request,
      ),
    );
  }
  createChild(
    organizationId: number,
    request: CreateOrganizationChild,
  ): Promise<OrganizationCreated> {
    return firstValueFrom(
      this.http.post<OrganizationCreated>(
        `${this.base}/organizations/${organizationId}/units`,
        request,
      ),
    );
  }
  update(
    unit: OrganizationUnit,
    request: UpdateOrganizationFields,
  ): Promise<OrganizationUnit> {
    return firstValueFrom(
      this.http.put<OrganizationUnit>(
        `${this.base}/organizations/${unit.organizationId}/units/${unit.organizationUnitId}`,
        request,
      ),
    );
  }
  setActive(
    unit: OrganizationUnit,
    isActive: boolean,
  ): Promise<OrganizationUnit> {
    return firstValueFrom(
      this.http.put<OrganizationUnit>(
        `${this.base}/organizations/${unit.organizationId}/units/${unit.organizationUnitId}/active`,
        { isActive, rowVersion: unit.rowVersion },
      ),
    );
  }
}
