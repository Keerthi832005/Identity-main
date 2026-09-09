import { DestroyRef, inject, signal } from "@angular/core";
import { ActivatedRoute, Router } from "@angular/router";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { combineLatest } from "rxjs";

/** Keeps directory searches and server pages in the URL, including when returning from a detail. */
export class DirectoryRoute {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  readonly id = signal<number | null>(null);
  readonly skip = signal(0);
  readonly total = signal(0);
  readonly query = signal("");
  readonly params = signal<Record<string, string | number | null>>({});
  constructor(
    private readonly path: string,
    private readonly key: string,
  ) {}
  start(load: () => void): void {
    combineLatest([this.route.paramMap, this.route.queryParamMap])
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(([params, query]) => {
        const id = Number(params.get(this.key));
        this.id.set(
          Number.isSafeInteger(id) && id > 0
            ? id
            : params.has(this.key)
              ? -1
              : null,
        );
        const page = Number(query.get("page") || 1);
        this.skip.set(
          (Number.isSafeInteger(page) && page > 0 && page < 1_000_000
            ? page - 1
            : 0) * 50,
        );
        this.query.set(query.get("search") || "");
        this.params.set({
          search: this.query() || null,
          page: this.skip() / 50 + 1,
        });
        load();
      });
  }
  search(query: string, load: () => void): void {
    this.page(0, query, load);
  }
  page(skip: number, query: string, load: () => void): void {
    if (this.skip() === skip && this.query() === query) {
      load();
      return;
    }
    void this.router.navigate([this.path], {
      queryParams: { search: query || null, page: skip / 50 + 1 },
    });
  }
  /** Returns to the list, keeping the search and page the detail was opened from. */
  back(): Promise<boolean> {
    return this.router.navigate([this.path], { queryParams: this.params() });
  }
  open(id: number): Promise<boolean> {
    return this.router.navigate([this.path, id], {
      queryParams: this.params(),
    });
  }
}
