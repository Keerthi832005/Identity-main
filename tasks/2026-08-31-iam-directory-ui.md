# IAM directory and inventory UI

## Changes

- Compact sidebar; existing local UI at `http://localhost:4301`, with its original IIS API proxy.
- Machines, Users and Applications use full-width directory grids with links to detail routes. Search and server pagination are retained in the URL. Existing edit confirmations and permission controls remain.
- Organizations use a full-width hierarchy grid and separate detail view, retaining expansion, ancestor browsing, editing and conflict recovery.
- Security details align at the top, with a bounded scrolling user list.
- Active sessions show user, application and client names supplied by the API. Numeric identifiers remain available in the column chooser; old API responses retain explicit ID fallbacks.
- Machine inventory includes the current interactive Windows user, signed-in sessions and local profile account metadata. The machine grid obtains the current username through a SQL JSON projection, without fetching each machine's full inventory.
- IAM TypeScript, HTML, SCSS and C# are formatted with Prettier and dotnet format. Vendor/generated files remain excluded.

## Windows user semantics

The agent uses Windows session APIs, not its own service account. A console session is preferred; otherwise a single active session is used. Multiple active sessions are listed without inventing a single current user. Disconnected sessions remain distinct. Special/system profiles are excluded, and profile file contents and credentials are never collected. Unknown accounts retain the local profile name and SID. A loaded profile is not proof of an active login.

These are snapshots from the last inventory (normally every 30 minutes), not a live login feed. Missing legacy reports show **Not reported**. Partial collection is labelled separately from an empty result. Arrays and text are bounded and validated in the signed-report protocol.

References: [Windows session query API](https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsquerysessioninformationa), [Windows user profiles](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/legacy/ee886409(v=vs.85)).

## Rollout boundary

This change is local source work. It does not redeploy IIS, modify production data, post a machine report or publish an agent release. The new names in API responses require the updated IAM API; Windows account data also requires the updated agent and its next inventory report. Existing reports and authentication remain compatible. No database schema migration is required.

## Verification

Frontend unit tests, production build, mock-API Playwright flows (light/dark, desktop/mobile), agent signed-report tests and SQL projection translation checks are used. Real administration actions are not submitted during browser verification. SQL integration tests require a separate disposable test database.

Final results:

- Prettier check and `dotnet format --verify-no-changes`: passed.
- IAM solution build: passed, no warnings or errors. Frontend production build: passed with existing DevExtreme CommonJS notices.
- Frontend unit tests: 180 passed. Backend solution: 231 passed, 42 skipped for unavailable database/opt-in prerequisites.
- 23 relevant browser checks passed across directories, forms, permissions, confirmations, inventory, software, Security and responsive layouts.
- The opt-in Windows collection test was also run separately on this PC and passed signed-report validation. No inventory was posted.
- Local proxied readiness endpoint: HTTP 200.

Screenshots are in ignored `src/Frontend/artifacts/layout-verification`, `sidebar-test-results` and `final-regression` directories.
