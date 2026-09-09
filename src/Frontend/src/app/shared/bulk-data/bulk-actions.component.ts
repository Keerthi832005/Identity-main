import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from "@angular/core";
import { Router } from "@angular/router";
import { DxButtonModule } from "devextreme-angular";
import { BulkColumn, BulkPasteRow } from "./bulk-data.models";
import { BulkDataService } from "./bulk-data.service";

/**
 * Template, export, import and the paste affordance. Dropped into a screen toolbar so every
 * management page offers the same four actions from one implementation.
 */
@Component({
  selector: "app-bulk-actions",
  standalone: true,
  imports: [DxButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bulk-actions">
      <dx-button
        stylingMode="outlined"
        icon="exportxlsx"
        text="Template"
        hint="Download the empty {{ entityLabel() }} template"
        [disabled]="busy()"
        (onClick)="downloadTemplate()"
      />
      <dx-button
        stylingMode="outlined"
        icon="download"
        text="Export"
        hint="Export every row the current filters match, not only this page"
        [disabled]="busy()"
        (onClick)="exportRows()"
      />
      @if (!exportOnly()) {
        <dx-button
          stylingMode="outlined"
          icon="upload"
          text="Import"
          hint="Upload a completed template"
          [disabled]="busy()"
          (onClick)="chooseFile()"
        />
        <input
          #picker
          type="file"
          class="sr-only"
          accept=".xlsx"
          tabindex="-1"
          aria-hidden="true"
          (change)="uploadChosen($event)"
        />
        <span class="bulk-actions__paste">
          Paste rows with <kbd>Ctrl</kbd> <kbd>V</kbd>
        </span>
      }
      @if (message(); as text) {
        <span class="bulk-actions__message" role="status">{{ text }}</span>
      }
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .bulk-actions {
        align-items: center;
        display: flex;
        flex-wrap: wrap;
        gap: var(--app-space-2);
      }

      .bulk-actions__paste {
        align-items: center;
        border: 1px dashed var(--app-border-strong);
        border-radius: var(--app-editor-radius);
        color: var(--app-text-secondary);
        display: inline-flex;
        font-size: var(--app-font-size-caption);
        gap: 5px;
        height: 32px;
        padding: 0 var(--app-space-3);
      }

      kbd {
        border: 1px solid var(--app-border-strong);
        border-bottom-width: 2px;
        border-radius: 4px;
        font-family: var(--app-font-mono);
        font-size: 0.72rem;
        padding: 1px 5px;
      }

      .bulk-actions__message {
        color: var(--app-text-secondary);
        font-size: var(--app-font-size-caption);
      }
    `,
  ],
})
export class BulkActionsComponent {
  readonly entityKey = input.required<string>();
  readonly entityLabel = input("");
  readonly columns = input.required<readonly BulkColumn[]>();
  /** Every row the active filter matches, across all pages; may page the API to collect them. */
  readonly exportRowsProvider = input<
    () => readonly BulkPasteRow[] | Promise<readonly BulkPasteRow[]>
  >(() => []);
  readonly exportOnly = input(false);
  readonly failed = output<string>();

  private readonly service = inject(BulkDataService);
  private readonly router = inject(Router);
  private readonly picker = viewChild<ElementRef<HTMLInputElement>>("picker");

  protected readonly busy = signal(false);
  protected readonly message = signal("");

  protected async downloadTemplate(): Promise<void> {
    await this.run("Template downloaded.", () =>
      this.service.downloadTemplate(this.entityKey()),
    );
  }

  protected async exportRows(): Promise<void> {
    this.busy.set(true);
    this.message.set("Collecting rows…");
    try {
      const rows = await this.exportRowsProvider()();
      if (rows.length === 0) {
        this.message.set(
          "There is nothing to export with the current filters.",
        );
        return;
      }
      await this.service.export(this.entityKey(), rows);
      this.message.set(`Exported ${rows.length} rows.`);
    } catch (error) {
      this.message.set("");
      this.failed.emit(describe(error));
    } finally {
      this.busy.set(false);
    }
  }

  protected chooseFile(): void {
    this.picker()?.nativeElement.click();
  }

  protected async uploadChosen(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = "";
    if (!file) return;

    await this.run("", async () => {
      const batch = await this.service.stageWorkbook(this.entityKey(), file);
      await this.router.navigate(["/bulk", batch.batchKey]);
    });
  }

  private async run(
    success: string,
    action: () => Promise<void>,
  ): Promise<void> {
    this.busy.set(true);
    this.message.set("");
    try {
      await action();
      this.message.set(success);
    } catch (error) {
      this.message.set("");
      this.failed.emit(describe(error));
    } finally {
      this.busy.set(false);
    }
  }
}

function describe(error: unknown): string {
  const detail = error as { error?: { title?: string }; status?: number };
  return (
    detail?.error?.title ??
    (detail?.status === 0
      ? "The identity service could not be reached."
      : "The request could not be completed.")
  );
}
