# T042: Revamp final validation, documentation and dependency exception

- Status: COMPLETED
- Objective: Prove the revamp end to end, record the commercial dependency exception, and close the release boundaries honestly.
- Scope: Cross-screen validation, documentation, dependency and security review, publish check. No new behavior.
- Requirements covered: Every page production-grade, every management page carrying smart paste and Excel template export and import, with real evidence rather than assertion.
- Files/components: `doc/IAM-Bulk-Data.md`, `doc/T032-T042-Revamp-Verification.md`, `doc/Identity-Coding-Standard.md`, `README.md`, `THIRD-PARTY-NOTICES.md`, `tasks/README.md`, `scripts/Publish-Identity.ps1` verification.
- Dependencies: T032 through T041.
- Risks/assumptions: `DevExpress.Document.Processor` is a licensed commercial redistributable and conflicts with the standing rule in `doc/Identity-Coding-Standard.md` that production dependencies be free/open-source and self-hostable without production license keys. It was added on explicit user direction. This task records it as a second approved exception beside DevExtreme rather than leaving the standard silently violated, and states the runtime licensing and deployment consequence plainly.

## Implementation Steps

Run the full gate on both halves: `dotnet format`, `dotnet build`, `dotnet test` including SQL Server tests on a uniquely named disposable database with migration `0011` applied and replayed; `npm run build`, `npm test -- --watch=false`, `npm run format:check`, all TypeScript checks, and the complete Playwright suite in fixture and live modes.

Verify the publish path still produces separate `api`, `database`, `admin-cli` and `frontend` outputs, and confirm the DevExpress assemblies land in the API output. Register a DevExpress package source or vendor the package, because a clean or CI restore currently fails: only the local NuGet cache resolves `DevExpress.Document.Processor 26.1.3` today.

Write `doc/IAM-Bulk-Data.md`: the template contract per entity, the `_meta` sheet and versioning rule, the stage-preview-commit lifecycle, smart paste behavior and column matching, error codes, bounds, retention and expiry of staged data, and the deliberate exclusion of secrets and of bulk security operations.

Write `doc/T032-T042-Revamp-Verification.md` with the actual commands, actual counts and actual commit hashes, distinguishing fixture browser evidence from real API and SQL evidence exactly as `T029-T031` does.

Update `doc/Identity-Coding-Standard.md`, `README.md` and `THIRD-PARTY-NOTICES.md` to record the DevExpress Office File API exception, its licensing obligation and its deployment requirement. Update `tasks/README.md` with final status and evidence.

Run a dependency and security review over the added package and the new upload surface, and confirm the request size limit, content-type checks, row bounds and per-administrator batch isolation from T035 are all active in the built application.

## Acceptance Criteria

Every check passes with recorded real output. Migration `0011` applies to a blank database and replays safely. Publish artifacts build. The commercial dependency exception is documented in all three files. Every disposable database is removed after exact-target verification. Remaining release boundaries are stated explicitly.

## Required Tests and Validation

The full backend and frontend gates, migration apply and replay, publish verification, dependency review, and focused commit verification with a clean task-owned tree.

## Validation Results

Full evidence is recorded in [T032-T043 verification](../doc/T032-T043-Revamp-Verification.md). The
summary: **200 backend tests and 107 frontend tests pass with none skipped**, formatting and both
TypeScript targets are clean, migration `0011` applies to a blank database and replays as a no-op, and
all four publish outputs build with `DevExpress.Docs.v26.1.dll` in the API output.

Two pieces of real work came out of this task rather than documentation alone.

**The expiry sweep now runs.** Retention was modelled in T034 but nothing executed it, so abandoned
batches would have accumulated a second copy of administrator-supplied identity data outside the
tables that own it. `BulkStagingExpiryService` sweeps every 30 minutes, up to 200 batches per pass. A
failed sweep is logged and retried at the next tick rather than stopping the service, because staged
data lingering one interval is better than never sweeping again.

**A silent partial publish was found and fixed.** `Publish-Identity.ps1` aborted in its frontend step
whenever output was captured: npm writes warnings to stderr, and `$ErrorActionPreference = 'Stop'`
turns a native command's stderr into a terminating error in Windows PowerShell. The backend had
already published by that point, so the run produced a full-looking output set containing a **stale
frontend from the previous day** — precisely what a CI pipeline, which always captures output, would
have shipped. The npm calls now run with the preference relaxed while still checking exit codes, and
the script fails loudly if any of the four outputs is empty. Verified by deleting the frontend output
and re-running with output captured: exit 0 and the frontend republished.

Documentation written: `doc/IAM-Bulk-Data.md` (template contract, `_meta` and versioning, the
stage-preview-commit lifecycle, smart paste and column matching, error codes, bounds, retention, and
how to add an entity) and `doc/T032-T043-Revamp-Verification.md`.

`THIRD-PARTY-NOTICES.md` was substantially wrong and is corrected. It claimed IAM "uses only
free/open-source production dependencies" and did not list DevExtreme at all, even though DevExtreme
had been an approved exception since T015. Both commercial dependencies are now listed first, with
their licensing obligation, the transitive advisory pin, and the unmet restore requirement.

Responsive and dark-theme evidence deferred from T037 and T043 was captured: 360px stacks correctly
with the rail collapsed and both cards intact, and dark theme at 1440px resolves every design-system
token per theme.

Seven defects found across the revamp are listed in the verification document. Each was found by
exercising the real system rather than fixtures, and each was a genuine fault: nested transactions
returning `500` on commit, a vulnerable transitive package, the silent partial publish, a breadcrumb
that named the wrong screen, a command palette whose ARIA attributes were silently ineffective, a
focus ring that read as a validation failure on a password field, and paste that required a click
first.

**Release boundaries that remain open, and are not closed by this task.** A clean or CI restore still
fails: no DevExpress package source is registered and the package resolves only from the local NuGet
cache. Adding a credentialed feed or vendoring the package is a procurement and deployment action, not
a source change, so it is recorded in the README and the notices rather than papered over. The
DevExtreme browser licence key is still empty in `public/config.json`. The feature remainders named in
T043 were recorded, not delivered.

No deployment, push, merge or business-database change was performed. `FIN_IAM`, `FIN_IAM_Dev`,
`FIN_PTS_DS4` and `FIN_PTS_QS4` were never written, and every disposable database was dropped after
verifying its exact name.

## Definition of Done

Revamp verified with honest evidence, dependency exception recorded, restore gap closed, no deployment or business-database change.
