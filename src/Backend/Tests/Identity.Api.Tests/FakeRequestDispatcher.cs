using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Application.Messaging;
using Identity.Application.BulkData;
using Identity.Application.Mfa;

namespace Identity.Api.Tests;

public sealed class FakeRequestDispatcher(string modulus, string exponent) : IRequestDispatcher
{
    public object? LastRequest { get; private set; }

    public ValueTask<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        object response = request switch
        {
            LoginCommand login when login.EmployeeCode == "throw-user" =>
                throw new InvalidOperationException("Sensitive database detail must not be returned."),
            LoginCommand login when login.EmployeeCode == "valid-user" => IssuedAuthentication(),
            LoginCommand login when login.EmployeeCode == "mfa-user" =>
                AuthenticationResult.MfaRequired(Guid.Parse("33445566-7788-4990-aabb-ccddeeff0011")),
            LoginCommand => AuthenticationResult.Rejected(AuthenticationFailureCode.InvalidCredentials),
            CompleteMfaLoginCommand mfa when mfa.Code == "123456" => IssuedAuthentication(),
            CompleteMfaLoginCommand => AuthenticationResult.Rejected(AuthenticationFailureCode.MfaInvalid),
            RefreshSessionCommand => IssuedAuthentication(),
            LogoutCommand => OperationResult.Success,
            SetPinCommand => OperationResult.Success,
            TerminalVerificationCommand terminal when terminal.EmployeeCode == "valid-terminal" =>
                TerminalVerificationResult.Verified(42, terminal.EmployeeCode, "Terminal Operator", ["pts.shopfloor.supervise"]),
            TerminalVerificationCommand => TerminalVerificationResult.Rejected(
                TerminalVerificationFailureCode.InvalidCredentials),
            GetSigningMetadataQuery => new SigningMetadata(
                IdentityApiFactory.Issuer,
                new JsonWebKey("RSA", "sig", "test-key", "RS256", modulus, exponent)),
            SearchOrganizationUnitsQuery query => new PagedOrganizationUnits(query.Skip, query.Take, 1, [OrganizationUnit()]),
            GetOrganizationUnitQuery query when query.OrganizationId == 999 => throw new KeyNotFoundException(),
            GetOrganizationUnitQuery => OrganizationUnit(),
            CreateOrganizationCommand command when command.Code == "DUPLICATE" => throw new OrganizationCodeConflictException(),
            CreateOrganizationCommand => new OrganizationHierarchyResult(10, 100, Identity.Domain.Enums.OrganizationUnitType.Organization, "/100/", new byte[8]),
            CreateOrganizationUnitCommand command => new OrganizationHierarchyResult(command.OrganizationId, 101, command.UnitType, "/100/101/", new byte[8]),
            UpdateOrganizationUnitCommand command when command.Name == "STALE" => throw new OrganizationConcurrencyException(),
            UpdateOrganizationUnitCommand => OrganizationUnit(),
            SetOrganizationUnitActiveCommand => OrganizationUnit(),
            CreateUserCommand => new AdministrationResult("User", 123, 123, null, null),
            UpdateUserProfileCommand profile => new AdministrationResult("User", profile.UserId, profile.UserId, null, null),
            GetAdministrationDashboardQuery => Dashboard(),
            SearchAdministrationApplicationsQuery query => new PagedAdministrationApplications(
                query.Skip,
                query.Take,
                1,
                [Application()]),
            SearchAdministrationUsersQuery query => new PagedAdministrationUsers(
                query.Skip,
                query.Take,
                1,
                [User()]),
            GetAdministrationApplicationCatalogQuery => Catalog(),
            SearchAdministrationApplicationUsersQuery query => new PagedAdministrationApplicationUsers(
                query.Skip,
                query.Take,
                1,
                [ApplicationUser()]),
            GetAdministrationUserAccessQuery => UserAccess(),
            GetAdministrationApplicationAccessQuery => ApplicationAccess(),
            GetAdministrationUserSecurityQuery => UserSecurity(),
            GetAdministrationSecurityOperationsQuery query => new AdministrationSecurityOperations(
                query.Skip, query.Take, 1,
                [Dashboard().RecentAuditEvents[0]],
                [new AdministrationSessionSummary(
                    Guid.Parse("11223344-5566-4777-8899-aabbccddeeff"), 42, 10, 20, null,
                    DateTime.UtcNow, DateTime.UtcNow.AddDays(1), null, null, true)],
                DateTime.UtcNow),
            RevokeSessionFamilyCommand => OperationResult.Success,
            StageBulkBatchCommand stage => BulkSummary(stage.Rows.Count),
            GetBulkBatchQuery query when query.BatchKey == MissingBatchKey =>
                throw new KeyNotFoundException(),
            GetBulkBatchQuery query => new BulkBatchPage(
                BulkSummary(1), query.Skip, query.Take, 1, [BulkRowResult()]),
            CorrectBulkRowCommand => BulkRowResult(),
            CommitBulkBatchCommand command =>
                new BulkCommitResult(command.BatchKey, 2, 1, 1, AlreadyCommitted: false),
            DiscardBulkBatchCommand command => new BulkDiscardResult(command.BatchKey, 3),
            _ => throw new NotSupportedException($"Test dispatcher does not support {request.GetType().Name}."),
        };
        return ValueTask.FromResult((TResponse)response);
    }

    public static readonly Guid BatchKey = Guid.Parse("aa11bb22-cc33-4d44-8e55-ff6677889900");
    public static readonly Guid MissingBatchKey = Guid.Parse("00000000-0000-4000-8000-000000000001");

    private static BulkBatchSummary BulkSummary(int rows) => new(
        BatchKey, "users", Identity.Domain.Enums.BulkImportSource.Paste, null,
        Identity.Domain.Enums.BulkImportBatchState.Staged,
        DateTime.UtcNow, DateTime.UtcNow.AddHours(24), rows, rows, 0, 0, 0);

    private static BulkStagedRow BulkRowResult() => new(
        3,
        Identity.Domain.Enums.BulkImportRowState.Invalid,
        new Dictionary<string, string?> { ["employeeCode"] = "INDE05513", ["email"] = "bad" },
        [new BulkCellError(3, "email", "email.invalid", "Not a valid email address.")]);

    private static OrganizationUnitDetails OrganizationUnit() => new(10, 100, null,
        Identity.Domain.Enums.OrganizationUnitType.Organization, "ORG", "Organization", null, new(), "/100/", true, DateTime.UtcNow, null, new byte[8]);

    private static AuthenticationResult IssuedAuthentication() => AuthenticationResult.Issued(
        new IssuedAccessToken("signed-access-token", DateTime.UtcNow.AddMinutes(5)),
        "opaque-refresh-token-value-with-required-length",
        DateTime.UtcNow.AddDays(7),
        3);

    private static AdministrationDashboard Dashboard()
    {
        var occurredAt = DateTime.Parse(
            "2026-08-29T12:00:00Z",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal);
        return new AdministrationDashboard(
            1,
            1,
            1,
            1,
            2,
            0,
            [Application()],
            [User()],
            [new AdministrationAuditSummary(
                30,
                42,
                10,
                "LoginSucceeded",
                true,
                null,
                Guid.Parse("8d7a3dbb-4926-47ca-9439-e9fda4b8921e"),
                occurredAt)],
            occurredAt);
    }

    private static AdministrationApplicationSummary Application() => new(
        10,
        "iam-administration",
        "Identity Administration",
        IdentityApiFactory.AdministrationAudience,
        true,
        DateTime.Parse("2026-08-28T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        null);

    private static AdministrationUserSummary User() => new(
        42,
        "ADMIN001",
        "Identity Administrator",
        true,
        1,
        null,
        null,
        DateTime.Parse("2026-08-28T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        null);

    private static AdministrationApplicationUserSummary ApplicationUser() => new(
        42,
        "ADMIN001",
        "Identity Administrator",
        "admin@example.com",
        true,
        DateTime.UtcNow,
        null,
        ["identity-administrator"]);

    private static AdministrationApplicationCatalog Catalog() => new(
        Application(),
        [new AdministrationClientSummary(
            20, 10, "identity-admin-web", "Administration Web", "Public", 0, true,
            DateTime.UtcNow, null, null)],
        [new AdministrationModuleSummary(
            21, 10, "administration", "Administration", null, null, 0, true, true,
            DateTime.UtcNow, null)],
        [new AdministrationCapabilitySummary(
            22, 10, 21, "iam.admin", "Identity administrator", null, true,
            DateTime.UtcNow, null)]);

    private static AdministrationUserAccessCatalog UserAccess() => new(
        User(),
        [new AdministrationUserApplicationSummary(
            42, 10, "iam-administration", "Identity Administration", true, 3,
            DateTime.UtcNow, null, ["iam.admin"])],
        [new AdministrationUserRoleSummary(
            40, 42, 10, 30, "identity-administrator", "Identity Administrator",
            DateTime.UtcNow, null)],
        [new AdministrationUserOverrideSummary(
            50, 42, 10, 22, "iam.audit", "Deny", "Test deny", DateTime.UtcNow, null, null)],
        DateTime.UtcNow);

    private static AdministrationApplicationAccessCatalog ApplicationAccess() => new(
        10,
        [new AdministrationRoleSummary(
            30, 10, "identity-administrator", "Identity Administrator", null, true, true)],
        [new AdministrationRolePermissionSummary(
            31, 10, 30, 22, "iam.admin", DateTime.UtcNow, null)],
        Catalog().Capabilities);

    private static AdministrationUserSecurityCatalog UserSecurity() => new(
        User(),
        [new AdministrationCredentialSummary(60, "Password", DateTime.UtcNow, null, null)],
        [new AdministrationDeviceSummary(
            70, "Shared terminal", "Terminal", true, DateTime.UtcNow.AddDays(30),
            true, DateTime.UtcNow, null, null)],
        [new AdministrationMfaMethodSummary(
            80, "Totp", "Authenticator", true, true, true,
            DateTime.UtcNow, DateTime.UtcNow, null, null)]);
}
