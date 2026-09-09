export const unitTypes = [
  "Organization",
  "Country",
  "Region",
  "State",
  "Branch",
  "Location",
  "Department",
  "Team",
] as const;
export type UnitType = (typeof unitTypes)[number];
export const unitLabels: Record<UnitType, string> = {
  Organization: "Organizations",
  Country: "Countries",
  Region: "Regions",
  State: "States",
  Branch: "Branches",
  Location: "Locations",
  Department: "Departments",
  Team: "Teams",
};
export const childTypes: Record<UnitType, readonly UnitType[]> = {
  Organization: ["Country", "Department"],
  Country: ["Region"],
  Region: ["State"],
  State: ["Branch"],
  Branch: ["Location"],
  Location: [],
  Department: ["Team"],
  Team: [],
};
export interface OrganizationAddress {
  readonly addressLine1: string | null;
  readonly addressLine2: string | null;
  readonly addressLine3: string | null;
  readonly city: string | null;
  readonly district: string | null;
  readonly stateName: string | null;
  readonly postalCode: string | null;
  readonly countryCode: string | null;
  readonly latitude: number | null;
  readonly longitude: number | null;
}
export interface OrganizationUnit {
  readonly organizationId: number;
  readonly organizationUnitId: number;
  readonly parentOrganizationUnitId: number | null;
  readonly unitType: UnitType;
  readonly unitCode: string;
  readonly unitName: string;
  readonly description: string | null;
  readonly address: OrganizationAddress;
  readonly hierarchyPath: string;
  readonly isActive: boolean;
  readonly createdAt: string;
  readonly updatedAt: string | null;
  readonly rowVersion: string;
}
export interface OrganizationPage {
  readonly skip: number;
  readonly take: number;
  readonly totalCount: number;
  readonly items: readonly OrganizationUnit[];
}
export interface OrganizationSearch {
  readonly organizationId?: number;
  readonly parentOrganizationUnitId?: number;
  readonly unitType?: UnitType;
  readonly isActive?: boolean;
  readonly search?: string;
  readonly skip?: number;
  readonly take?: number;
}

export interface OrganizationFields {
  readonly unitCode: string;
  readonly unitName: string;
  readonly description: string | null;
  readonly address: OrganizationAddress;
}
export interface CreateOrganizationChild extends OrganizationFields {
  readonly unitType: UnitType;
  readonly parentOrganizationUnitId: number;
}
export interface UpdateOrganizationFields extends OrganizationFields {
  readonly rowVersion: string;
}
export interface OrganizationCreated {
  readonly organizationId: number;
  readonly organizationUnitId: number;
  readonly unitType: UnitType;
  readonly hierarchyPath: string;
  readonly rowVersion: string;
}
export const addressFields = [
  { key: "addressLine1", label: "Address line 1", max: 250 },
  { key: "addressLine2", label: "Address line 2", max: 250 },
  { key: "addressLine3", label: "Address line 3", max: 250 },
  { key: "city", label: "City", max: 100 },
  { key: "district", label: "District", max: 100 },
  { key: "stateName", label: "State / province", max: 100 },
  { key: "postalCode", label: "Postal code", max: 20 },
  { key: "countryCode", label: "Physical country code", max: 3 },
] as const;
export type AddressTextKey = (typeof addressFields)[number]["key"];
export interface OrganizationDraft {
  unitCode: string;
  unitName: string;
  description: string;
  address: Record<AddressTextKey, string>;
  latitude: string;
  longitude: string;
}
export function organizationDraft(unit?: OrganizationUnit): OrganizationDraft {
  return {
    unitCode: unit?.unitCode ?? "",
    unitName: unit?.unitName ?? "",
    description: unit?.description ?? "",
    address: {
      addressLine1: unit?.address.addressLine1 ?? "",
      addressLine2: unit?.address.addressLine2 ?? "",
      addressLine3: unit?.address.addressLine3 ?? "",
      city: unit?.address.city ?? "",
      district: unit?.address.district ?? "",
      stateName: unit?.address.stateName ?? "",
      postalCode: unit?.address.postalCode ?? "",
      countryCode: unit?.address.countryCode ?? "",
    },
    latitude: unit?.address.latitude?.toString() ?? "",
    longitude: unit?.address.longitude?.toString() ?? "",
  };
}
export function organizationFields(
  draft: OrganizationDraft,
): OrganizationFields {
  const unitCode = draft.unitCode.trim(),
    unitName = draft.unitName.trim();
  if (!unitCode || unitCode.length > 50 || !unitName || unitName.length > 200)
    throw new Error(
      "Code (1–50 characters) and name (1–200 characters) are required.",
    );
  if (draft.description.trim().length > 500)
    throw new Error("Description must be at most 500 characters.");
  for (const field of addressFields)
    if (draft.address[field.key].trim().length > field.max)
      throw new Error(
        `${field.label} must be at most ${field.max} characters.`,
      );
  const coordinate = (
    text: string,
    max: number,
    label: string,
  ): number | null => {
    if (!text.trim()) return null;
    const value = Number(text);
    if (!Number.isFinite(value) || value < -max || value > max)
      throw new Error(`${label} must be between ${-max} and ${max}.`);
    if (Math.abs(value * 1e6 - Math.round(value * 1e6)) > 0.000001)
      throw new Error(`${label} supports at most six decimal places.`);
    return value;
  };
  const optional = (value: string) => value.trim() || null;
  return {
    unitCode,
    unitName,
    description: optional(draft.description),
    address: {
      addressLine1: optional(draft.address.addressLine1),
      addressLine2: optional(draft.address.addressLine2),
      addressLine3: optional(draft.address.addressLine3),
      city: optional(draft.address.city),
      district: optional(draft.address.district),
      stateName: optional(draft.address.stateName),
      postalCode: optional(draft.address.postalCode),
      countryCode: optional(draft.address.countryCode),
      latitude: coordinate(draft.latitude, 90, "Latitude"),
      longitude: coordinate(draft.longitude, 180, "Longitude"),
    },
  };
}
export function organizationError(failure: unknown): string {
  const problem = failure as {
    status?: number;
    error?: {
      title?: string;
      detail?: string;
      errors?: Record<string, string[]>;
    };
    message?: string;
  };
  if (problem.status === 0)
    return "The API could not be reached. Your draft is kept. Before retrying creation, check whether it already completed.";
  const fields = problem.error?.errors
    ? Object.values(problem.error.errors).flat().join(" ")
    : null;
  return (
    fields ||
    problem.error?.detail ||
    problem.error?.title ||
    problem.message ||
    "The organization change could not be saved."
  );
}

/** A unit placed in the drill-down, with the child state the row's toggle needs. */
export interface OrganizationRow {
  readonly unit: OrganizationUnit;
  readonly depth: number;
  readonly expandable: boolean;
  readonly expanded: boolean;
  readonly loading: boolean;
  /** Children beyond the page loaded inline; the administrator drills in to see the rest. */
  readonly hiddenChildren: number;
}
