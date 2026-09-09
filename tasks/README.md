# IAM Development Task Sheet

## IAM.Agent (2026-08-31)

- User scope: Windows agent owned by IAM, hostname/device identity consumed by PTS, authenticated hardware/software reports visible in IAM, downloadable setup, automatic startup and signed OTA/rollback. See `doc/IAM-Agent.md`.
- Preserve concurrent unrelated IAM module-reordering and PTS workspace edits; commit only agent-owned paths.
- Validation: agent/security tests, IAM API/domain/persistence checks, both frontend suites/builds, package/startup/update/browser acceptance. Live trust and installation require administrator setup, never bypassed.

| ID | Task | Dependencies | Status |
|---|---|---|---|
| A001 | [Local identity and PTS integration](A001-agent-identity.md) | Existing IAM | COMPLETED |
| A002 | [Machine inventory](A002-agent-inventory.md) | A001 | COMPLETED |
| A003 | [Installation and updates](A003-agent-install-update.md) | A002 | COMPLETED |
| A004 | [Acceptance and rollout](A004-agent-acceptance.md) | A003 | COMPLETED |
| A005 | [Server rollout preparation](A005-agent-rollout-preparation.md) | A004 | COMPLETED |
| A006 | [Approved SQL recovery](A006-approved-sql-recovery.md) | A005 | COMPLETED |

- A006 completed the explicitly approved SQL Express restart without forced termination. IAM, DS4 and QS4 readiness are healthy, and the stranded empty agent-test database was removed after identity/content/session checks. Live agent rollout still awaits migration prerequisites 0011/0012, deployment and administrator enrollment; see `doc/IAM-Agent-Rollout.md`.

- IAM.Agent repository implementation and synthetic acceptance are complete. Live server deployment and elevated pilot/fleet commissioning remain separate; see `doc/IAM-Agent.md` for the signed package, validation evidence and the SQL Express temporary-database cleanup caveat. This is not a claim of completed live rollout.

- Project status: T001–T025 implementation complete. T025 applied approved local migrations and INDE03275 provisioning for DS4 acceptance; production rollout remains separate.
- Authoritative scope: the approved 16-table IAM SQL review, the user's separate-application requirement, and the repository free/open-source dependency policy.
- Architecture: independently publishable .NET 10 Clean Architecture solution, separate SQL Server database and DbUp runner, first-party token service, asymmetric JWT/JWKS integration, explicit application capabilities, rotating refresh tokens, TOTP MFA, and append-only audit.
- Validation: format, build, unit/API tests, SQL Server migration replay, publish checks, dependency/security audit, and IAM-to-PTS authorization tests.
- Initial Git baseline: branch `main`, HEAD `46b73fb`; the IAM review SQL was the only pre-existing staged path.

## Ordered tasks

| ID | Task | Dependencies | Status |
|---|---|---|---|
| T001 | Split repository and establish IAM solution | None | COMPLETED |
| T002 | IAM domain and persistence | T001 | COMPLETED |
| T003 | User and application administration | T002 | COMPLETED |
| T004 | Authentication and token service | T002, T003 | COMPLETED |
| T005 | MFA, devices, and security audit | T002-T004 | COMPLETED |
| T006 | Hardened IAM HTTP API | T003-T005 | COMPLETED |
| T007 | PTS integration and independent publishing | T006 | COMPLETED |
| T008 | Shared-terminal PIN verification | T005-T007, PTS T018 | COMPLETED |
| T009 | IAM frontend foundation and browser authentication | T006 | COMPLETED |
| T010 | Administration dashboard and resource discovery API | T009 | COMPLETED |
| T011 | Applications, clients, modules, and capabilities | T010 | COMPLETED |
| T012 | Users, application access, roles, and overrides | T011 | COMPLETED |
| T013 | Credentials, devices, and MFA administration | T012 | COMPLETED |
| T014 | Security audit, sessions, and revocation operations | T013 | COMPLETED |
| T015 | Frontend publishing and end-to-end validation | T009-T014 | COMPLETED |
| T016 | IAM administration login and licensed UI repair | T015 | COMPLETED |
| T017 | Authentication lockout, trusted-device expiry, and TOTP replay hardening | T016 | COMPLETED |
| T018 | User contact email and self-referencing reporting manager | T003, T012, T017 | COMPLETED; LOCAL ROLLOUT VERIFIED IN T025 |
| T019 | Organization hierarchy database, entities, mappings, and field descriptions | T002 | COMPLETED; LOCAL SCHEMA ROLLOUT VERIFIED IN T025 |
| T020 | [Organization management queries and update workflows](T020-organization-management-workflows.md) | T019 | COMPLETED |
| T021 | [Typed organization management HTTP APIs](T021-organization-management-api.md) | T020 | COMPLETED |
| T022 | [Organization navigation and hierarchy browsing](T022-organization-management-browsing.md) | T021 | COMPLETED |
| T023 | [Organization creation editing and state management UI](T023-organization-management-editing.md) | T022 | COMPLETED |
| T024 | [Organization management final review and end-to-end evidence](T024-organization-management-final-validation.md) | T023 | COMPLETED |
| T025 | [Bootstrap/web provisioning compatibility and approved local restoration](T025-bootstrap-web-provisioning-compatibility.md) | T024 | COMPLETED |

