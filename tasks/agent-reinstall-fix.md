# IAM.Agent repeated-installation recovery

User reported a second installation failing with `File.Replace: The path is not of a legal form` after a successful enrollment.

## Cause and recovery

Windows PowerShell 5.1 binds `$null` to an empty string for the .NET `File.Replace` backup-path parameter. The fresh-install `File.Move` branch worked, but replacing existing JSON failed. The same helper was used by rollback, so the prior installer left the service stopped after its recovery write failed.

The existing service was recovered on 31 August 2026: its executable path/ownership, supervisor bytes and worker bytes were checked against the verified signed 1.0.0 release before starting it. Local identity returned terminal 2/version 1.0.0. Configuration, version and enrollment file hashes were unchanged. Evidence: `IAM/artifacts/reinstall-fix/recovery-result.json`. No re-enrollment, credential reset or database write was performed.

## Fix

- Atomic JSON replacement passes `System.Management.Automation.Language.NullString.Value`, supplying an actual null backup path on Windows PowerShell; per-write temporary files are cleaned even on failure.
- After signed package, installed worker and owned-service checks, an identical healthy installation returns **already installed and running** without stopping the service or rewriting configuration. Changed versions/configuration/supervisor, stopped services and invalid local identity continue into normal installation/repair; validation is not bypassed.
- Only the uniquely generated worker extraction directory from that invocation is removed, with absolute-path/name and link checks.
- Bootstrap explains that existing installations are checked first. A failure points to the actual installer error rather than implying every failure is a script-signing problem.

## Verification

`Test-AgentReinstall.ps1` runs the actual installer helpers against temporary files and a synthetic local identity response. It reproduces the original bug in Windows PowerShell, verifies first creation/replacement/rollback, preserves destination bytes on a locked-file failure and checks healthy-repeat versus repair decisions. It does not run a service, connect to IAM or create test databases. The fixture suite also passes under PowerShell 7 and is included in archived rollout publication gates.

## Verified release

Committed source `942b9e638a61828da28af14ec3e9bd08ebd5704a` was published as `IAM/artifacts/agent-rollout/20260831-055819-ac5b595b`. Its archived build passed 60 API tests, 163 frontend tests, Windows PowerShell reinstall fixtures, bootstrap/UI fixtures and signed rollout validation. Real bundle extraction repeated the UI and reinstall fixture suites against the packaged scripts.

An elevated read-only precheck first confirmed that the existing installation matched the healthy-repeat conditions. The complete packaged installer then ran **twice** on the recovered machine. Both runs returned `already installed and running`, exit zero and terminal 2/version 1.0.0. The Windows service retained the same process ID and stayed Running. Configuration, version, enrollment and public identity file hashes were unchanged; neither run left a new temporary worker extraction directory. These were actual repeated installer runs, not only mocked helper tests. No new IAM login or enrollment was performed.

The guarded IAM-only deployment completed at `2026-08-31T06:02:11Z`, replacing `20260831-054840-9db5605a-iam-agent` while preserving protected settings and PTS. Live HTTP verification at `2026-08-31T06:02:36Z` confirmed the uncached bootstrap, exact setup ZIP and frontend index, original logo/icon bytes and service still Running. Current ZIP SHA-256: `39AA6BE6B61BB21A39581BC7AE9D01D5D8D858A7F45812B6A390F4B47C37021D`. The signed worker stays at 1.0.0; only setup behavior changed.

Evidence under the release directory: `live-repeat-result.json`, `repeat-install-1.log`, `repeat-install-2.log`, `deployment-result.json`, `bootstrap-deploy-result.json` and `live-modern-installer-verification.json`. The HTTP verification itself did not execute setup; actual execution is recorded separately in the repeat-install evidence. The user does not need another reinstall after this recovery.
