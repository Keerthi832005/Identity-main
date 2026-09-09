import { DOCUMENT } from "@angular/common";
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  signal,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import {
  NavigationEnd,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from "@angular/router";
import { AuthState } from "../../core/auth/auth-state.service";
import { SessionService } from "../../core/auth/session.service";
import { RUNTIME_CONFIG } from "../../core/config/runtime-config";
import {
  ThemePreference,
  ThemePreferenceService,
} from "../../core/theme/theme-preference.service";
import {
  GlobalSearchGroup,
  GlobalSearchHit,
  GlobalSearchService,
} from "./global-search.service";

interface NavigationItem {
  readonly label: string;
  readonly route: string;
  readonly icon: string;
  readonly section: string;
  readonly keywords: string;
}

@Component({
  selector: "app-shell",
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: "./shell.component.html",
  styleUrl: "./shell.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    "(document:click)": "handleDocumentClick($event)",
    "(document:keydown)": "handleKeydown($event)",
  },
})
export class ShellComponent {
  protected readonly auth = inject(AuthState);
  protected readonly theme = inject(ThemePreferenceService);
  protected readonly applicationName = inject(RUNTIME_CONFIG).applicationName;
  private readonly session = inject(SessionService);
  private readonly globalSearch = inject(GlobalSearchService);
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);
  private readonly document = inject(DOCUMENT);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly media = this.document.defaultView?.matchMedia(
    "(max-width: 1000px)",
  );
  protected readonly isMobile = signal(this.media?.matches ?? false);
  protected readonly mobileNavigationOpen = signal(false);
  protected readonly collapsed = signal(false);
  protected readonly menuOpen = signal(false);
  protected readonly accountView = signal<
    "profile" | "permissions" | "session" | null
  >(null);
  protected readonly globalQuery = signal("");
  protected readonly searchOpen = signal(false);
  protected readonly hiddenSections = signal<readonly string[]>([]);
  protected readonly currentPath = signal(this.router.url.split(/[?#]/)[0]);
  protected readonly navigation: readonly NavigationItem[] = [
    {
      label: "Overview",
      route: "/",
      icon: "home",
      section: "Workspace",
      keywords: "dashboard overview",
    },
    {
      label: "Applications",
      route: "/applications",
      icon: "box",
      section: "Administration",
      keywords: "clients modules capabilities catalog",
    },
    {
      label: "Organizations",
      route: "/organizations",
      icon: "hierarchy",
      section: "Administration",
      keywords:
        "country countries regions states branches locations departments teams hierarchy",
    },
    {
      label: "Users & access",
      route: "/users",
      icon: "group",
      section: "Administration",
      keywords:
        "profile email manager roles grants permissions password credentials devices mfa security",
    },
    {
      label: "Machines & agents",
      route: "/agents",
      icon: "desktop",
      section: "Administration",
      keywords:
        "machines computers terminals agent hardware software inventory hostname",
    },
    {
      label: "Audit",
      route: "/audit",
      icon: "event",
      section: "Security",
      keywords: "sessions events investigation",
    },
  ];
  protected readonly breadcrumb = computed(() => {
    const path = this.currentPath();
    const matched = this.navigation.find(
      (item) =>
        item.route === path ||
        (item.route !== "/" && path.startsWith(item.route + "/")),
    );
    if (matched) return { section: matched.section, label: matched.label };

    const segments = path.split("/").filter(Boolean);
    return {
      section: title(segments.at(-2) ?? "Workspace"),
      label: title(segments.at(-1) ?? "Overview"),
    };
  });
  protected readonly groups = computed(() =>
    ["Workspace", "Administration", "Security"]
      .map((label) => ({
        label,
        items: this.navigation.filter((item) => item.section === label),
      }))
      .filter((group) => group.items.length),
  );
  protected readonly recordHits = signal<readonly GlobalSearchHit[]>([]);
  protected readonly searching = signal(false);
  protected readonly searchResults = computed<readonly GlobalSearchHit[]>(
    () => [
      ...this.matches(this.globalQuery()).map((item) => ({
        group: "Screens" as GlobalSearchGroup,
        id: `screen-${item.route}`,
        label: item.label,
        detail: item.section,
        route: item.route,
        icon: item.icon,
      })),
      ...this.recordHits(),
    ],
  );
  /* Rendered grouped but navigated flat, so each row carries the index the arrow keys use. */
  protected readonly searchGroups = computed(() => {
    const groups: {
      label: GlobalSearchGroup;
      items: { hit: GlobalSearchHit; index: number }[];
    }[] = [];
    this.searchResults().forEach((hit, index) => {
      let bucket = groups.find((group) => group.label === hit.group);
      if (!bucket) groups.push((bucket = { label: hit.group, items: [] }));
      bucket.items.push({ hit, index });
    });
    return groups;
  });
  protected readonly highlighted = signal(0);
  protected readonly activeOptionId = computed(() => {
    const results = this.searchResults();
    if (!this.searchOpen() || results.length === 0) return null;
    return `menu-option-${this.boundedHighlight()}`;
  });
  private searchTimer?: ReturnType<typeof setTimeout>;

  protected boundedHighlight(): number {
    const count = this.searchResults().length;
    return count === 0 ? 0 : Math.min(this.highlighted(), count - 1);
  }

  protected moveHighlight(offset: number): void {
    const count = this.searchResults().length;
    if (count === 0) return;
    this.highlighted.set((this.boundedHighlight() + offset + count) % count);
  }
  protected readonly themeOptions: readonly {
    value: ThemePreference;
    label: string;
    icon: string;
  }[] = [
    { value: "light", label: "Light", icon: "sun" },
    { value: "dark", label: "Dark", icon: "moon" },
    { value: "system", label: "System", icon: "preferences" },
  ];

  constructor() {
    const resize = (event: MediaQueryListEvent): void => {
      this.isMobile.set(event.matches);
      this.mobileNavigationOpen.set(false);
    };
    this.media?.addEventListener("change", resize);
    this.destroyRef.onDestroy(() =>
      this.media?.removeEventListener("change", resize),
    );
    /* Debounced: a keystroke fans out to four record endpoints, so only the pause is queried. */
    effect(() => {
      const query = this.globalQuery().trim();
      clearTimeout(this.searchTimer);
      if (query.length < 2) {
        this.recordHits.set([]);
        this.searching.set(false);
        return;
      }
      this.searching.set(true);
      this.searchTimer = setTimeout(() => {
        void this.globalSearch
          .search(query)
          .then((hits) => {
            /* A slower earlier request must not overwrite the newest term's results. */
            if (this.globalQuery().trim() === query) this.recordHits.set(hits);
          })
          .finally(() => {
            if (this.globalQuery().trim() === query) this.searching.set(false);
          });
      }, 250);
    });
    this.destroyRef.onDestroy(() => clearTimeout(this.searchTimer));
    this.router.events.pipe(takeUntilDestroyed()).subscribe((event) => {
      if (event instanceof NavigationEnd) {
        this.currentPath.set(event.urlAfterRedirects.split(/[?#]/)[0]);
        this.mobileNavigationOpen.set(false);
        this.menuOpen.set(false);
        this.searchOpen.set(false);
        this.globalQuery.set("");
        this.recordHits.set([]);
      }
    });
  }

  private matches(query: string): readonly NavigationItem[] {
    const normalized = query.trim().toLocaleLowerCase();
    return this.navigation.filter((item) =>
      `${item.label} ${item.section} ${item.keywords}`
        .toLocaleLowerCase()
        .includes(normalized),
    );
  }

  protected initials(): string {
    return this.auth
      .displayName()
      .trim()
      .split(/\s+/)
      .map((part) => part[0])
      .join("")
      .slice(0, 2)
      .toUpperCase();
  }

  protected toggleNavigation(): void {
    this.menuOpen.set(false);
    this.searchOpen.set(false);
    if (this.isMobile()) {
      this.mobileNavigationOpen.update((open) => !open);
      if (this.mobileNavigationOpen())
        setTimeout(() => this.focus(".sidebar .nav-close"));
    } else this.collapsed.update((value) => !value);
  }

  protected closeNavigation(): void {
    this.mobileNavigationOpen.set(false);
    setTimeout(() => this.focus(".navigation-toggle"));
  }

  protected toggleSection(label: string): void {
    this.hiddenSections.update((items) =>
      items.includes(label)
        ? items.filter((item) => item !== label)
        : [...items, label],
    );
  }

  protected sectionOpen(label: string): boolean {
    return (
      !this.hiddenSections().includes(label) ||
      (this.collapsed() && !this.isMobile())
    );
  }

  protected toggleMenu(): void {
    this.searchOpen.set(false);
    this.accountView.set(null);
    this.menuOpen.update((value) => !value);
    if (this.menuOpen()) {
      setTimeout(() => this.focus(".user-menu__panel button"));
    } else {
      /* Closing must return focus to the trigger, or the caret is left nowhere. */
      this.focus(".user-menu__trigger");
    }
  }

  /* The account panel is a dialog: Tab must not escape it while it is open. */
  private trapAccountMenu(event: KeyboardEvent): void {
    const panel = this.host.nativeElement.querySelector(".user-menu__panel");
    if (!panel) return;
    const items = Array.from(
      panel.querySelectorAll<HTMLElement>(
        "a[href], button:not(:disabled), input, [tabindex]:not([tabindex='-1'])",
      ),
    ).filter((item) => item.getClientRects().length > 0);
    const first = items[0];
    const last = items.at(-1);
    if (event.shiftKey && this.document.activeElement === first) {
      event.preventDefault();
      last?.focus();
    } else if (!event.shiftKey && this.document.activeElement === last) {
      event.preventDefault();
      first?.focus();
    }
  }

  protected selectAccount(view: "profile" | "permissions" | "session"): void {
    this.accountView.update((value) => (value === view ? null : view));
  }

  protected submitSearch(): void {
    const chosen = this.searchResults()[this.boundedHighlight()];
    if (chosen) {
      this.closeSearch();
      void this.router.navigateByUrl(chosen.route);
    }
  }

  protected closeSearch(): void {
    this.searchOpen.set(false);
    this.recordHits.set([]);
    this.globalQuery.set("");
  }

  protected handleSearchKeys(event: KeyboardEvent | undefined): void {
    const offset =
      event?.key === "ArrowDown" ? 1 : event?.key === "ArrowUp" ? -1 : 0;
    if (!offset) return;
    event?.preventDefault();
    this.searchOpen.set(true);
    this.moveHighlight(offset);
  }

  protected handleDocumentClick(event: Event): void {
    const target = event.target as Node | null;
    if (!target) return;
    if (!this.host.nativeElement.querySelector(".user-menu")?.contains(target))
      this.menuOpen.set(false);
    if (
      !this.host.nativeElement.querySelector(".global-search")?.contains(target)
    )
      this.searchOpen.set(false);
  }

  protected handleKeydown(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
      event.preventDefault();
      this.menuOpen.set(false);
      this.mobileNavigationOpen.set(false);
      this.searchOpen.set(true);
      setTimeout(() => this.focus(".global-search__input"));
    }
    if (event.key === "Escape") {
      if (this.mobileNavigationOpen()) this.closeNavigation();
      else if (this.menuOpen()) {
        this.menuOpen.set(false);
        this.focus(".user-menu__trigger");
      } else if (this.searchOpen()) {
        this.focus(".global-search__input");
        this.searchOpen.set(false);
      }
    }
    if (event.key === "Tab" && this.menuOpen()) {
      this.trapAccountMenu(event);
      return;
    }
    if (event.key === "Tab" && this.mobileNavigationOpen()) {
      const items = Array.from(
        this.host.nativeElement.querySelectorAll<HTMLElement>(
          ".sidebar a[href], .sidebar button:not(:disabled), .sidebar input",
        ),
      ).filter((item) => item.getClientRects().length > 0);
      const first = items[0],
        last = items.at(-1);
      if (event.shiftKey && this.document.activeElement === first) {
        event.preventDefault();
        last?.focus();
      } else if (!event.shiftKey && this.document.activeElement === last) {
        event.preventDefault();
        first?.focus();
      }
    }
  }

  private focus(selector: string): void {
    this.host.nativeElement.querySelector<HTMLElement>(selector)?.focus();
  }
  protected handleThemeKeys(event: KeyboardEvent): void {
    const offset =
      event.key === "ArrowRight" || event.key === "ArrowDown"
        ? 1
        : event.key === "ArrowLeft" || event.key === "ArrowUp"
          ? -1
          : 0;
    if (!offset) return;
    event.preventDefault();
    const index = this.themeOptions.findIndex(
      (option) => option.value === this.theme.preference(),
    );
    const option =
      this.themeOptions[
        (index + offset + this.themeOptions.length) % this.themeOptions.length
      ];
    this.setTheme(option.value);
    this.focus(`[data-theme-option="${option.value}"]`);
  }

  protected setTheme(value: ThemePreference): void {
    this.theme.setPreference(value);
  }
  protected async logout(): Promise<void> {
    await this.session.logout();
    await this.router.navigateByUrl("/login");
  }
}

function title(segment: string): string {
  const trimmed = segment.trim();
  if (!trimmed) return "Overview";

  /* Tested before separators are stripped: a batch key is mostly hyphens, and replacing them first
     turns the identifier into something that looks like words and gets title-cased. */
  if (/^[0-9a-f-]{8,}$/i.test(trimmed)) return "Details";

  const readable = trimmed.replace(/[-_]/g, " ").trim();
  return readable
    ? readable.charAt(0).toUpperCase() + readable.slice(1)
    : "Overview";
}
