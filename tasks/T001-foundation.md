# T001: Split repository and establish IAM solution

- Status: COMPLETED
- Objective: Create independent `PTS/` and `IAM/` project roots and a buildable IAM solution foundation.
- Scope: Move existing PTS assets, create IAM build files/solution/projects/tests, promote the approved schema to IAM DbUp migrations, and preserve Git history.
- Requirements covered: Separate repository folders, separate publishable solution, separate database project, no PTS-prefixed IAM code.
- Files/components: Repository layout, `PTS/`, `IAM/`, IAM solution/projects, IAM migration, task records.
- Dependencies: None.
- Risks/assumptions: Root `.gitignore` and `.vscode` remain repository-wide; all other project-owned files move under their project root.

## Implementation Steps

Verify move targets, move PTS-owned content, create IAM-owned configuration and project skeletons, convert the reviewed SQL into migration `0001`, then restore/build/test both roots.

## Acceptance Criteria

Both project roots are independent, PTS behavior is unchanged, IAM has no PTS-named code, the IAM migration runner embeds only IAM SQL, and both solutions build.

## Required Tests and Validation

Run Git rename inspection, `dotnet restore`, formatting verification, builds, skeleton tests, and a sensitive/generated-file scan.

## Validation Results

- Verified the resolved `PTS/` and `IAM/` move targets remained inside the repository before moving explicitly named paths.
- `dotnet restore PTS.slnx` and `dotnet restore Identity.slnx`: passed independently.
- `dotnet format PTS.slnx --verify-no-changes --no-restore` and the equivalent Identity command: passed.
- `dotnet build PTS.slnx --no-restore`: passed with zero warnings and errors after the move.
- `dotnet build Identity.slnx --no-restore`: passed with zero warnings and errors.
- `dotnet test PTS.slnx --no-build --no-restore`: 50 passed, 16 SQL-dependent tests skipped, zero failed.
- `dotnet test Identity.slnx --no-build --no-restore`: four passed, zero skipped or failed.
- Both vulnerable-package scans reported no known vulnerable direct or transitive packages.
- Admin CLI `--version` returned successfully; the database runner failed closed with exit code 2 when no connection was supplied.
- Static checks found 16 migration tables, no PTS-prefixed IAM code, no production cross-project reference, no anonymous C# object payload, and no whitespace errors.

## Definition of Done

Implementation and validation pass, this record and index are updated, and the focused T001 commit succeeds.
