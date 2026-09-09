import { InjectionToken } from "@angular/core";

export interface RuntimeConfig {
  readonly identityBaseUrl: string;
  readonly identityClientId: string;
  readonly applicationName: string;
  readonly devExtremeLicenseKey: string;
}

export const RUNTIME_CONFIG = new InjectionToken<RuntimeConfig>(
  "RUNTIME_CONFIG",
);

export async function loadRuntimeConfig(): Promise<RuntimeConfig> {
  const response = await fetch("/config.json", { cache: "no-store" });
  if (!response.ok)
    throw new Error(`Unable to load runtime configuration: ${response.status}`);
  return normalizeRuntimeConfig(await response.json());
}

export function normalizeRuntimeConfig(value: unknown): RuntimeConfig {
  if (!isRecord(value))
    throw new Error("Runtime configuration must be a JSON object.");
  return {
    identityBaseUrl: sameOriginPath(requiredString(value, "identityBaseUrl")),
    identityClientId: requiredString(value, "identityClientId"),
    applicationName: requiredString(value, "applicationName"),
    devExtremeLicenseKey: optionalString(value, "devExtremeLicenseKey"),
  };
}

function sameOriginPath(value: string): string {
  const normalized = value === "/" ? "" : value.replace(/\/$/, "");
  if (
    normalized &&
    (!normalized.startsWith("/") || normalized.startsWith("//"))
  ) {
    throw new Error(`Identity base URL '${value}' must be a same-origin path.`);
  }
  return normalized;
}

function requiredString(value: Record<string, unknown>, key: string): string {
  const result = optionalString(value, key);
  if (!result)
    throw new Error(
      `Runtime configuration '${key}' must be a non-empty string.`,
    );
  return result;
}

function optionalString(value: Record<string, unknown>, key: string): string {
  const candidate = value[key];
  return typeof candidate === "string" ? candidate.trim() : "";
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
