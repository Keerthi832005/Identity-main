import { DOCUMENT } from "@angular/common";
import { Injectable, computed, inject } from "@angular/core";
import { ThemePreferenceService } from "./theme-preference.service";

/** Colour roles a DevExtreme chart needs as JS values - the widgets cannot read CSS custom
    properties themselves. Status roles only; brand (--app-primary) is deliberately excluded. */
export interface ChartPalette {
  readonly neutral: string;
  readonly success: string;
  readonly warning: string;
  readonly danger: string;
  readonly text: string;
  readonly textSecondary: string;
  readonly border: string;
  readonly surface: string;
}

const fallback: ChartPalette = {
  neutral: "#64748b",
  success: "#8bc34a",
  warning: "#ffc107",
  danger: "#ea580c",
  text: "#1f2126",
  textSecondary: "#6b7078",
  border: "#e6e6ea",
  surface: "#ffffff",
};

/**
 * Reads the product's design tokens off `<body>` so charts stay in step with the active theme.
 *
 * ThemePreferenceService applies the resolved theme (swatch class, dataset) synchronously before
 * this service would ever be read, so depending on its `resolved` signal is enough to invalidate
 * the computed on every toggle - no extra scheduling needed.
 */
@Injectable({ providedIn: "root" })
export class ChartPaletteService {
  private readonly document = inject(DOCUMENT);
  private readonly theme = inject(ThemePreferenceService);

  readonly palette = computed<ChartPalette>(() => {
    this.theme.resolved();
    return this.read();
  });

  private read(): ChartPalette {
    const view = this.document.defaultView;
    const body = this.document.body;
    if (!view || !body) return fallback;
    const style = view.getComputedStyle(body);
    const token = (name: string, value: string) =>
      style.getPropertyValue(name).trim() || value;
    return {
      neutral: token("--app-series-neutral", fallback.neutral),
      success: token("--app-success", fallback.success),
      warning: token("--app-warning", fallback.warning),
      danger: token("--app-danger", fallback.danger),
      text: token("--app-text", fallback.text),
      textSecondary: token("--app-text-secondary", fallback.textSecondary),
      border: token("--app-border", fallback.border),
      surface: token("--app-surface", fallback.surface),
    };
  }
}
