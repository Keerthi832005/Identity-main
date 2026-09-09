import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnInit,
  Output,
  inject,
  signal,
} from "@angular/core";
import { OrganizationService } from "../organization-management/organization.service";
import { OrganizationUnit } from "../organization-management/organization.models";
import {
  AppLookupComponent,
  LookupSearch,
} from "../../shared/ui/form-controls/app-lookup.component";
import { UserOrganizationMapping } from "./user-access.models";

@Component({
  selector: "app-user-organization-mapping",
  imports: [AppLookupComponent],
  templateUrl: "./user-organization-mapping.component.html",
  styles: [
    `
      :host {
        display: block;
        grid-column: 1 / -1;
      }
      .mapping-fields {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 1rem;
      }
      label {
        display: grid;
        gap: 0.5rem;
        min-width: 0;
      }
      label > span {
        font-weight: 600;
      }
      .derived-value {
        min-height: 2.75rem;
        padding: 0.75rem;
        border: 1px solid var(--app-border);
        border-radius: 0.35rem;
        background: var(--app-surface-muted);
        color: var(--app-text-secondary);
      }
      p {
        margin: 0.75rem 0 0;
        color: var(--app-text-secondary);
      }
      @media (max-width: 640px) {
        .mapping-fields {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserOrganizationMappingComponent implements OnInit {
  private readonly service = inject(OrganizationService);
  @Input() initialMapping: UserOrganizationMapping = {
    departmentId: null,
    teamId: null,
    branchId: null,
  };
  @Input() departmentName: string | null = null;
  @Input() teamName: string | null = null;
  @Input() branchName: string | null = null;
  @Input() stateId: number | null = null;
  @Input() stateName: string | null = null;
  @Input() regionId: number | null = null;
  @Input() regionName: string | null = null;
  @Input() countryId: number | null = null;
  @Input() countryName: string | null = null;
  @Input() disabled = false;
  @Output() readonly mappingChange =
    new EventEmitter<UserOrganizationMapping>();
  protected readonly departmentId = signal<number | null>(null);
  protected readonly teamId = signal<number | null>(null);
  protected readonly selectedDepartmentName = signal<string | null>(null);
  protected readonly selectedTeamName = signal<string | null>(null);
  protected readonly selectedCountryId = signal<number | null>(null);
  protected readonly selectedRegionId = signal<number | null>(null);
  protected readonly selectedStateId = signal<number | null>(null);
  protected readonly branchId = signal<number | null>(null);
  protected readonly selectedCountryName = signal<string | null>(null);
  protected readonly selectedRegionName = signal<string | null>(null);
  protected readonly selectedStateName = signal<string | null>(null);
  protected readonly selectedBranchName = signal<string | null>(null);
  private departments: readonly OrganizationUnit[] = [];
  private teams: readonly OrganizationUnit[] = [];
  private branches: readonly OrganizationUnit[] = [];

  protected readonly searchBranches: LookupSearch = async (search) => {
    const result = await this.service.search({
      unitType: "Branch",
      isActive: true,
      search,
      take: 50,
    });
    this.branches = result.items;
    return this.options(result.items);
  };

  protected readonly searchDepartments: LookupSearch = async (search) => {
    const result = await this.service.search({
      unitType: "Department",
      isActive: true,
      search,
      take: 50,
    });
    this.departments = result.items;
    return result.items.map((unit) => ({
      value: unit.organizationUnitId,
      label: `${unit.unitName} (${unit.unitCode})`,
    }));
  };
  protected readonly searchTeams: LookupSearch = async (search) => {
    const departmentId = this.departmentId();
    if (departmentId === null) return [];
    const result = await this.service.search({
      unitType: "Team",
      isActive: true,
      parentOrganizationUnitId: departmentId,
      search,
      take: 50,
    });
    if (departmentId !== this.departmentId()) return [];
    this.teams = result.items;
    return result.items.map((unit) => ({
      value: unit.organizationUnitId,
      label: `${unit.unitName} (${unit.unitCode})`,
    }));
  };
  ngOnInit(): void {
    this.departmentId.set(this.initialMapping.departmentId);
    this.teamId.set(this.initialMapping.teamId);
    this.selectedDepartmentName.set(this.departmentName);
    this.selectedTeamName.set(this.teamName);
    this.selectedCountryId.set(this.countryId);
    this.selectedRegionId.set(this.regionId);
    this.selectedStateId.set(this.stateId);
    this.branchId.set(this.initialMapping.branchId ?? null);
    this.selectedCountryName.set(this.countryName);
    this.selectedRegionName.set(this.regionName);
    this.selectedStateName.set(this.stateName);
    this.selectedBranchName.set(this.branchName);
  }
  protected chooseDepartment(id: number | null): void {
    if (this.disabled || id === this.departmentId()) return;
    this.departmentId.set(id);
    this.selectedDepartmentName.set(
      this.departments.find((x) => x.organizationUnitId === id)?.unitName ??
        null,
    );
    this.teamId.set(null);
    this.selectedTeamName.set(null);
    this.teams = [];
    this.emitMapping();
  }
  protected chooseTeam(id: number | null): void {
    if (this.disabled || this.departmentId() === null) return;
    this.teamId.set(id);
    this.selectedTeamName.set(
      this.teams.find((x) => x.organizationUnitId === id)?.unitName ?? null,
    );
    this.emitMapping();
  }
  protected async chooseBranch(id: number | null): Promise<void> {
    if (this.disabled || id === this.branchId()) return;
    this.branchId.set(id);
    this.selectedBranchName.set(this.nameOf(this.branches, id));
    this.clearDerivedGeography();
    this.emitMapping();
    if (id === null) return;

    const branch = this.branches.find((unit) => unit.organizationUnitId === id);
    if (!branch?.parentOrganizationUnitId) return;

    const state = await this.service.get(
      branch.organizationId,
      branch.parentOrganizationUnitId,
    );
    if (id !== this.branchId() || state.unitType !== "State") return;
    this.selectedStateId.set(state.organizationUnitId);
    this.selectedStateName.set(state.unitName);
    if (!state.parentOrganizationUnitId) return;

    const region = await this.service.get(
      branch.organizationId,
      state.parentOrganizationUnitId,
    );
    if (id !== this.branchId() || region.unitType !== "Region") return;
    this.selectedRegionId.set(region.organizationUnitId);
    this.selectedRegionName.set(region.unitName);
    if (!region.parentOrganizationUnitId) return;

    const country = await this.service.get(
      branch.organizationId,
      region.parentOrganizationUnitId,
    );
    if (id !== this.branchId() || country.unitType !== "Country") return;
    this.selectedCountryId.set(country.organizationUnitId);
    this.selectedCountryName.set(country.unitName);
  }
  private emitMapping(): void {
    this.mappingChange.emit({
      departmentId: this.departmentId(),
      teamId: this.teamId(),
      branchId: this.branchId(),
    });
  }
  private options(units: readonly OrganizationUnit[]) {
    return units.map((unit) => ({
      value: unit.organizationUnitId,
      label: `${unit.unitName} (${unit.unitCode})`,
    }));
  }
  private nameOf(
    units: readonly OrganizationUnit[],
    id: number | null,
  ): string | null {
    return (
      units.find((unit) => unit.organizationUnitId === id)?.unitName ?? null
    );
  }
  private clearDerivedGeography(): void {
    this.selectedStateId.set(null);
    this.selectedStateName.set(null);
    this.selectedRegionId.set(null);
    this.selectedRegionName.set(null);
    this.selectedCountryId.set(null);
    this.selectedCountryName.set(null);
  }
}
