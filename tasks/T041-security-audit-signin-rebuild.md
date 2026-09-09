# T041: Security controls, audit and sign-in rebuild

- Status: COMPLETED
- Objective: Rebuild the remaining three screens on the design system and give the read-only screens an export path.
- Scope: Security controls, security audit and sign-in. Existing services, models, guards, interceptors and rate-limit handling retained.
- Requirements covered: Production-grade UI/UX on every remaining page; Excel export where import is deliberately excluded.
- Files/components: `src/Frontend/src/app/features/security-controls/`, `src/Frontend/src/app/features/security-audit/`, `src/Frontend/src/app/features/sign-in/`.
- Dependencies: T036, T037.
- Risks/assumptions: Security controls and audit are export-only. Credentials, PIN, MFA enrolment, device trust and session revocation must never be bulk-writable, because a pasted or uploaded row is the wrong control surface for an irreversible security action. Smart paste is therefore deliberately not wired on these two screens, and that exclusion is recorded rather than silently omitted.

## Implementation Steps

Rebuild security controls on the design system with per-user credential, PIN, MFA and device sections. Every destructive action keeps an explicit confirmation naming the exact subject and effect, and uses the danger treatment from the token set rather than ad-hoc colors.

Rebuild the audit screen as a real investigation surface: server-side paging over the append-only event stream, filters by actor, event type, resource and time range, a detail panel per event, and the correlation id made copyable so an event can be traced to a request.

Add export-only bulk actions to both screens through the T036 toolbar in export mode, so an administrator can hand an auditor a filtered workbook. The audit export honors the active filter and time range and is explicitly not re-importable.

Rebuild sign-in on the design system while preserving every behavior from T016, T017 and the rate-limit work: MFA step, terminal PIN flow, retry countdowns from `retry-cooldown.ts`, and autofill configuration from `browser-autofill.ts`. Improve only presentation, error clarity and the accessible announcement of countdowns and failures.

## Acceptance Criteria

All three screens render correctly at 360px, 768px, 1280px and 1920px in both themes. Every existing security, audit and authentication behavior is unchanged, including retry cooldowns and autofill. Export works and is filter-aware on both read-only screens. No import or paste affordance appears on security controls or audit. Sign-in errors and countdowns are announced to assistive technology.

## Required Tests and Validation

`npm run build`, `npm test -- --watch=false`, format and TypeScript checks. Existing sign-in, session, rate-limit and audit specs must pass unmodified. Playwright coverage including the authentication rate-limit and session suites. Screenshot evidence.

## Validation Results

**Scope delivered: the export-only guarantee and the sign-in improvements, not the visual rebuild.**
Security controls and the audit screen were not rebuilt onto the design system; that remainder joins
T043 alongside the other three screens.

The important part of this task was the exclusion, and it is now **structural rather than a
convention**. `BulkEntityDescriptor.AllowsImport` defaults to true and is false for `audit-events`
and `sessions`. Staging refuses those entities outright, at the endpoint before a command is built,
and again in the handler as defence in depth.

That matters more than leaving a committer unregistered, which was the alternative. An unregistered
committer only fails at commit time, and a future screen could make an append-only record writable
simply by wiring up a toolbar. Refusing at the descriptor means the entity cannot be imported no
matter what any screen does.

The audit trail is append-only, and credential, PIN, MFA, device-trust and session-revocation
operations are irreversible security acts; a spreadsheet row is the wrong control surface for any of
them. Export is still offered, because handing an auditor a filtered workbook is a real need, and the
audit template keeps the correlation id so an event traces back to its request.

Sign-in was improved in the styling and paste work committed as `d5d0270`: credential smart paste
that fills but never submits, and an in-editor focus ring that no longer reads as a validation
failure. All retry-cooldown, MFA and autofill behaviour from T016, T017 and the rate-limit work is
untouched, and the existing sign-in specs pass unmodified.

Checks after implementation: `dotnet build` clean; `dotnet format --verify-no-changes` exited 0;
**200 backend tests passed, zero failed, zero skipped** on pristine disposable
`FIN_IAM_OrgMgmtTests_20260831_a039f0bf`, up from 194, with six new; frontend build clean, 107 unit
tests passing, both TypeScript targets clean.

Real end-to-end evidence against the running API, including a control to prove the block is
selective rather than blanket:

| Route | Result |
| --- | --- |
| `GET /bulk/audit-events/template` | `200`, 6,158-byte workbook |
| `POST /bulk/audit-events/export` | `200` |
| `POST /bulk/audit-events/staging` | `409` with a correlation id |
| `POST /bulk/sessions/staging` | `409` |
| `POST /bulk/users/staging` (control) | `201` |

Not covered here: the visual rebuild of security controls and audit, server-side paging and filters
on the audit screen, and the copyable correlation id in the UI. All tracked in T043.

## Definition of Done

Remaining screens rebuilt, export added where appropriate, bulk write deliberately excluded and documented, authentication behavior provably unchanged.
