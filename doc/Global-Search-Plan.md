# IAM global search implementation plan

Status: proposed; the record-search expansion is not implemented by this UI fix.

## Intended behavior

One search field should find IAM menus, users, organization units, and related administration records across all pages the signed-in administrator may access. Typing `ana` should return matching people such as Anand, rather than only the Users & access menu. Search must query the database, not just the records currently loaded in a grid.

Keep Ctrl/Cmd+K, arrow-key selection, Enter to open, and Escape to close. Rename the placeholder to **Search users, organizations and more…** and group results by record type. On focus with no query, show menu shortcuts; after two characters, search records with a 250 ms debounce and cancel superseded requests.

## Search coverage

| Group                  | Match against                                                                                             | Result context and destination                                                      |
| ---------------------- | --------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| Users                  | Display name, employee code, email                                                                        | Employee code, department, team, active/inactive status; open `/users/:userId`      |
| Organization units     | Unit name, code, description; organization, country, region, state, branch, location, department and team | Unit type and ancestor path; open the selected unit in the organization detail view |
| Applications           | Application name and code                                                                                 | Application status; open `/applications/:applicationId`                             |
| Related access records | Role names/codes, module names/codes, capability names/codes and client names/identifiers                 | Owning application and record type; open the relevant application detail section    |
| Machines and agents    | Hostname, machine identifier, installation identifier                                                     | Status and machine context; open `/agents/:installationId`                          |
| Menus                  | Existing labels and navigation keywords                                                                   | Screen name and section; existing navigation route                                  |

Related data should also be discoverable through relationships: searching a department/team can expose a **View users in this unit** action; user details can lead to application access and security controls. This requires explicit directory filters, including a choice between direct members and descendant units. Do not silently treat ancestor membership as direct membership.

Limit this expansion to IAM administration data. PTS production transactions and unrestricted audit/session payloads are outside the proposed scope. Security navigation can be included, but passwords, PINs, hashes, tokens, device fingerprints, MFA secrets and recovery codes must never be queried or returned by global search.

## Existing implementation to reuse

- `src/Frontend/src/app/shared/shell/shell.component.ts` currently searches a static navigation array only.
- `AdministrationEndpoints` already protects administration reads with `AdministrationPolicy.Name` and exposes paged user/application searches.
- `AdministrationDiscoveryStore.SearchUsers` already matches display name, employee code and email; its summaries include department/team context.
- `OrganizationStore.Search` already supports organization/type/parent/status filters and matches unit code, name and description.
- User, application and agent detail routes already have stable IDs. Organization viewing currently uses component selection state; its existing child URLs are editor routes, so global search must not use an edit URL as a read-only destination.

## Delivery sequence

1. **Define the contract and access rules.** Add a read-only `GET /identity/api/v1/admin/search?q=...&types=...&takePerGroup=5` endpoint. Return grouped typed summaries with IDs, label, secondary text, status, owning organization/application IDs and `hasMore`. Reuse existing administration authorization, and enforce any applicable organization/application restrictions before ranking, counting or limiting results. The frontend constructs routes from a fixed mapping of result types rather than accepting arbitrary URLs.

2. **Implement user, organization-unit and application queries first.** Add a dedicated application query/handler and search-store abstraction. Use parameterized, read-only queries with cancellation, bounded query length (for example 2–100 characters), and a server-enforced per-group cap. Use SQL-side filtering and projections; do not load complete directories into the browser or issue detail requests per result. Avoid concurrent operations on a shared EF DbContext.

3. **Add stable read-only destinations.** Support organization/unit selection through URL state and restore its ancestor context on reload. Support direct links to application detail sections for related records. Preserve directory search/page state when returning. Add a security user ID parameter if the search offers an explicit “Open security” action.

4. **Replace menu-only results with grouped results.** Extract a global-search service/component from the shell. Show up to five results per group, matching text, status and contextual subtitles. Prioritize exact employee/unit codes and exact names, then prefixes, then substrings, with stable ordering for ties. Keep menu matching immediate. Show separate loading, no-match, error and retry states. Use request cancellation plus a latest-query guard so slow responses never replace newer results. Maintain valid combobox/listbox semantics and one keyboard selection across groups, including when asynchronous groups change.

5. **Add a full results grid.** “View all results” opens `/search?q=...` with category filters, server-side paging and record-type/context columns. Group “View all” links open that same route with a type filter. Display exact counts only if measured query cost is acceptable; otherwise use `hasMore`. Include active and inactive records with clear labels and an optional status filter.

6. **Extend related-record coverage.** Add roles, modules, capabilities, clients and machines/agents using the same summary contract. Add organization-member drill-down after its filter semantics and authorization are covered. Do not fetch full user security catalogs for autocomplete.

7. **Verify and release.** Test authorization and cross-organization boundaries, empty/short queries, repeated names, exact codes, long input, paging beyond 50 records, inactive records, stale responses, keyboard navigation, direct links, mobile layouts, and light/dark themes. Measure query plans with representative data before choosing indexes or SQL full-text search. Deploy only through the separately authorized environment-specific publish process.

## Acceptance examples

- `ana` finds matching user records regardless of the current screen or directory page.
- An employee code opens that exact user's details; duplicate names show distinguishing codes.
- `Business Support` finds the team with its organization and department path.
- Department/team results can lead to the correctly scoped user directory without exposing users outside the administrator's permissions.
- Application/role/module matches show their owner so similarly named records are distinguishable.
- Records beyond the first 50 are reachable through full results paging.
- Clearing a query removes record results; unavailable search still leaves authorized menu navigation usable.
- Every result opens a read-only detail view; no search selection changes data or executes a security action.
- No sensitive security material appears in responses, telemetry, browser storage or screenshots. Avoid logging raw search text because it may contain employee identifiers or email addresses.

## Suggested first milestone

Deliver users + every organization-unit type + applications, grouped autocomplete, read-only deep links, and the paged full-results grid together. Follow with related access records and machines/agents once their destination sections are ready. No database search infrastructure change is assumed until measurements justify it.
