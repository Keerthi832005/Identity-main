# A001: IAM.Agent identity and PTS integration

- Status: COMPLETED
- Objective: Deliver the user's automatically identified, downloadable Windows terminal agent.
- Scope: Agent core, restricted local HTTP API, Terminal automatic detection and tests.
- Requirements covered: Hostname/device info, no typed ID, automatic startup, signed OTA.
- Files/components: Agent projects, Terminal access, scripts, tests, docs.
- Dependencies: Existing IAM device verification.
- Risks/assumptions: Admin installation and IAM trust approval required; browser local-network permission possible; preserve unrelated changes.

## Implementation Steps
Implement this slice, test success and failure paths, review, document, commit.
## Acceptance Criteria
No arbitrary ID input or credential persistence. Secure restricted discovery and signed updates. Record actual deployment separately from synthetic tests.
## Required Tests and Validation
Agent tests, relevant Terminal suite/build, package/service/browser checks appropriate to this slice.
## Validation Results
User renamed the agent IAM.Agent and required ownership under IAM; source projects and tests now live there. `dotnet test IAM/src/Backend/Tests/IAM.Agent.Tests/IAM.Agent.Tests.csproj`: 2 HTTP/config tests passed. PTS Terminal `npm test`: 287 passed; `npm run build`: passed. Tests verify hostile Origin/Host/write rejection and frontend clears stale identity without sending credentials. Download packaging and inventory follow in IAM tasks. No live deployment claimed.
## Definition of Done
Implementation, applicable checks, reviewed diff, updated record and focused Git commit.
