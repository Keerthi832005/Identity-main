import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { RUNTIME_CONFIG } from "../../core/config/runtime-config";
import {
  AdministrationDashboard,
  PagedApplications,
  PagedUsers,
} from "./dashboard.models";

@Injectable({ providedIn: "root" })
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(RUNTIME_CONFIG).identityBaseUrl}/api/v1/admin`;

  getDashboard(recent = 5): Promise<AdministrationDashboard> {
    const params = new HttpParams().set("recent", recent);
    return firstValueFrom(
      this.http.get<AdministrationDashboard>(`${this.baseUrl}/dashboard`, {
        params,
      }),
    );
  }

  searchApplications(
    search: string,
    skip = 0,
    take = 20,
  ): Promise<PagedApplications> {
    return firstValueFrom(
      this.http.get<PagedApplications>(`${this.baseUrl}/applications`, {
        params: this.searchParams(search, skip, take),
      }),
    );
  }

  searchUsers(search: string, skip = 0, take = 20): Promise<PagedUsers> {
    return firstValueFrom(
      this.http.get<PagedUsers>(`${this.baseUrl}/users`, {
        params: this.searchParams(search, skip, take),
      }),
    );
  }

  private searchParams(search: string, skip: number, take: number): HttpParams {
    let params = new HttpParams().set("skip", skip).set("take", take);
    if (search.trim()) params = params.set("search", search.trim());
    return params;
  }
}
