import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  input,
  OnDestroy,
  output,
  ViewChild,
} from "@angular/core";
import {
  DxButtonComponent,
  DxButtonModule,
} from "devextreme-angular/ui/button";
import { ConfirmationRequest } from "./confirmation.service";

/** App-themed modal with an inert background, contained focus and an explicit return target. */
@Component({
  selector: "app-confirmation",
  imports: [DxButtonModule],
  templateUrl: "./app-confirmation.component.html",
  styleUrl: "./app-confirmation.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppConfirmationComponent implements AfterViewInit, OnDestroy {
  readonly request = input.required<ConfirmationRequest>();
  readonly decided = output<boolean>();
  @ViewChild("dialog", { static: true })
  private dialog!: ElementRef<HTMLDialogElement>;
  @ViewChild("cancelButton", { static: true })
  private cancelButton!: DxButtonComponent;
  private returnFocus: HTMLElement | null = null;

  ngAfterViewInit(): void {
    const active = this.dialog.nativeElement.ownerDocument.activeElement;
    this.returnFocus = active instanceof HTMLElement ? active : null;
    this.dialog.nativeElement.showModal();
    this.cancelButton.instance.focus();
  }

  protected cancel(event: Event): void {
    event.preventDefault();
    this.decided.emit(false);
  }

  protected backdrop(event: MouseEvent): void {
    if (event.target !== this.dialog.nativeElement) return;
    event.preventDefault();
    event.stopPropagation();
    this.decided.emit(false);
  }

  protected containFocus(event: KeyboardEvent): void {
    if (event.key !== "Tab" || event.ctrlKey || event.altKey || event.metaKey)
      return;
    const dialog = this.dialog.nativeElement;
    const buttons = dialog.querySelectorAll<HTMLElement>(
      '[role="button"][tabindex="0"]',
    );
    const first = buttons[0];
    const last = buttons[buttons.length - 1];
    const active = dialog.ownerDocument.activeElement;
    // Native dialog makes the page inert; explicitly wrap the two actions so Tab stays here
    // rather than advancing to the browser toolbar at either boundary.
    if (event.shiftKey && active === first) {
      event.preventDefault();
      last?.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first?.focus();
    }
  }

  ngOnDestroy(): void {
    if (this.dialog.nativeElement.open) this.dialog.nativeElement.close();
    // Angular destroys the child buttons before this hook, so native close() alone can lose
    // the previously focused element. Restore it only if the original page still exists.
    if (this.returnFocus?.isConnected)
      this.returnFocus.focus({ preventScroll: true });
  }
}
