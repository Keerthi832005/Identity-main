# A003: IAM.Agent install-update

- Status: COMPLETED
- Objective: Windows supervisor, download installer, signed release feed and rollback.
- Scope: Windows supervisor, download installer, signed release feed and rollback.
- Requirements covered: User's IAM-owned managed Windows agent; PTS consumes terminal identity.
- Files/components: IAM.Agent, Identity backend, IAM UI, scripts/tests/docs.
- Dependencies: A002.
- Risks/assumptions: IAM admin trust required; no credential collection or arbitrary remote command execution; preserve concurrent changes.

## Implementation Steps
Implement this slice; test security and recovery; review, document and commit.
## Acceptance Criteria
Download uses IAM's configured distribution directory; installation enrolls without a typed terminal ID; service starts automatically; signed worker updates require health and recover the previous version after failure.
## Required Tests and Validation
Focused .NET/Angular tests, builds, migration/package/browser tests appropriate to this slice.
## Validation Results
- Self-contained win-x64 worker/supervisor publication, signature and package hash verification passed. Final setup: `artifacts/agent-publish/1.0.0-07c4d03c/feed/IAM.Agent.Setup.zip` (ignored artifact, not committed).
- Agent suite: 10 passed, including real 1.0.0 → 1.0.1 upgrade, broken signed 1.0.2 rejection, rollback and failed-version quarantine. HTTP tests cover IAM preflight header, hostile origins/hosts and denied writes.
- IAM API suite: 56 passed, including public allowlisted downloads, private-file rejection, cache policy and admin authorization.
- Installer/publisher/uninstaller PowerShell parsing passed; both frontends production-build successfully. No actual service installation, reboot, IAM administrator enrollment or live IIS change was performed; these require normal administrator commissioning.
- Installer protects config/key directories, retains rollback state, verifies packages and never bypasses UAC, TLS or execution policy. OTA updates worker plus bundled runtime; supervisor updates require administrator reinstall.
## Definition of Done
Implementation, applicable checks, task record and focused Git commit.
