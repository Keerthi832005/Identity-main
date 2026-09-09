# User department and team mapping

In **Users & access → New user / Edit profile**, choose an optional Department and then an optional Team. Manager, Department and Team each use one searchable dropdown: type directly in the field to search the server, then select a result. There are no separate Find inputs. Searches return up to 50 matches; refine the text to find records beyond the initial results. Manager search accepts employee code, name or email. The profile summary shows saved names. Team choices are filtered to the selected department. Changing or clearing Department clears Team.

Create real departments under an organization and teams under a department in **Organizations** first. No placeholder hierarchy or employee assignments are seeded. Repeated personnel names remain separate users identified by employee code.

## API contract

`POST /api/v1/admin/users` and `PUT /api/v1/admin/users/{userId}/profile` accept an optional object alongside the existing profile fields:

```json
{
  "displayName": "Employee name",
  "email": null,
  "managerUserId": null,
  "organizationMapping": { "departmentId": 11, "teamId": 12 }
}
```

- Omit `organizationMapping`, or send it as null, to preserve an existing mapping. This keeps older clients and existing bulk user imports compatible.
- Send `{ "departmentId": 11, "teamId": null }` for a department without a team.
- Send `{ "departmentId": null, "teamId": null }` to clear both.
- IDs must be positive. Departments and teams must exist with the correct unit types; Team must belong to Department. New assignments require active units and an active organization. A current inactive mapping can be retained during unrelated edits or removed.

User summaries from search, dashboard, access detail and security detail include nullable `departmentId`, `departmentName`, `teamId`, and `teamName`. Authorization remains the existing IAM administration policy. User creation and profile changes use the existing audit events; no-op mapping updates do not add a change audit. Assignments do not grant application access, roles, capabilities, or credentials and do not change the security version.

## Persistence and deployment

Forward-only migration **0013_user_organization_mapping.sql** adds two nullable UserAccount columns. Existing rows remain unassigned. A typed department FK, composite team/department FK, and check constraint reject inconsistent pairs even outside the application. No rows are deleted or existing migrations altered.

Back up and verify the intended database, apply 0013 through the migration runner, then deploy the API and UI from the same reviewed source. The previous application can still run with these additive columns if application rollback is needed; retain the schema and backup rather than dropping columns containing assignments.

The default employee roster is managed separately by the [default seed task](../tasks/A008-default-employee-roster.md). Bulk import columns remain unchanged and preserve organization mappings; use the profile editor/API to assign departments and teams.
