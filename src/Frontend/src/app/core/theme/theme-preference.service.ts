import { DOCUMENT } from "@angular/common";
import { Injectable, inject, signal } from "@angular/core";

export type ThemePreference = "light" | "dark" | "system";
export type ResolvedTheme = "light" | "dark";

const storageKey = "identity.theme-preference";

/** Matches the .dx-swatch-iam-{mode} class each generated ThemeBuilder bundle scopes itself under. */
function swatchClassFor(mode: ResolvedTheme): string {
  return `dx-swatch-iam-${mode}`;
}

@Injectable({ providedIn: "root" })
export class ThemePreferenceService {
  private readonly document = inject(DOCUMENT);
  private readonly media = this.document.defaultView?.matchMedia?.(
    "(prefers-color-scheme: dark)",
  );
  readonly preference = signal<ThemePreference>(this.readPreference());
  readonly resolved = signal<ResolvedTheme>(this.resolve(this.preference()));

  constructor() {
    this.apply();
    this.media?.addEventListener?.("change", () => {
      if (this.preference() === "system") this.apply();
    });
  }

  setPreference(preference: ThemePreference): void {
    this.preference.set(preference);
    this.safeStorage()?.setItem(storageKey, preference);
    this.apply();
  }

  toggle(): void {
    this.setPreference(this.resolved() === "dark" ? "light" : "dark");
  }

  private apply(): void {
    const resolved = this.resolve(this.preference());
    this.resolved.set(resolved);
    this.document.documentElement.dataset["theme"] = resolved;
    this.document.documentElement.style.colorScheme = resolved;
    // Both generated swatches ship in the bundle (angular.json styles); the active one is selected
    // by the class each scopes itself under, so there is no stylesheet request on toggle.
    const classList = this.document.body.classList;
    classList.remove(swatchClassFor("light"), swatchClassFor("dark"));
    classList.add(swatchClassFor(resolved));
  }

  private resolve(preference: ThemePreference): ResolvedTheme {
    return preference === "system"
      ? this.media?.matches
        ? "dark"
        : "light"
      : preference;
  }

  private readPreference(): ThemePreference {
    const value = this.safeStorage()?.getItem(storageKey);
    return value === "light" || value === "dark" || value === "system"
      ? value
      : "system";
  }

  private safeStorage(): Storage | null {
    try {
      return this.document.defaultView?.localStorage ?? null;
    } catch {
      return null;
    }
  }
}