## Bounded scope

- `PTS/` and `IAM/` are independent project roots with separate solutions, package management, source, tests, databases, documentation, and task records.
- IAM owns users, applications, clients, modules, capabilities, roles, permission overrides, devices, credentials, MFA, tokens, and authentication audit.
- PTS has no project reference to IAM and validates IAM-issued JWTs through issuer metadata and JWKS.
- T018 adds contact email and a reporting manager; T019 adds normalized organization/geographic/department hierarchy storage. T020–T024 add organization management API/UI. Full employee-directory duplication, hierarchical roles, deployment, and email/SMS delivery providers remain out of scope.
- The new independently deployable management UI is under `src/Frontend`; DevExtreme is the approved commercial UI exception, while all other added dependencies remain free/open-source.
- V1 implements employee-code/password authentication, TOTP MFA, and a narrowly scoped four-digit PIN flow for trusted shared-terminal verification. Email/SMS method storage remains modeled but is not enabled.

## Final evidence

The project is complete only after both solutions build and test cleanly, IAM migrations apply to a blank database and replay safely, IAM publish artifacts build, real IAM-issued tokens pass PTS policies, task commits are verified, and no task-owned changes remain uncommitted.

The original IAM backend tasks are complete. T009-T015 extend that finished service with an independently deployable administration UI without coupling it to a consuming application.


## Organization management completion contract (2026-08-30)

- Source: delegated user scope and Identity-Coding-Standard, T018/T019, Organization-Module-Database, User-Email-And-Manager and T018-T019-Verification; actual code takes precedence over stale scope summaries.
- Initial state: clean detached HEAD `6077f1ac37a0dae5b5b55be04e821f6915a5497d`; work branch `codex/iam-organization-management`. All changes/commits confined to IAM/.
- Implement all eight types, both hierarchy branches, bounded/paged/searchable lists, details, creation, shared descriptive/address editing, active/inactive, authorized typed APIs, validation, audit, cancellation, optimistic concurrency and responsive light/dark UI.
- Preserve atomic creation, canonical fields/paths, organization-wide normalized code uniqueness, profile-target race fix and existing DevExtreme license setup.
- Excluded: moves, hard deletion, user organization assignment, permission inheritance, new identity providers, INDE03275 provisioning, live rollout, push/deploy/publish/merge and reset allowlist changes.
- Never write FIN_IAM, FIN_PTS_DS4 or FIN_PTS_QS4 (including SchemaVersions). SQL uses uniquely named disposable databases, explicit connection strings, Windows integrated authentication, server `lpc:HOCOM18502627\SQLEXPRESS2022`, Encrypt=true, TrustServerCertificate=true; verify exact targets before cleanup.
- Evidence: real repository dotnet build/test/format; npm build/test, tsc and Prettier; Playwright existing + extended suite, actual HTTP/SQL evidence distinguished from fixtures, both themes and mobile screenshots; focused commit verification and clean task-owned tree. Baseline: 80 backend, 23 UI unit and five browser tests.
- No new dependency is planned. Required migration 0010 extends only the SQL audit event allow-list; applied migrations are unchanged. Remaining release boundaries remain explicit even after in-scope completion.

