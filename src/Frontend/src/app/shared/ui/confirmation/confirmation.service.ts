import { DestroyRef, inject, Injectable, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { NavigationStart, Router } from "@angular/router";

export interface ConfirmationRequest {
  readonly title: string;
  readonly message: string;
  readonly confirmText: string;
  readonly tone?: "danger" | "default";
}

/** One explicit decision at a time. Dismissal and navigation never approve an action. */
@Injectable({ providedIn: "root" })
export class ConfirmationService {
  private readonly current = signal<ConfirmationRequest | null>(null);
  readonly active = this.current.asReadonly();
  private complete: ((confirmed: boolean) => void) | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.cancel());
    inject(Router)
      .events.pipe(takeUntilDestroyed())
      .subscribe((event) => {
        if (event instanceof NavigationStart) this.cancel();
      });
  }

  ask(request: ConfirmationRequest): Promise<boolean> {
    // Do not queue duplicate clicks or silently replace the decision already on screen.
    if (this.current()) return Promise.resolve(false);
    return new Promise((resolve) => {
      this.complete = resolve;
      this.current.set({ ...request });
    });
  }

  respond(request: ConfirmationRequest, confirmed: boolean): void {
    if (this.current() !== request) return;
    const complete = this.complete;
    this.complete = null;
    this.current.set(null);
    complete?.(confirmed);
  }

  cancel(): void {
    const request = this.current();
    if (request) this.respond(request, false);
  }
}
