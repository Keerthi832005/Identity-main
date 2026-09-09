import { Injectable, computed, signal } from "@angular/core";
import { JwtPayload } from "./session.models";

@Injectable({ providedIn: "root" })
export class AuthState {
  private readonly accessToken = signal<string | null>(null);
  private readonly payload = computed(() => this.decode(this.accessToken()));

  readonly isAuthenticated = computed(() => {
    const payload = this.payload();
    return (
      payload !== null && (!payload.exp || payload.exp * 1000 > Date.now())
    );
  });
  readonly employeeCode = computed(
    () => this.payload()?.employee_code ?? "Administrator",
  );
  readonly displayName = computed(
    () => this.payload()?.display_name ?? this.employeeCode(),
  );
  readonly capabilities = computed(() =>
    this.asArray(this.payload()?.capability),
  );
  readonly authorizationVersion = computed(
    () => this.payload()?.authorization_version ?? "—",
  );

  token(): string | null {
    if (!this.isAuthenticated()) {
      this.clear();
      return null;
    }
    return this.accessToken();
  }

  setToken(token: string): void {
    this.accessToken.set(token);
  }

  clear(): void {
    this.accessToken.set(null);
  }

  expiresWithin(seconds: number): boolean {
    const expiration = this.payload()?.exp;
    return (
      !expiration ||
      expiration * 1000 <= Date.now() + Math.max(0, seconds) * 1000
    );
  }

  hasCapability(capability: string): boolean {
    return this.capabilities().includes(capability);
  }

  private decode(token: string | null): JwtPayload | null {
    if (!token) return null;
    try {
      const encoded = token.split(".")[1];
      if (!encoded) return null;
      const normalized = encoded.replace(/-/g, "+").replace(/_/g, "/");
      return JSON.parse(
        atob(normalized.padEnd(Math.ceil(normalized.length / 4) * 4, "=")),
      ) as JwtPayload;
    } catch {
      return null;
    }
  }

  private asArray(value: string | string[] | undefined): readonly string[] {
    if (!value) return [];
    return Array.isArray(value) ? value : [value];
  }
}
