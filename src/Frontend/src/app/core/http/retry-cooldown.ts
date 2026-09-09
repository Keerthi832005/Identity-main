import { HttpErrorResponse } from "@angular/common/http";
import { DestroyRef, signal } from "@angular/core";

/** In-memory UI cooldown only. The server remains the authority for rate limits. */
export class RetryCooldown {
  private readonly seconds = signal(0);
  readonly remainingSeconds = this.seconds.asReadonly();
  private until = 0;
  private destroyed = false;
  private timer: ReturnType<typeof setInterval> | undefined;

  constructor(destroyRef: DestroyRef) {
    destroyRef.onDestroy(() => {
      this.destroyed = true;
      this.stop();
    });
  }

  capture(error: unknown): boolean {
    if (this.destroyed) return false;
    if (!(error instanceof HttpErrorResponse) || error.status !== 429)
      return false;
    const value = error.headers.get("Retry-After")?.trim() ?? "";
    let seconds = 60;
    if (/^\d+$/.test(value)) {
      const parsed = Number(value);
      if (
        Number.isSafeInteger(parsed) &&
        parsed <= (Number.MAX_SAFE_INTEGER - Date.now()) / 1000
      ) {
        seconds = Math.max(1, parsed);
      }
    } else if (/^[A-Za-z]{3}, /.test(value)) {
      const date = Date.parse(value);
      if (Number.isFinite(date))
        seconds = Math.max(1, Math.ceil((date - Date.now()) / 1000));
    }

    this.stop();
    this.until = Date.now() + seconds * 1000;
    this.seconds.set(seconds);
    // Recompute from the deadline so background-tab timer throttling cannot extend the wait.
    this.timer = setInterval(() => {
      this.seconds.set(
        Math.max(0, Math.ceil((this.until - Date.now()) / 1000)),
      );
      if (this.seconds() === 0) this.stop();
    }, 1000);
    return true;
  }

  private stop(): void {
    clearInterval(this.timer);
    this.timer = undefined;
  }
}
