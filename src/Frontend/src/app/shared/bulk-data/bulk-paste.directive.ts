import { Directive, HostListener, inject, input } from "@angular/core";
import { Router } from "@angular/router";
import { BulkColumn } from "./bulk-data.models";
import { BulkDataService } from "./bulk-data.service";
import { isMultiCell, parseClipboard } from "./clipboard-parser";
import { looksLikeHeaderRow } from "./column-matcher";

const maxPastedRows = 5000;

/**
 * Captures a multi-cell paste on a list surface and routes to the mapping screen.
 *
 * Deliberately narrow: a paste is only intercepted when the target is not an editor and the payload
 * spans more than one cell. Anything else is an ordinary paste into a field and is left alone.
 */
@Directive({
  selector: "[appBulkPaste]",
  standalone: true,
})
export class BulkPasteDirective {
  readonly appBulkPaste = input.required<string>();
  readonly pasteColumns = input.required<readonly BulkColumn[]>();

  private readonly service = inject(BulkDataService);
  private readonly router = inject(Router);

  /* Document level rather than host level: the grid is not focused on arrival, so a host
     listener would require a click first. The guards below keep an editor paste untouched. */
  @HostListener("document:paste", ["$event"])
  protected onPaste(event: ClipboardEvent): void {
    if (isEditing(event.target)) return;

    const parsed = parseClipboard(event.clipboardData, maxPastedRows);
    if (!isMultiCell(parsed) || !parsed) return;

    event.preventDefault();
    const columns = this.pasteColumns();
    this.service.beginPaste(
      this.appBulkPaste(),
      columns,
      parsed.rows,
      looksLikeHeaderRow(parsed.rows[0], columns),
      parsed.truncated,
    );
    void this.router.navigate(["/bulk", "map"]);
  }
}

/** A paste into a text field, a textarea or anything contenteditable belongs to that field. */
function isEditing(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  if (target.isContentEditable) return true;
  const tag = target.tagName;
  return tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT";
}
