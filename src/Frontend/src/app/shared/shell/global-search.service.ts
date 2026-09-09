import { Injectable, inject } from "@angular/core";
import { AgentInventoryService } from "../../features/agent-inventory/agent-inventory.service";
import { OrganizationService } from "../../features/organization-management/organization.service";
import { UserAccessService } from "../../features/user-access/user-access.service";

export type GlobalSearchGroup =
  "Screens" | "Users" | "Applications" | "Organizations" | "Machines";

export interface GlobalSearchHit {
  readonly group: GlobalSearchGroup;
  readonly id: string;
  readonly label: string;
  readonly detail: string;
  readonly route: string;
  readonly icon: string;
}

/** Records per source; the drop-down is a jump list, not a report. */
const LIMIT = 5;

/**
 * Backs the top-bar search with the records themselves, not just the menu list. Each source is
 * queried in parallel and a failing source is dropped rather than emptying the whole result, so a
 * single unhealthy endpoint cannot make the search look broken.
 */
@Injectable({ providedIn: "root" })
export class GlobalSearchService {
  private readonly users = inject(UserAccessService);
  private readonly organizations = inject(OrganizationService);
  private readonly agents = inject(AgentInventoryService);

  async search(term: string): Promise<readonly GlobalSearchHit[]> {
    const query = term.trim();
    if (query.length < 2) return [];

    const [users, applications, units, machines] = await Promise.all([
      settle(this.users.searchUsers(query)),
      settle(this.users.searchApplications(query)),
      settle(this.organizations.search({ search: query, take: LIMIT })),
      settle(this.agents.search(query)),
    ]);

    const hits: GlobalSearchHit[] = [];

    for (const user of (users?.items ?? []).slice(0, LIMIT))
      hits.push({
        group: "Users",
        id: `user-${user.userId}`,
        label: user.displayName,
        detail: [user.employeeCode, user.email].filter(Boolean).join(" · "),
        route: `/users/${user.userId}`,
        icon: "user",
      });

    for (const application of (applications?.items ?? []).slice(0, LIMIT))
      hits.push({
        group: "Applications",
        id: `application-${application.applicationId}`,
        label: application.applicationName,
        detail: application.applicationCode,
        route: `/applications/${application.applicationId}`,
        icon: "box",
      });

    for (const unit of (units?.items ?? []).slice(0, LIMIT))
      hits.push({
        group: "Organizations",
        id: `unit-${unit.organizationUnitId}`,
        label: unit.unitName,
        detail: `${unit.unitType} · ${unit.unitCode}`,
        route: "/organizations",
        icon: "hierarchy",
      });

    for (const machine of (machines?.items ?? []).slice(0, LIMIT))
      hits.push({
        group: "Machines",
        id: `machine-${machine.installationId}`,
        label: machine.hostname,
        detail: machine.currentUserName ?? machine.agentVersion ?? "Enrolled",
        route: `/agents/${machine.installationId}`,
        icon: "desktop",
      });

    return hits;
  }
}

function settle<T>(promise: Promise<T>): Promise<T | null> {
  return promise.catch(() => null);
}
