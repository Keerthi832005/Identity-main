# Third-Party Notices

Most of IAM runs on free/open-source dependencies. **Two are commercial and licensed**, and are
listed first because a deployment cannot ignore them.

## Commercial dependencies requiring a DevExpress licence

| Dependency | Version | Licence | Purpose |
|---|---:|---|---|
| DevExtreme / devextreme-angular | 26.1.4 | DevExpress commercial (browser licence key) | Administration UI controls |
| DevExpress.Document.Processor | 26.1.3 | DevExpress commercial (Office File API) | All Excel template generation, parsing and annotation |

Both are approved exceptions to the free/open-source rule in
[Identity Coding Standard](Identity-Coding-Standard.md). Neither is optional: without a valid
DevExpress subscription the UI shows a licence banner and the Excel endpoints cannot be redistributed.

`DevExpress.Document.Processor` is referenced **only** from `Identity.Infrastructure` and sits behind
`Identity.Application` ports, so Domain and Application carry no dependency on it and the engine
remains replaceable.

### Restore requirement, not yet satisfied

No DevExpress package source is registered in this repository or in the machine's NuGet
configuration. `DevExpress.Document.Processor 26.1.3` currently resolves **only from the local NuGet
cache**, which means a clean checkout or a CI agent will fail to restore.

Before this builds anywhere but a developer machine that already has DevExpress installed, add the
DevExpress feed as a package source with credentials, or vendor the package into a repository-local
folder feed. This is a deployment and procurement action; it is not fixed in source.

### Transitive advisory, pinned

`DevExpress.Printing.Core` resolves `System.Security.Cryptography.Xml 8.0.3`, which carries three
high-severity advisories (GHSA-cvvh-rhrc-wg4q, GHSA-g8r8-53c2-pm3f, GHSA-mmjf-rqrv-855v). The package
is pinned to `10.0.11` in `Directory.Packages.props` through central transitive pinning. `NU1903` is
**not** suppressed, so a future DevExpress release that regresses this will fail the build rather than
ship quietly.

## Free and open-source dependencies

| Dependency | Version | License | Source | Purpose |
|---|---:|---|---|---|
| DbUp | 7.2.0 | MIT | https://github.com/DbUp/DbUp | Forward-only SQL Server deployment |
| Microsoft ASP.NET Core packages | 10.0.11 | MIT | https://github.com/dotnet/aspnetcore | HTTP API, JWT validation, OpenAPI, testing |
| Microsoft Entity Framework Core | 10.0.11 | MIT | https://github.com/dotnet/efcore | Explicit SQL Server persistence mapping |
| Microsoft IdentityModel JWT | 8.19.2 | MIT | https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet | RS256 JWT creation and validation primitives |
| Angular | 22.1 | MIT | https://github.com/angular/angular | Administration UI framework |
| Barlow, Barlow Condensed, IBM Plex Mono | via @fontsource | OFL-1.1 | https://fontsource.org | Typefaces |
| xUnit.net | 3.2.2 | Apache-2.0 | https://github.com/xunit/xunit | Automated testing |
| Vitest | 4.0.8 | MIT | https://github.com/vitest-dev/vitest | Frontend unit testing |
| Playwright | 1.62.1 | Apache-2.0 | https://github.com/microsoft/playwright | Browser testing |

Transitive Microsoft.Extensions and Microsoft.IdentityModel dependencies are distributed under the
MIT license.
