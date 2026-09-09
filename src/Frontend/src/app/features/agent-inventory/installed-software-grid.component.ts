import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  viewChild,
} from "@angular/core";
import {
  DxDataGridComponent,
  DxDataGridModule,
  DxButtonModule,
} from "devextreme-angular";
import type { Column } from "devextreme/ui/data_grid";
import { AppDataGridComponent } from "../../shared/ui/design-system/app-data-grid.component";
import type { InstalledSoftware } from "./agent-inventory.service";

export function softwareInstalledDate(
  value: string | null | undefined,
): Date | null {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return null;
  const [year, month, day] = value.split("-").map(Number);
  const date = new Date(0);
  date.setFullYear(year, month - 1, day);
  date.setHours(0, 0, 0, 0);
  return year > 0 &&
    date.getFullYear() === year &&
    date.getMonth() === month - 1 &&
    date.getDate() === day
    ? date
    : null;
}

export function softwareGridRows(software: readonly InstalledSoftware[]) {
  return software.map((app, rowId) => ({
    ...app,
    rowId,
    version: app.version || null,
    publisher: app.publisher || null,
    installedOn: softwareInstalledDate(app.installedOn),
    estimatedSizeMiB:
      app.estimatedSizeBytes != null &&
      Number.isFinite(app.estimatedSizeBytes) &&
      app.estimatedSizeBytes >= 0
        ? app.estimatedSizeBytes / 1048576
        : null,
    registryView: app.registryView ?? null,
  }));
}

const reportedText: NonNullable<Column["customizeText"]> = ({
  value,
  valueText,
}) =>
  value == null || value === "" ? "Not reported" : (valueText ?? String(value));

@Component({
  selector: "app-installed-software-grid",
  imports: [DxButtonModule, AppDataGridComponent],
  templateUrl: "./installed-software-grid.component.html",
  styleUrl: "./installed-software-grid.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InstalledSoftwareGridComponent {
  readonly software = input.required<readonly InstalledSoftware[]>();
  readonly rows = computed(() => softwareGridRows(this.software()));
  private readonly grid = viewChild(AppDataGridComponent);
  readonly columns: Column[] = [
    {
      dataField: "name",
      caption: "Application",
      minWidth: 220,
      sortOrder: "asc",
    },
    {
      dataField: "version",
      caption: "Version",
      minWidth: 120,
      customizeText: reportedText,
    },
    {
      dataField: "publisher",
      caption: "Publisher",
      minWidth: 170,
      customizeText: reportedText,
    },
    {
      dataField: "installedOn",
      caption: "Installed / serviced date",
      dataType: "date",
      format: "dd MMM yyyy",
      minWidth: 190,
      customizeText: reportedText,
    },
    {
      dataField: "estimatedSizeMiB",
      caption: "Estimated size (MiB)",
      dataType: "number",
      format: "#,##0.##",
      minWidth: 175,
      customizeText: reportedText,
    },
    {
      dataField: "registryView",
      caption: "Registry view",
      minWidth: 130,
      customizeText: reportedText,
    },
  ];

  clearFilters(): void {
    this.grid()?.clearFilters();
  }
}
