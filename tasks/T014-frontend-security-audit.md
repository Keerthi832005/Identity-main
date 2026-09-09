# T014: Security audit, sessions, and revocation operations

- Status: COMPLETED
- Objective: Provide bounded operational visibility and safe revocation controls.
- Scope: Authentication/audit search, correlation detail, active refresh-token family summaries, revoke sessions/resources, filters, export of non-sensitive fields, and accessible state indicators.
- Requirements covered: Append-only audit operations and incident response.
- Files/components: Backend audit/session queries and frontend security-operations feature.
- Dependencies: T013.
- Risks/assumptions: Audit metadata is allow-listed; raw identifiers, tokens, headers, hashes, and protected payloads remain hidden.

## Implementation Steps

1. Define privacy-safe audit/session read models and paging.
2. Implement authorized queries and revocation commands.
3. Build filters, timeline/detail, export, and confirmation workflows.

## Acceptance Criteria

- Administrators can investigate by time, event, user, application, result, and correlation ID.
- Session revocation is auditable and no secret-derived values are exposed.

## Required Tests and Validation

Backend/frontend gates, privacy contract tests, browser operations tests.

## Validation Results

- Isolated .NET format/build passed with 0 warnings and 0 errors; the full backend suite passed.
- Angular production build and frontend unit tests passed.
- Privacy-safe filtering, bounded audit paging, session-family summaries/revocation, confirmation, and safe CSV export are implemented.
- Frontend formatting and production dependency audit passed with 0 vulnerabilities.

## Definition of Done

Implementation, tests, task records, and focused commit are complete.