Final evidence: [T020–T024 verification](../doc/T020-T024-Verification.md) records 93 backend tests, 37 UI unit tests, seven fixture browser tests and two actual browser/API/SQL tests passing. All task databases were removed after exact-target verification. DS4's read-only counts differ from the supplied baseline; this task did not write or reset it. The bounded repository scope is complete; live rollout and account provisioning are not authorized.

## PTS template alignment

| ID | Task | Dependencies | Status |
| --- | --- | --- | --- |
| T026 | [Apply the PTS template to IAM](T026-pts-template-alignment.md) | T024, current main | COMPLETED |

The supplied PTS screenshot and existing PTS components are the visual reference. Changes stay within IAM; deployment and business data are excluded.

T026 verification: production build, formatting, all TypeScript checks, 37 UI unit tests and nine fixture browser tests passed. Desktop/mobile light/dark screenshots reviewed. No deployment or business-database changes.

## MINI form styling follow-up (2026-08-30)

User clarification: inputs, number boxes, selects, buttons and grids only; no header/navigation changes. Reference source is read-only. No business database operations.

- [T027: Match MINI form controls and record lists](T027-mini-form-controls.md) — COMPLETED; depends on T026.
- [T028: Verify IAM and PTS form-style parity](T028-mini-style-validation.md) — COMPLETED; depends on IAM T027, PTS T054.

The bounded form/grid styling follow-up is complete: implementation commits `9f2dc43` and `ef3b9ca`, with final validation in T028. Header/navigation, backend, license and live-release boundaries are unchanged.

## Organization form pages (2026-08-30)

- [T029: Organization create and edit pages](T029-organization-editor-pages.md) — COMPLETED; replaces the popup with routed pages, preserving workflows and existing release boundaries.

User scope extension: all IAM management popups become inline pages with Back; account/appearance dropdown and action confirmations remain.
- [T030: Inline management forms](T030-inline-management-forms.md) — COMPLETED; depends on T029.
- [T031: Inline pages final validation](T031-inline-pages-final-validation.md) — COMPLETED; depends on T029/T030.

The bounded inline-page scope is complete. Implementation commits `eeec61a` and `159446e` are verified. Final post-commit checks: 94 backend tests (including real SQL), 41 UI unit tests, 11 fixture browser tests and two actual API/SQL browser tests passed; build/format/type checks passed. All disposable databases were removed. See [inline-page verification](../doc/T029-T031-Inline-Pages-Verification.md) for commands, requirement mapping and release boundaries.

## Production revamp: design system, smart paste and Excel (2026-08-31)

User scope: the current screens are not production-grade. Rebuild the administration UI on an owned design system, give every management page smart paste and Excel template based export and import, and handle all Excel work through the DevExpress Office File API.

Decisions taken with the user before planning:

- Rebuild depth: new design system plus a full rewrite of every screen template and component. Existing `*.service.ts`, `*.models.ts`, guards, interceptors and API contracts are retained, because they pass T009-T031 tests and re-deriving them adds risk without benefit.
- Import semantics: stage, preview, then commit. An upload or paste is validated into a staging batch and nothing reaches identity tables until the administrator confirms; failures are correctable inline or downloadable as an annotated workbook.
- Delivery: task specs first, then phased execution with a build and test gate and a focused commit per task.

| ID | Task | Dependencies | Status |
| --- | --- | --- | --- |
| T032 | [Production design system foundation](T032-production-design-system.md) | T031 | COMPLETED |
| T033 | [Excel document service on DevExpress Office File API](T033-excel-document-service.md) | T031 | COMPLETED |
| T034 | [Bulk staging persistence and validation pipeline](T034-bulk-staging-pipeline.md) | T033 | COMPLETED |
| T035 | [Bulk data HTTP API](T035-bulk-data-api.md) | T034 | COMPLETED |
| T036 | [Smart paste engine and shared bulk data workspace](T036-smart-paste-bulk-workspace.md) | T032, T035 | COMPLETED |
| T037 | [Shell, navigation and dashboard rebuild](T037-shell-dashboard-rebuild.md) | T032 | COMPLETED |
| T038 | [Users bulk capability](T038-users-screen-rebuild.md) | T036, T037 | COMPLETED |
| T039 | [Catalog bulk capability](T039-applications-screen-rebuild.md) | T036, T037 | COMPLETED |
| T040 | [Organization bulk capability](T040-organizations-screen-rebuild.md) | T036, T037 | COMPLETED |
| T041 | [Assurance export-only guarantee and sign-in](T041-security-audit-signin-rebuild.md) | T036, T037 | COMPLETED |
| T042 | [Revamp final validation, documentation and dependency exception](T042-revamp-final-validation.md) | T032-T041, T043 | COMPLETED |
| T043 | [Screen visual rebuild on the design system](T043-users-screen-visual-rebuild.md) | T038-T041 | COMPLETED |

