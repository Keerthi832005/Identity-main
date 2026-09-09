# IAM audit grid and sidebar update

Completed: 31 August 2026, 13:35 IST.

- Converted Active sessions to a compact DevExtreme grid with sorting, filtering, search, paging, status and the existing confirmed revoke action.
- Removed the sidebar menu search and Prev/Next navigator. Preserved the header search, keyboard navigation, menu links and collapsible sections.
- Updated the navigation browser-test expectations. Those browser tests were not run for this change.
- Production build passed; seven audit tests and ten shell tests passed. Existing DevExtreme dependency optimization warnings remain.

## Deployment

Deployed IAM web app **0.1.2** at `https://iam.local.fujitecindia.com/` using the built working-tree changes. The frontend-only release used the existing version allocator and deployment mutex. No API, database, agent feed, PTS site or worker was changed.

- Release: `C:/inetpub/FIN_PTS/Releases/20260831-080423-314325a5-iam-web/IAM/Frontend`.
- Previous web root retained: `C:/inetpub/FIN_PTS/Releases/20260831-064606-1b821076-iam-agent/IAM/Frontend`.
- IIS backup: `IAM-Web-20260831-080423-314325a5`.
- Preserved live `config.json`, `web.config`, bindings and pool settings. Only the IAM-Web physical path changed and its pool was recycled.
- Verified exact served bytes for the root, login and audit routes, all JavaScript/CSS assets, runtime configuration and version metadata. API readiness returned 200; unauthenticated security operations returned 401. No real session was revoked.
- Compared IIS configuration excluding the intended web-root change and confirmed the previous release stayed unchanged.

Ignored operational evidence is under `IAM/artifacts/web-rollout/20260831-080423-314325a5/`: file-hash manifest, source patch, deployment wrapper and successful `deployment-result.json`. The wrapper is scoped to this release. Rollback must check the current IAM-Web path before switching only that site to the retained previous root; do not restore an entire IIS backup over unrelated work.
