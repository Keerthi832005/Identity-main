# A002: IAM.Agent inventory

- Status: COMPLETED
- Objective: Authenticated enrollment, machine reports, SQL persistence and IAM inventory UI.
- Scope: Authenticated enrollment, machine reports, SQL persistence and IAM inventory UI.
- Requirements covered: User's IAM-owned managed Windows agent; PTS consumes terminal identity.
- Files/components: IAM.Agent, Identity backend, IAM UI, scripts/tests/docs.
- Dependencies: A001.
- Risks/assumptions: IAM admin trust required; no credential collection or arbitrary remote command execution; preserve concurrent changes.

## Implementation Steps
Implement this slice; test security and recovery; review, document and commit.
## Acceptance Criteria
Authenticated per-device reports, accurate visibility and no invented live validation; applicable startup/update checks pass.
## Required Tests and Validation
Focused .NET/Angular tests, builds, migration/package/browser tests appropriate to this slice.
## Validation Results
- IAM.Agent tests: 3 passed (HTTP/config and signed-report validation).
- IAM API agent authorization test: passed.
- `scripts/Test-AgentInventory.ps1`: fresh disposable SQL database, all 12 migrations, replay and enrollment/report/revocation test passed; generated database removed. Fixed an EF projection translation issue found by this test.
- IAM frontend: 122 tests passed; production build passed with existing DevExtreme CommonJS warnings.
- Machine-wide registry software inventory is explicit about per-user/portable exclusions and size truncation. Metadata failures are shown as collection warnings. No live enrollment/deployment performed.
- All agent task records now live under IAM; PTS retains only its integration source.
## Definition of Done
Implementation, applicable checks, task record and focused Git commit.