T032/T033 and T037 have no dependency on each other and may run in parallel. T038-T041 are independent of one another once T036 and T037 land.

T038-T041 delivered every bulk capability end to end, and T041 made the audit and session export-only rule structural. T043 then rebuilt all five management screens on the design system. What remains for T042 is cross-screen evidence at four widths in both themes, the operational gaps (staged-batch expiry sweep, DevExpress package source), and the documentation and dependency-exception record. Two feature remainders are named explicitly in T043 rather than closed: the user-applications and user-roles descriptors, and inline row correction in the bulk preview.

### Bounded scope

- Changes stay inside `IAM/`. `PTS/` is untouched.
- Retained unchanged: authentication, token issuance, MFA, devices, session and rate-limit behavior, the eight organization types and their allowed parents, optimistic concurrency, and all existing audit events.
- Smart paste and Excel import apply to users, user application grants, user roles, applications, modules, capabilities, roles and organization units. Security controls and audit are export-only by design.
- Never bulk-writable and never present in a template, export or staged row: passwords, PINs, OTPs, client secrets, refresh tokens, MFA enrolment, device trust and session revocation.
- Migration `0011` is the only new schema. Migrations `0001`-`0010` are applied and are not edited.
- Excluded: organization unit moves, hard deletion, new identity providers, live rollout, real-account provisioning, deployment, push and merge.

### Known gap to close in T042

`DevExpress.Document.Processor 26.1.3` resolves only from the local NuGet cache; no DevExpress package source is registered on this machine. A clean or CI restore will fail until a source is added or the package is vendored. It is also a licensed commercial redistributable, which conflicts with the free/open-source production dependency rule in `doc/Identity-Coding-Standard.md`; it was added on explicit user direction and T042 records it as a second approved exception beside DevExtreme.

### Follow-up

| ID | Task | Dependencies | Status |
| --- | --- | --- | --- |
| T044 | [Access grant descriptors and inline row correction](T044-access-grant-descriptors-and-inline-correction.md) | T038, T042 | COMPLETED |
| T045 | [Presentation refinements and verification gaps](T045-presentation-refinements.md) | T043, T044 | COMPLETED |

T044 closed the two feature remainders from T043 that carried real user value: `user-applications`
and `user-roles` bulk descriptors, and an editable preview cell that calls the correction endpoint.

T045 closed the presentation remainders and the verification gaps: tabbed user detail, a module
hierarchy with reorder controls and system-module protection, a lazy organization drill-down with a
browsing path, a concurrency prompt that names what changed, and a paged audit trail with copyable
correlation ids. Module reorder needed backend work the task had not anticipated - a module's
position was settable only at creation. It also repaired the offline browser suite, which had not
been run since the design-system rebuild and was asserting on markup that no longer exists; all 15
browser tests pass.

### Revamp outcome

The bounded revamp scope is complete. Final evidence is recorded in
[T032-T043 verification](../doc/T032-T043-Revamp-Verification.md): 200 backend tests and 107 frontend
tests passing with none skipped, migration `0011` applying and replaying on a pristine disposable
database, all four publish outputs building, and real API and SQL evidence for every bulk entity.

One release boundary remains open and is not closed by this work: a clean or CI restore still fails
because no DevExpress package source is registered, which is a procurement and deployment action.

The feature and presentation remainders named in T043 were delivered afterwards in T044 and T045, so
the suites have since grown to 211 backend and 134 frontend tests.
