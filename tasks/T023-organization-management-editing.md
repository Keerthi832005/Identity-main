# T023: Organization creation editing and state management UI

Verified focused commit: `320a2ef`. Final regression evidence: [T020–T024 verification](../doc/T020-T024-Verification.md).

- Status: COMPLETED
- Objective: Organization creation editing and state management UI.
- Scope: Implement root and typed-child creation, common code/name/description/address editing, confirmed active/inactive changes, validation and loading/empty/error states. Snapshot edit targets, preserve immutable relationships, retain drafts on failure and explicitly reload after optimistic-concurrency conflicts.
- Requirements covered: Approved Organization → Country → Region → State → Branch → Location and Organization → Department → Team; canonical storage, immutable relationships, authorized/audited management, responsive existing UI.
- Files/components: Angular organization feature and service/component tests.
- Dependencies: T022.
- Risks/assumptions: Existing schema is sufficient; no new migrations anticipated. Business databases are forbidden test targets. No production deployment or account provisioning.

## Implementation Steps

1. Inspect dependency implementation and preserve existing behavior.
2. Implement the complete bounded scope described above.
3. Add meaningful regression/error-path tests, run applicable checks, review the diff.
4. Record evidence and create/verify a focused commit before continuing.

## Acceptance Criteria

- Every behavior in Scope is implemented and exercised by applicable tests.
- Parent/type/organization and canonical paths cannot be edited; code uniqueness remains organization-wide.
- No unrelated screen/license/PTS changes, parallel stores, hard deletion, EF migrations, or business-database writes.

## Required Tests and Validation

Frontend production build, unit tests, TypeScript and changed-file Prettier checks; focused browser flows.

## Validation Results

- `npm run build` in `IAM/src/Frontend`: passed, existing CommonJS warnings only.
- `npm test -- --watch=false`: 37 passed in 11 files, none skipped (baseline 23).
- `npx tsc --noEmit -p tsconfig.app.json`; `npx tsc --noEmit -p tsconfig.spec.json`; `npx tsc --ignoreConfig --noEmit --strict --target ES2022 --module nodenext --moduleResolution nodenext --types node e2e/organization-management.spec.ts`: passed. TypeScript 6 requires --ignoreConfig when filenames are supplied; corrected the initial invocation accordingly.
- `npx prettier --check src/app/features/organization-management e2e/organization-management.spec.ts` and `git diff --check`: passed.
- `npm run test:e2e -- organization-management.spec.ts`: two passed, light/dark. Each creates all eight types through real UI controls, edits children, clears description, handles stale versions without losing draft, explicitly reloads, confirms deactivate/reactivate, browses children/ancestors, tests empty/validation/list-error recovery, and checks mobile dialog bounds. HTTP responses in this suite are fixtures, not SQL.
- Agent-browser 0.35.1 independently opened the non-intercepted dev server on 4302: login controls and favicon rendered, no captured page errors; expected refresh proxy refusal while no API was started. Not claimed as live sign-in.
- Reviewed generated 1280×720 and 390×844 light/dark screenshots; repaired long-form scrolling so title/actions remain in view and fixed local dark-theme Save/error contrast. Browser assertions enforce title/action viewport and white Save text. Existing screens/license files unchanged.
- Removed temporary task database-name marker accidentally included in T022; no secret/data was in that marker. Runtime test configuration stays outside tracked source.
- Connected actual API/SQL browser validation remains T024. Focused commit recorded by final ledger.

## Definition of Done

Implementation and applicable checks pass; review and documentation are complete; task/index updated; focused Git commit created and verified. No required placeholder or silently skipped check remains.

- Dependency commit verified: T022 `6b3a7d2`.
