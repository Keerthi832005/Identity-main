import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { RUNTIME_CONFIG } from "../../core/config/runtime-config";
import {
  AdministrationResponse,
  EnrollTotpRequest,
  OperationResponse,
  PagedUsers,
  RegisterDeviceRequest,
  SetPasswordRequest,
  SetPinRequest,
  TotpEnrollmentResponse,
  UserSecurityCatalog,
  VerifyTotpRequest,
} from "./security-controls.models";

@Injectable({ providedIn: "root" })
export class SecurityControlsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(RUNTIME_CONFIG).identityBaseUrl}/api/v1/admin`;

  searchUsers(search = "", skip = 0): Promise<PagedUsers> {
    let params = new HttpParams().set("skip", skip).set("take", 50);
    if (search.trim()) params = params.set("search", search.trim());
    return firstValueFrom(
      this.http.get<PagedUsers>(`${this.baseUrl}/users`, { params }),
    );
  }
  getSecurity(userId: number): Promise<UserSecurityCatalog> {
    return firstValueFrom(
      this.http.get<UserSecurityCatalog>(
        `${this.baseUrl}/users/${userId}/security`,
      ),
    );
  }
  setPassword(
    userId: number,
    request: SetPasswordRequest,
  ): Promise<OperationResponse> {
    return firstValueFrom(
      this.http.post<OperationResponse>(
        `${this.baseUrl}/users/${userId}/password`,
        request,
      ),
    );
  }
  setPin(userId: number, request: SetPinRequest): Promise<OperationResponse> {
    return firstValueFrom(
      this.http.post<OperationResponse>(
        `${this.baseUrl}/users/${userId}/pin`,
        request,
      ),
    );
  }
  registerDevice(
    userId: number,
    request: RegisterDeviceRequest,
  ): Promise<AdministrationResponse> {
    return firstValueFrom(
      this.http.post<AdministrationResponse>(
        `${this.baseUrl}/users/${userId}/devices`,
        request,
      ),
    );
  }
  trustDevice(deviceId: number): Promise<OperationResponse> {
    return firstValueFrom(
      this.http.post<OperationResponse>(
        `${this.baseUrl}/devices/${deviceId}/trust`,
        {},
      ),
    );
  }
  revokeDevice(deviceId: number): Promise<AdministrationResponse> {
    return firstValueFrom(
      this.http.delete<AdministrationResponse>(
        `${this.baseUrl}/resources/Device/${deviceId}`,
      ),
    );
  }
  enrollTotp(
    userId: number,
    request: EnrollTotpRequest,
  ): Promise<TotpEnrollmentResponse> {
    return firstValueFrom(
      this.http.post<TotpEnrollmentResponse>(
        `${this.baseUrl}/users/${userId}/mfa/totp`,
        request,
      ),
    );
  }
  verifyTotp(
    methodId: number,
    request: VerifyTotpRequest,
  ): Promise<OperationResponse> {
    return firstValueFrom(
      this.http.post<OperationResponse>(
        `${this.baseUrl}/mfa/${methodId}/verify`,
        request,
      ),
    );
  }
  revokeMfa(methodId: number): Promise<OperationResponse> {
    return firstValueFrom(
      this.http.delete<OperationResponse>(`${this.baseUrl}/mfa/${methodId}`),
    );
  }
}
