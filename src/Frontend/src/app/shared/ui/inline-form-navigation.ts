import { CanDeactivateFn } from "@angular/router";

/** Keeps drafts in memory and protects navigation from inline management forms. */
export class InlineFormNavigation {
  private initial = "";
  private returnFocus: HTMLElement | null = null;
  private focusTimer: ReturnType<typeof setTimeout> | undefined;

  constructor(
    private readonly isOpen: () => boolean,
    private readonly isSaving: () => boolean,
    private readonly snapshot: () => string,
    private readonly confirmDiscard: () => Promise<boolean>,
  ) {}

  begin(): void {
    this.initial = this.snapshot();
    this.returnFocus =
      document.activeElement instanceof HTMLElement
        ? document.activeElement
        : null;
    clearTimeout(this.focusTimer);
    this.focusTimer = setTimeout(() => {
      document
        .querySelector<HTMLElement>(
          ".inline-editor input, .inline-editor select, .inline-editor button",
        )
        ?.focus({ preventScroll: true });
      window.scrollTo(0, 0);
    });
  }

  private dirty(): boolean {
    return this.isOpen() && this.snapshot() !== this.initial;
  }

  async canLeave(): Promise<boolean> {
    if (this.isSaving()) return false;
    if (!this.dirty()) return true;
    const draft = this.snapshot();
    return (
      (await this.confirmDiscard()) &&
      !this.isSaving() &&
      this.snapshot() === draft
    );
  }

  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.isSaving() || this.dirty()) {
      event.preventDefault();
      event.returnValue = "";
    }
  }

  finish(): void {
    this.initial = "";
    clearTimeout(this.focusTimer);
    this.focusTimer = setTimeout(() => {
      if (this.returnFocus?.isConnected) this.returnFocus.focus();
    });
  }

  destroy(): void {
    clearTimeout(this.focusTimer);
    this.initial = "";
    this.returnFocus = null;
  }
}

export const canLeaveInlineForm: CanDeactivateFn<{
  canLeaveForm(): Promise<boolean>;
}> = (component) => component.canLeaveForm();
