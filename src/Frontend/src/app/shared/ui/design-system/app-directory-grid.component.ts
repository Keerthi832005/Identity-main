import {
  Component,
  ChangeDetectionStrategy,
  computed,
  input,
  output,
} from "@angular/core";
import { RouterLink } from "@angular/router";
import { DxButtonModule } from "devextreme-angular/ui/button";
import type { Column } from "devextreme/ui/data_grid";
import { AppDataGridComponent } from "./app-data-grid.component";
import { AppGridCellDirective } from "./app-grid-cell.directive";

/**
 * A server-paged directory listing: name, status and a link into the record. Wraps app-data-grid
 * with the paging and search wiring the catalog screens share, so those screens pass rows and
 * columns and nothing else.
 */
@Component({
  selector: "app-directory-grid",
  standalone: true,
  imports: [
    RouterLink,
    DxButtonModule,
    AppDataGridComponent,
    AppGridCellDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: "./app-directory-grid.component.html",
  styleUrl: "./app-directory-grid.component.scss",
})
export class AppDirectoryGridComponent {
  readonly label = input.required<string>();
  readonly rows = input.required<readonly unknown[]>();
  readonly columns = input.required<Column[]>();
  readonly idField = input.required<string>();
  readonly basePath = input.required<string>();
  readonly total = input(0);
  readonly skip = input(0);
  readonly search = input("");
  readonly busy = input(false);
  readonly last = computed(() =>
    Math.min(this.skip() + this.rows().length, this.total()),
  );
  readonly queryParams = input<Record<string, string | number | null>>({});
  readonly searchInput = output<string>();
  readonly searchChange = output<string>();
  readonly pageChange = output<number>();
}
