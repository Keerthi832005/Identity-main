# Agent diagnostics and requested updates

Status: implemented, tested, committed, published and deployed. The coordinated web rollout applied migration 0014 and deployed IAM/PTS from newer reviewed source `8c60a6c`. The separately approved agent-only activation completed successfully at **12:28:17 IST on 31 August 2026** (`2026-08-31T06:58:17Z`).

## Behavior

- Machines & agents has **Ping machine**, **Update now**, and **Download inventory** actions.
- Ping is a bounded ICMP check from IAM to the selected active enrollment's Windows short hostname. Request bodies cannot supply targets. It has a shared 12 requests/minute/client budget with Update now and at most four simultaneous network probes per API process. DNS and ICMP timeouts are bounded. No reply is not proof that the PC or agent is offline.
- Supervisor 1.0.1 sends a signed heartbeat over the existing outbound HTTPS connection about every 20 seconds when not updating. The page distinguishes this from the 30-minute inventory report. It shows the worker/supervisor versions and last server-received heartbeat.
- Administrators can queue a check for the latest signed **worker** release. IAM records actor, correlation, delivery, completion, and reported result in SQL. The action accepts no command, executable, release URL, or target version. The configured feed's existing RSA signature, expiry, hash, size, downgrade protection, health check, rollback and quarantine rules still apply.
- Commands expire after 15 minutes. Repeated requests return the current request or enforce a one-minute cooldown. Signed device messages have a separate protocol purpose and persisted replay protection; inactive/revoked devices cannot receive commands. Acknowledgements must belong to the same installation and an already-delivered request. A reply is required before displaying updated/up-to-date/failed. Status polling is bounded and stops on selection changes, completion, expiry or page destruction.
- Uptime is explicitly labeled **at inventory capture**, not live telemetry. Disk warnings flag below 10% or 10 GiB free. Trust expiry is separate from connectivity. Downloaded JSON contains inventory and diagnostics, no private keys or credentials.

## Compatibility and installation

Worker and supervisor release: **1.0.1**. Existing inventory payloads without diagnostics remain accepted.

Supervisor 1.0.0 only performs scheduled worker OTA checks; updating its worker cannot add the new command poller. Existing installations need **one local administrator upgrade** using the newly published setup/PowerShell installer. The existing installer reuses the protected identity/enrollment, retains the terminal ID, replaces the supervisor, verifies the worker, and checks local identity readiness. Do not re-enroll or reset device trust to enable updates. Afterwards, Update now works without a reinstall for future signed worker releases. Supervisor upgrades still require a local administrator. There is no remote shell, Windows restart, arbitrary package installation, or fleet-wide push in this change.

## Database and deployment

Migration `0014_agent_update_requests.sql` adds `Identity.AgentControlState` and `Identity.AgentUpdateRequest`; it does not modify personnel, organization, permission, PTS or inventory records. Deploying the new IAM API requires this migration first; the IAM deployer now refuses to proceed without its journal entry and tables. Follow the exact-target approval and verified backup procedure in `DEPLOYMENT.md`. Preserve all existing protected settings and PTS service/worker modes. Publish the fixed reviewed commit; commit only this task's files.

## Verification before publication

- 77 IAM API tests passed, including administration authorization, device proof separation, target restrictions, rate limiting, and ping concurrency.
- 174 frontend tests passed, including request/selection races, stale responses, unsupported supervisors, and uncertain request recovery.
- 9 persistence tests passed against a newly created disposable SQL Developer database, with all 14 migrations applied and migration replay verified. Includes concurrent request idempotence, replay rejection across contexts, pre-delivery/cross-device acknowledgement rejection, command expiry, recorded actor and revocation.
- 11 agent tests passed; the existing real worker update/rollback process test was skipped because it binds the same fixed loopback port as the live agent. The live service was not stopped for a fixture.
- Two browser tests passed with synthetic API data: light desktop and dark mobile. Verified ping outcomes/zero-millisecond reply, update receipt/completion, revoked/old supervisor restrictions, export contents, and no browser page errors. Both screenshots were visually inspected.

