import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { RUNTIME_CONFIG } from "../../core/config/runtime-config";
import {
  SecurityOperations,
  SecurityOperationsFilter,
} from "./security-audit.models";

@Injectable({ providedIn: "root" })
export class SecurityAuditService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(RUNTIME_CONFIG).identityBaseUrl}/api/v1/admin/security`;
  load(filter: SecurityOperationsFilter): Promise<SecurityOperations> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filter))
      if (value !== null && value !== "")
        params = params.set(key, String(value));
    return firstValueFrom(
      this.http.get<SecurityOperations>(`${this.baseUrl}/operations`, {
        params,
      }),
    );
  }
  revoke(tokenFamilyId: string): Promise<{ readonly succeeded: boolean }> {
    return firstValueFrom(
      this.http.delete<{ readonly succeeded: boolean }>(
        `${this.baseUrl}/sessions/${tokenFamilyId}`,
      ),
    );
  }
}
