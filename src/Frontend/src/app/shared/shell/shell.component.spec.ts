import { ElementRef, signal } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { Router } from "@angular/router";
import { NEVER } from "rxjs";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthState } from "../../core/auth/auth-state.service";
import { SessionService } from "../../core/auth/session.service";
import { RUNTIME_CONFIG } from "../../core/config/runtime-config";
import { ThemePreferenceService } from "../../core/theme/theme-preference.service";
import { ShellComponent } from "./shell.component";

/**
 * Exposes the members the template binds to. The breadcrumb and the palette were only ever verified
 * in a browser, and both had defects that a unit test would have caught.
 */
class ShellHarness extends ShellComponent {
  goTo(path: string): void {
    this.currentPath.set(path);
  }
  crumb(): { section: string; label: string } {
    return this.breadcrumb();
  }
  query(value: string): void {
    this.globalQuery.set(value);
    this.searchOpen.set(true);
    this.highlighted.set(0);
  }
  move(offset: number): void {
    this.moveHighlight(offset);
  }
  highlightIndex(): number {
    return this.boundedHighlight();
  }
  activeOption(): string | null {
    return this.activeOptionId();
  }
  resultRoutes(): readonly string[] {
    return this.searchResults().map((item) => item.route);
  }
  hidePalette(): void {
    this.searchOpen.set(false);
  }
  choose(): void {
    this.submitSearch();
  }
}

describe("Shell", () => {
  const navigateByUrl = vi.fn(async () => true);

  beforeEach(() => {
    navigateByUrl.mockClear();
    /* jsdom ships no matchMedia, and the shell asks it whether the viewport is mobile. */
    Object.defineProperty(window, "matchMedia", {
      writable: true,
      configurable: true,
      value: (query: string) => ({
        matches: false,
        media: query,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        addListener: vi.fn(),
        removeListener: vi.fn(),
        onchange: null,
        dispatchEvent: vi.fn(),
      }),
    });
    TestBed.configureTestingModule({
      providers: [
        {
          provide: Router,
          useValue: { events: NEVER, url: "/", navigateByUrl },
        },
        // The shell reaches into its own host to move focus; these tests exercise state, not focus.
        {
          provide: ElementRef,
          useValue: new ElementRef(document.createElement("div")),
        },
        {
          provide: AuthState,
          useValue: {
            displayName: signal("Vinothkumar S"),
            employeeCode: signal("INDE03275"),
            capabilities: signal(["iam.admin"]),
            authorizationVersion: signal(3),
          },
        },
        {
          provide: SessionService,
          useValue: { logout: vi.fn(async () => {}) },
        },
        {
          provide: ThemePreferenceService,
          useValue: {
            preference: signal("system"),
            resolved: signal("light"),
            setPreference: vi.fn(),
          },
        },
        {
          provide: RUNTIME_CONFIG,
          useValue: {
            identityBaseUrl: "/identity",
            identityClientId: "identity-admin-web",
            applicationName: "Identity Administration",
            devExtremeLicenseKey: "",
          },
        },
      ],
    });
  });

  function shell(): ShellHarness {
    return TestBed.runInInjectionContext(() => new ShellHarness());
  }

  describe("breadcrumb", () => {
    it("names the navigation entry for a route it knows", () => {
      const component = shell();
      component.goTo("/users");

      expect(component.crumb()).toEqual({
        section: "Administration",
        label: "Users & access",
      });
    });

    it("derives segments for a route the navigation list does not contain", () => {
      /* The regression: activeItem falls back to the first entry so the stepper always has a
         position, and the breadcrumb used to inherit that and name the wrong screen. */
      const component = shell();
      component.goTo("/bulk/map");

      expect(component.crumb()).toEqual({ section: "Bulk", label: "Map" });
    });

    it("does not try to prettify an identifier segment", () => {
      const component = shell();
      component.goTo("/bulk/aa11bb22-cc33-4d44-8e55-ff6677889900");

      expect(component.crumb()).toEqual({ section: "Bulk", label: "Details" });
    });

    it("matches a child route to its parent navigation entry", () => {
      const component = shell();
      component.goTo("/organizations/10/units/100/edit");

      expect(component.crumb().label).toBe("Organizations");
    });
  });

  describe("command palette", () => {
    it("lists menus in the order the sidebar groups them", () => {
      /* The sidebar groups by section, but the palette and the prev/next stepper walk the flat list:
         an entry filed out of section order shows up between two unrelated menus. */
      const component = shell();
      component.query("");

      expect(component.resultRoutes()).toEqual([
        "/",
        "/applications",
        "/organizations",
        "/users",
        "/agents",
        "/audit",
      ]);
    });

    it("filters to matching menus", () => {
      const component = shell();
      component.query("audit");

      expect(component.resultRoutes()).toEqual(["/audit"]);
    });

    it("moves the highlight with arrow keys and wraps at both ends", () => {
      const component = shell();
      component.query("");
      const count = component.resultRoutes().length;

      expect(component.highlightIndex()).toBe(0);
      component.move(1);
      expect(component.highlightIndex()).toBe(1);
      component.move(-1);
      expect(component.highlightIndex()).toBe(0);
      /* Wrapping backwards from the first option lands on the last. */
      component.move(-1);
      expect(component.highlightIndex()).toBe(count - 1);
      component.move(1);
      expect(component.highlightIndex()).toBe(0);
    });

    it("publishes an active option only while the palette is open with results", () => {
      const component = shell();
      component.query("audit");
      expect(component.activeOption()).toBe("menu-option-0");

      component.hidePalette();
      expect(component.activeOption()).toBeNull();

      component.query("no-such-menu");
      expect(component.resultRoutes()).toEqual([]);
      expect(component.activeOption()).toBeNull();
    });

    it("keeps the highlight in range when the result list shrinks", () => {
      const component = shell();
      component.query("");
      component.move(1);
      component.move(1);
      expect(component.highlightIndex()).toBe(2);

      /* Narrowing to one result must not leave the highlight pointing past the end. */
      component.query("audit");
      expect(component.highlightIndex()).toBe(0);
    });

    it("navigates to the highlighted result rather than always the first", () => {
      const component = shell();
      component.query("");
      component.move(1);
      const expected = component.resultRoutes()[1];

      component.choose();

      expect(navigateByUrl).toHaveBeenCalledWith(expected);
    });
  });
});