The browser fixtures and disposable SQL tests do not prove deployment or completion of a real workstation update. Record live evidence separately after the approved rollout.

## Prepared release and deployment handoff

Implementation commit: `b08a99fe0591ee68698aa5b51c3f93aa1a0ea3ca` (only this task's 32 files). Concurrent production-topology documentation was excluded; it was committed independently in `eec3208`.

Final IAM rollout: `IAM/artifacts/agent-rollout/20260831-062022-8689ba63`. It contains the API, frontend, signed worker 1.0.1, supervisor 1.0.1, and setup. Publication reran 77 API tests, 174 UI tests, production build, installer/bootstrap fixtures, and signed release verification from the fixed source archive. Final artifact and tamper-rejection checks passed under Windows PowerShell 5.1.

The installed PC reaches IAM through the existing PTS `/identity/` proxy. Its approved setup profile is therefore preserved as `https://ptsapp.local.fujitecindia.com/identity/`, with the same PTS site origin and release feed. A separate preserved-profile package retained identical signed manifest, worker ZIP, supervisor binary and release key bytes. The initially built direct-IAM-profile rollout `20260831-061551-044d5404` is **superseded and must not be deployed** for this upgrade, because the existing installer correctly refuses to silently change the saved server profile.

Read-only preflight confirmed current live IAM paths `20260831-055819-ac5b595b-iam-agent`, PTS paths unchanged, and exactly migration 0014 pending in `HOCOM18502627\SQLEXPRESS2022 / FIN_IAM` (13 applied scripts). The first installation preflight intentionally stopped at the profile mismatch before inspecting private installed files. The prepared preflight now requires the observed preserved profile; it must be rerun with administrator access before any live change and verify the installed release trust anchor.

A unique FIN_IAM full `COPY_ONLY` backup with `CHECKSUM` was created and `RESTORE VERIFYONLY WITH CHECKSUM` passed at `2026-08-31T06:15:38Z`. The exact path and verification receipt are in `IAM/artifacts/diagnostics-rollout/20260831-b08a99f/backup-verified.json`. No live migration, reseed, service restart, credential change or IIS switch has occurred for this feature.

The same directory contains `release-plan.json`, the fixed database runner with per-file digests, `preserved-profile.json`, logs, and a syntax-checked `deploy-approved.ps1`. The wrapper requires administrator access and `-ApplyApprovedMigration`, refuses IIS drift, reruns installation/schema/signing-key preflight, re-verifies the backup, applies only the reviewed pending migration, deploys IAM with scoped rollback, upgrades the existing Windows service from the verified setup ZIP, compares saved identity/configuration, and requires a real 1.0.1 heartbeat plus diagnostic inventory before marking success. The archived deployer's only wrapper adjustment is its repository path; ownership, hash, migration, and unrelated-IIS checks remain intact. The wrapper has not been executed.

The preceding preflight/backup notes describe preparation before the coordinated rollout; they are not the current live state. The separate web deployment task subsequently obtained exact-target migration approval, created and verified a fresh backup, applied `0014_agent_update_requests.sql` to **FIN_IAM on HOCOM18502627\SQLEXPRESS2022**, and deployed all IAM/PTS applications from newer source `8c60a6c`. Its successful final handoff was recorded at `2026-08-31T06:54:33Z`. See `PTS/docs/deployment/2026-08-31-full-redeployment-0014.md` and `PTS/artifacts/full-rollout/20260831-064515-fcb3de8c/full-deployment-recovered.json` for authoritative web paths and migration evidence.

**Do not run the older `deploy-approved.ps1` wrapper or deploy this task's older API/UI payload over that rollout.** The signed agent binaries and protocol are unchanged between this task's source and the newer web source. The user separately approved activating the 1.0.1 agent feed and upgrading the one enrolled local machine, retaining terminal 2.

The narrower `IAM/artifacts/agent-activation/20260831-b08a99f/Activate-AgentOnly.ps1` validates the signed package, existing signing trust, saved PTS proxy profile, exact installation identity, migration 0014, single-machine scope and fresh IIS configuration hash. It changes only the `IAM-Api` feed path, recycles that pool, runs the existing verified installer, and requires actual 1.0.1 control heartbeat and diagnostic inventory. It checks that unrelated IIS configuration, all web roots, device identity, enrollment and saved settings remain unchanged. It does not run migrations or deploy any API/UI files. The installer performs the one-time supervisor/worker upgrade; Windows itself is not restarted.

Live administration verification must use a normal authenticated IAM session. Do not create a fabricated production admin token or insert a command directly into SQL to bypass UI/API authorization.

## Completed agent activation

Fresh administrator preflight passed after the web deployment handoff. Exactly one machine was enrolled. Package signature, copied/served release hashes, existing signing trust, saved PTS proxy profile and the installed identity all matched the reviewed scope.

- Installed **worker 1.0.1 and supervisor 1.0.1** on `HOCOM18502627`; service `IAM.Agent` is running with automatic startup and recovery.
- Preserved terminal **2**, installation `6566ecd8-4564-435c-8d9c-85420edf5514`, device key, enrollment and every saved configuration property. Protected identity/enrollment file digests matched before and after installation.
- Activated `C:/inetpub/FIN_PTS/Releases/20260831-agent101-b08a99f/AgentFeed`. Both IAM and PTS identity proxies served the exact signed 1.0.1 manifest; the setup download and worker ZIP matched the reviewed hashes.
- Kept all five newer web physical paths and all unrelated IIS configuration unchanged. Only the IAM feed setting changed. IAM, DS4 and QS4 readiness checks passed afterwards.
- Confirmed a real SQL control heartbeat with both versions 1.0.1 and a real inventory report containing uptime diagnostics. The loopback identity endpoint independently returned hostname `HOCOM18502627`, terminal 2 and agent version 1.0.1.
- No migration, reseed, credential reset, web release replacement or Windows restart was performed by the agent activation. Expected agent heartbeat/inventory writes occurred through its normal authenticated API.

Local evidence: `IAM/artifacts/agent-activation/20260831-b08a99f/preflight.json`, `activation.json`, `install.log`, `live-verification.json`, `live-control-verification.txt`, and `Activate-AgentOnly.ps1`. The success receipt records preserved paths, identity digests, old/new feed roots and completed real heartbeat/inventory checks. Generated artifacts remain ignored; do not commit protected installation files or downloaded packages.

### Actual update request

A live request appeared during verification. Read-only SQL inspection confirmed request `e6ee150c-e277-4da2-bf0e-1ad6c905adab`, recorded administrator user ID 1 and correlation `2e0d7b2c-2b70-4a3c-8ee5-9f0daeebbf58`:

| Event | UTC time on 31 August 2026 |
| --- | --- |
| Requested | 06:58:20.411 |
| Delivered to agent | 06:58:35.938 |
| Agent completion received | 06:58:55.978 |
| Result | `upToDate`, version `1.0.1` |

This proves actual command delivery and an agent result through the persisted control protocol. Version 1.0.1 was already installed, so the request correctly performed an update check without replacing the worker again. A later-version OTA replacement was not exercised in this live check. No command was inserted directly into SQL by this task.

The in-app IAM browser remained signed out; this task did not submit credentials or itself click the authenticated page. Authenticated layout/actions were covered by the earlier browser fixtures, while the live command completion above was verified separately in SQL. Final checks at `2026-08-31T06:59:29Z` again confirmed terminal 2, running worker 1.0.1, delayed automatic service startup, and HTTP 200 readiness from IAM, DS4 and QS4.
