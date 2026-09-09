import { TestBed } from "@angular/core/testing";
import { describe, expect, it, vi } from "vitest";
import { AgentInventoryService } from "../../features/agent-inventory/agent-inventory.service";
import { OrganizationService } from "../../features/organization-management/organization.service";
import { UserAccessService } from "../../features/user-access/user-access.service";
import { GlobalSearchService } from "./global-search.service";

function configure(overrides: {
  users?: unknown;
  applications?: unknown;
  units?: unknown;
  machines?: unknown;
}): GlobalSearchService {
  TestBed.configureTestingModule({
    providers: [
      {
        provide: UserAccessService,
        useValue: {
          searchUsers: vi.fn(async () => overrides.users ?? { items: [] }),
          searchApplications: vi.fn(
            async () => overrides.applications ?? { items: [] },
          ),
        },
      },
      {
        provide: OrganizationService,
        useValue: {
          search: vi.fn(async () => overrides.units ?? { items: [] }),
        },
      },
      {
        provide: AgentInventoryService,
        useValue: {
          search: vi.fn(async () => overrides.machines ?? { items: [] }),
        },
      },
    ],
  });
  return TestBed.inject(GlobalSearchService);
}

describe("GlobalSearchService", () => {
  it("returns records from every source, routed to their own screen", async () => {
    const service = configure({
      users: {
        items: [
          {
            userId: 7,
            displayName: "Siddeswaran S",
            employeeCode: "INDE03275",
            email: "sid@example.com",
          },
        ],
      },
      applications: {
        items: [
          {
            applicationId: 2,
            applicationName: "Production Tracking System",
            applicationCode: "production-tracking",
          },
        ],
      },
      units: {
        items: [
          {
            organizationUnitId: 11,
            unitName: "Chennai",
            unitType: "Branch",
            unitCode: "BR-CHENNAI",
          },
        ],
      },
      machines: {
        items: [
          {
            installationId: "abc-123",
            hostname: "FUJITECAPP2",
            currentUserName: "INDE03275",
            agentVersion: "1.2.0",
          },
        ],
      },
    });

    const hits = await service.search("sidd");

    expect(hits.map((hit) => [hit.group, hit.route])).toEqual([
      ["Users", "/users/7"],
      ["Applications", "/applications/2"],
      ["Organizations", "/organizations"],
      ["Machines", "/agents/abc-123"],
    ]);
    expect(hits[0].detail).toBe("INDE03275 · sid@example.com");
  });

  it("does not query on a single character", async () => {
    const service = configure({});

    expect(await service.search("s")).toEqual([]);
    expect(
      TestBed.inject(UserAccessService).searchUsers,
    ).not.toHaveBeenCalled();
  });

  /* One unhealthy endpoint used to be indistinguishable from "nothing matched". */
  it("keeps the results of the sources that answered when one fails", async () => {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: UserAccessService,
          useValue: {
            searchUsers: vi.fn(async () => {
              throw new Error("503");
            }),
            searchApplications: vi.fn(async () => ({
              items: [
                {
                  applicationId: 1,
                  applicationName: "Identity Administration",
                  applicationCode: "iam-administration",
                },
              ],
            })),
          },
        },
        {
          provide: OrganizationService,
          useValue: { search: vi.fn(async () => ({ items: [] })) },
        },
        {
          provide: AgentInventoryService,
          useValue: { search: vi.fn(async () => ({ items: [] })) },
        },
      ],
    });

    const hits = await TestBed.inject(GlobalSearchService).search("identity");

    expect(hits).toHaveLength(1);
    expect(hits[0].group).toBe("Applications");
  });
});
