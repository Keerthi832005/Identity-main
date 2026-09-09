# A010 — Combined profile lookup fields

Status: COMPLETED — deployed to local IAM at 10:51 IST on 2026-08-31.

The user found separate Find and selection fields repetitive. Manager, Department and Team now each use one editable searchable dropdown, backed by the existing server search APIs. Typing searches, choosing an option changes the stored ID, and the clear button / No selection option removes it. Each search remains bounded to 50 matches; further typing searches the server rather than filtering only the first page. Manager searches retain email matching even when the result label does not contain the email.

Team remains disabled until Department is chosen. Department changes invalidate pending team results and clear Team. Existing labels remain available through lookup by ID when the selected item is absent from current results. Errors retain the stored selection and offer Retry. Existing plain selection controls elsewhere are unchanged.

No API contract, schema, employee data, credentials or permissions change. The new shared control and user profile files are the only application changes in this task; concurrent confirmation-dialog changes belong to another agent and must not be committed with this task.

Verification: production frontend build passed; 150 frontend tests passed. Browser create/edit/clear/error workflows passed in light and dark themes; the extended run also checks inline server searches in all three fields and the absence of separate Find inputs.

Implementation commit: `6f8212cbdd6cc1e57bb3c8d7ea97d9b5051de070`. Release `IAM/artifacts/agent-rollout/20260831-051806-26aa9b89` was built from this exact committed archive, excluding the other agent's uncommitted files. The archive repeated all 56 API and 150 frontend tests successfully; package verification and seven rejection checks passed. Existing third-party source-map/CommonJS warnings remain.

The deployment held the shared IIS lock and first checked that the active IAM release was still `20260831-050317-dec61bfd`, protecting against overwriting a concurrent rollout. Existing scoped deployment checks succeeded at `2026-08-31T05:21:10Z`. Protected configuration, PTS, database schema/data and agent service state were unchanged; the standard IAM rollout recycled only its API pool. The previous release remains available.

Live `chunk-DoGDqaa6.js` matches the verified bundle byte-for-byte and contains none of the three former Find labels. IAM readiness and PTS Terminal returned HTTP 200. Evidence: `lookup-result.json`, `deployment-result.json`, `live-lookup-verification.json`, and desktop/mobile browser screenshots under the release directory. Browser fixture tests used synthetic data; no live employee assignments were changed.
