USE [IAM_LocalTest];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

/* Keep this at 0 for the first run. Change to 1 only after checking the preview. */
DECLARE @Apply BIT = 0;
DECLARE @KeepEmployeeCode NVARCHAR(50) = N'04203';
DECLARE @KeepApplicationCode NVARCHAR(100) = N'iam-administration';
DECLARE @KeepUserId BIGINT;
DECLARE @KeepApplicationId BIGINT;

SELECT @KeepUserId = UserId
FROM [Identity].[UserAccount]
WHERE NormalizedEmployeeCode = UPPER(LTRIM(RTRIM(@KeepEmployeeCode)));

SELECT @KeepApplicationId = ApplicationId
FROM [Identity].[Application]
WHERE NormalizedApplicationCode = UPPER(LTRIM(RTRIM(@KeepApplicationCode)));

IF @KeepUserId IS NULL
    THROW 51000, 'Almas Ahamed (04203) was not found. Nothing was deleted.', 1;

IF @KeepApplicationId IS NULL
    THROW 51001, 'The iam-administration application was not found. Nothing was deleted.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM [Identity].[UserApplication]
    WHERE UserId = @KeepUserId
      AND ApplicationId = @KeepApplicationId
      AND RevokedAt IS NULL
)
    THROW 51002, 'Almas does not have active IAM administration access. Reset refused.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM [Identity].[UserRole] AS userRole
    JOIN [Identity].[Role] AS role
      ON role.RoleId = userRole.RoleId
     AND role.ApplicationId = userRole.ApplicationId
    WHERE userRole.UserId = @KeepUserId
      AND userRole.ApplicationId = @KeepApplicationId
      AND userRole.RevokedAt IS NULL
      AND role.NormalizedRoleCode = N'IDENTITY-ADMINISTRATOR'
      AND role.IsActive = 1
)
    THROW 51003, 'Almas does not have the active identity-administrator role. Reset refused.', 1;

DROP TABLE IF EXISTS #DeleteUsers;
DROP TABLE IF EXISTS #DeleteApplications;
DROP TABLE IF EXISTS #DeleteDevices;
DROP TABLE IF EXISTS #DeleteInstallations;

CREATE TABLE #DeleteUsers (UserId BIGINT NOT NULL PRIMARY KEY);
CREATE TABLE #DeleteApplications (ApplicationId BIGINT NOT NULL PRIMARY KEY);
CREATE TABLE #DeleteDevices (DeviceId BIGINT NOT NULL PRIMARY KEY);
CREATE TABLE #DeleteInstallations (InstallationId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY);

INSERT #DeleteUsers (UserId)
SELECT UserId
FROM [Identity].[UserAccount]
WHERE UserId <> @KeepUserId;

INSERT #DeleteApplications (ApplicationId)
SELECT ApplicationId
FROM [Identity].[Application]
WHERE ApplicationId <> @KeepApplicationId;

INSERT #DeleteDevices (DeviceId)
SELECT device.DeviceId
FROM [Identity].[Device] AS device
JOIN #DeleteUsers AS deleted ON deleted.UserId = device.UserId;

INSERT #DeleteInstallations (InstallationId)
SELECT installation.InstallationId
FROM [Identity].[AgentInstallation] AS installation
JOIN #DeleteDevices AS deleted ON deleted.DeviceId = installation.DeviceId;

DECLARE @UsersToDelete INT = (SELECT COUNT(*) FROM #DeleteUsers);
DECLARE @ApplicationsToDelete INT = (SELECT COUNT(*) FROM #DeleteApplications);
DECLARE @OrganizationsToDelete INT = (SELECT COUNT(*) FROM [Identity].[Organization]);

/* Preview. */
SELECT N'PRESERVE USER' AS Action, UserId, EmployeeCode, DisplayName
FROM [Identity].[UserAccount]
WHERE UserId = @KeepUserId;

SELECT N'PRESERVE APPLICATION' AS Action, ApplicationId, ApplicationCode, ApplicationName
FROM [Identity].[Application]
WHERE ApplicationId = @KeepApplicationId;

SELECT N'DELETE USER' AS Action, account.UserId, account.EmployeeCode, account.DisplayName
FROM [Identity].[UserAccount] AS account
JOIN #DeleteUsers AS deleted ON deleted.UserId = account.UserId
ORDER BY account.EmployeeCode;

SELECT N'DELETE APPLICATION' AS Action, application.ApplicationId,
       application.ApplicationCode, application.ApplicationName
FROM [Identity].[Application] AS application
JOIN #DeleteApplications AS deleted ON deleted.ApplicationId = application.ApplicationId
ORDER BY application.ApplicationCode;

BEGIN TRY
    BEGIN TRANSACTION;

    /* Detach every user, including Almas, from organization records being removed. */
    UPDATE [Identity].[UserAccount]
    SET ManagerUserId = NULL,
        DepartmentId = NULL,
        TeamId = NULL,
        BranchId = NULL,
        UpdatedAt = SYSUTCDATETIME();

    /* Retained records must not reference an actor who is about to be deleted. */
    UPDATE credential
    SET CreatedByUserId = @KeepUserId
    FROM [Identity].[UserCredential] AS credential
    JOIN #DeleteUsers AS actor ON actor.UserId = credential.CreatedByUserId
    WHERE credential.UserId = @KeepUserId;

    UPDATE device
    SET RevokedByUserId = @KeepUserId
    FROM [Identity].[Device] AS device
    JOIN #DeleteUsers AS actor ON actor.UserId = device.RevokedByUserId
    WHERE device.UserId = @KeepUserId;

    UPDATE device
    SET TerminalServiceUserId = NULL
    FROM [Identity].[Device] AS device
    JOIN #DeleteUsers AS actor ON actor.UserId = device.TerminalServiceUserId;

    UPDATE accessGrant
    SET AssignedByUserId = CASE WHEN assigned.UserId IS NULL THEN accessGrant.AssignedByUserId ELSE @KeepUserId END,
        UpdatedByUserId = CASE WHEN updated.UserId IS NULL THEN accessGrant.UpdatedByUserId ELSE @KeepUserId END,
        RevokedByUserId = CASE WHEN revoked.UserId IS NULL THEN accessGrant.RevokedByUserId ELSE @KeepUserId END
    FROM [Identity].[UserApplication] AS accessGrant
    LEFT JOIN #DeleteUsers AS assigned ON assigned.UserId = accessGrant.AssignedByUserId
    LEFT JOIN #DeleteUsers AS updated ON updated.UserId = accessGrant.UpdatedByUserId
    LEFT JOIN #DeleteUsers AS revoked ON revoked.UserId = accessGrant.RevokedByUserId
    WHERE accessGrant.UserId = @KeepUserId
      AND accessGrant.ApplicationId = @KeepApplicationId
      AND (assigned.UserId IS NOT NULL OR updated.UserId IS NOT NULL OR revoked.UserId IS NOT NULL);

    UPDATE userRole
    SET AssignedByUserId = CASE WHEN assigned.UserId IS NULL THEN userRole.AssignedByUserId ELSE @KeepUserId END,
        RevokedByUserId = CASE WHEN revoked.UserId IS NULL THEN userRole.RevokedByUserId ELSE @KeepUserId END
    FROM [Identity].[UserRole] AS userRole
    LEFT JOIN #DeleteUsers AS assigned ON assigned.UserId = userRole.AssignedByUserId
    LEFT JOIN #DeleteUsers AS revoked ON revoked.UserId = userRole.RevokedByUserId
    WHERE userRole.UserId = @KeepUserId
      AND userRole.ApplicationId = @KeepApplicationId
      AND (assigned.UserId IS NOT NULL OR revoked.UserId IS NOT NULL);

    UPDATE permissionOverride
    SET AssignedByUserId = CASE WHEN assigned.UserId IS NULL THEN permissionOverride.AssignedByUserId ELSE @KeepUserId END,
        RevokedByUserId = CASE WHEN revoked.UserId IS NULL THEN permissionOverride.RevokedByUserId ELSE @KeepUserId END
    FROM [Identity].[UserPermissionOverride] AS permissionOverride
    LEFT JOIN #DeleteUsers AS assigned ON assigned.UserId = permissionOverride.AssignedByUserId
    LEFT JOIN #DeleteUsers AS revoked ON revoked.UserId = permissionOverride.RevokedByUserId
    WHERE permissionOverride.UserId = @KeepUserId
      AND permissionOverride.ApplicationId = @KeepApplicationId
      AND (assigned.UserId IS NOT NULL OR revoked.UserId IS NOT NULL);

    UPDATE rolePermission
    SET GrantedByUserId = CASE WHEN granted.UserId IS NULL THEN rolePermission.GrantedByUserId ELSE @KeepUserId END,
        RevokedByUserId = CASE WHEN revoked.UserId IS NULL THEN rolePermission.RevokedByUserId ELSE @KeepUserId END
    FROM [Identity].[RolePermission] AS rolePermission
    LEFT JOIN #DeleteUsers AS granted ON granted.UserId = rolePermission.GrantedByUserId
    LEFT JOIN #DeleteUsers AS revoked ON revoked.UserId = rolePermission.RevokedByUserId
    WHERE rolePermission.ApplicationId = @KeepApplicationId
      AND (granted.UserId IS NOT NULL OR revoked.UserId IS NOT NULL);

    /* Bulk-import child rows must be removed before their batches. */
    DELETE importRow
    FROM [Identity].[BulkImportRow] AS importRow
    JOIN [Identity].[BulkImportBatch] AS batch
      ON batch.BulkImportBatchId = importRow.BulkImportBatchId
    JOIN #DeleteUsers AS deleted ON deleted.UserId = batch.SubmittedByUserId;

    DELETE batch
    FROM [Identity].[BulkImportBatch] AS batch
    JOIN #DeleteUsers AS deleted ON deleted.UserId = batch.SubmittedByUserId;

    /* Remove agent records connected to devices owned by deleted users. */
    DELETE request
    FROM [Identity].[AgentUpdateRequest] AS request
    LEFT JOIN #DeleteInstallations AS installation
      ON installation.InstallationId = request.InstallationId
    LEFT JOIN #DeleteUsers AS actor ON actor.UserId = request.RequestedByUserId
    WHERE installation.InstallationId IS NOT NULL OR actor.UserId IS NOT NULL;

    DELETE state
    FROM [Identity].[AgentCollectState] AS state
    JOIN #DeleteInstallations AS deleted
      ON deleted.InstallationId = state.InstallationId;

    DELETE state
    FROM [Identity].[AgentControlState] AS state
    JOIN #DeleteInstallations AS deleted
      ON deleted.InstallationId = state.InstallationId;

    DELETE installation
    FROM [Identity].[AgentInstallation] AS installation
    JOIN #DeleteInstallations AS deleted
      ON deleted.InstallationId = installation.InstallationId;

    /* Delete session and authorization records for deleted users or applications. */
    DELETE challenge
    FROM [Identity].[MfaChallenge] AS challenge
    LEFT JOIN #DeleteUsers AS deletedUser ON deletedUser.UserId = challenge.UserId
    LEFT JOIN #DeleteApplications AS deletedApp ON deletedApp.ApplicationId = challenge.ApplicationId
    WHERE deletedUser.UserId IS NOT NULL OR deletedApp.ApplicationId IS NOT NULL;

    DELETE token
    FROM [Identity].[RefreshToken] AS token
    LEFT JOIN #DeleteUsers AS deletedUser ON deletedUser.UserId = token.UserId
    LEFT JOIN #DeleteApplications AS deletedApp ON deletedApp.ApplicationId = token.ApplicationId
    WHERE deletedUser.UserId IS NOT NULL OR deletedApp.ApplicationId IS NOT NULL;

    DELETE permissionOverride
    FROM [Identity].[UserPermissionOverride] AS permissionOverride
    LEFT JOIN #DeleteUsers AS deletedUser ON deletedUser.UserId = permissionOverride.UserId
    LEFT JOIN #DeleteApplications AS deletedApp ON deletedApp.ApplicationId = permissionOverride.ApplicationId
    WHERE deletedUser.UserId IS NOT NULL OR deletedApp.ApplicationId IS NOT NULL;

    DELETE userRole
    FROM [Identity].[UserRole] AS userRole
    LEFT JOIN #DeleteUsers AS deletedUser ON deletedUser.UserId = userRole.UserId
    LEFT JOIN #DeleteApplications AS deletedApp ON deletedApp.ApplicationId = userRole.ApplicationId
    WHERE deletedUser.UserId IS NOT NULL OR deletedApp.ApplicationId IS NOT NULL;

    DELETE accessGrant
    FROM [Identity].[UserApplication] AS accessGrant
    LEFT JOIN #DeleteUsers AS deletedUser ON deletedUser.UserId = accessGrant.UserId
    LEFT JOIN #DeleteApplications AS deletedApp ON deletedApp.ApplicationId = accessGrant.ApplicationId
    WHERE deletedUser.UserId IS NOT NULL OR deletedApp.ApplicationId IS NOT NULL;

    /* AuthenticationAudit is append-only outside this local reset. */
    DISABLE TRIGGER [Identity].[TrAuthenticationAuditAppendOnly]
    ON [Identity].[AuthenticationAudit];

    DELETE audit
    FROM [Identity].[AuthenticationAudit] AS audit
    LEFT JOIN #DeleteUsers AS deletedUser ON deletedUser.UserId = audit.UserId
    LEFT JOIN #DeleteApplications AS deletedApp ON deletedApp.ApplicationId = audit.ApplicationId
    WHERE deletedUser.UserId IS NOT NULL OR deletedApp.ApplicationId IS NOT NULL;

    ENABLE TRIGGER [Identity].[TrAuthenticationAuditAppendOnly]
    ON [Identity].[AuthenticationAudit];

    DELETE method
    FROM [Identity].[UserMfaMethod] AS method
    JOIN #DeleteUsers AS deleted ON deleted.UserId = method.UserId;

    DELETE device
    FROM [Identity].[Device] AS device
    JOIN #DeleteUsers AS deleted ON deleted.UserId = device.UserId;

    DELETE credential
    FROM [Identity].[UserCredential] AS credential
    JOIN #DeleteUsers AS deleted ON deleted.UserId = credential.UserId;

    DELETE account
    FROM [Identity].[UserAccount] AS account
    JOIN #DeleteUsers AS deleted ON deleted.UserId = account.UserId;

    /* Delete non-administration application catalogues from child to parent. */
    DELETE rolePermission
    FROM [Identity].[RolePermission] AS rolePermission
    JOIN #DeleteApplications AS deleted ON deleted.ApplicationId = rolePermission.ApplicationId;

    DELETE role
    FROM [Identity].[Role] AS role
    JOIN #DeleteApplications AS deleted ON deleted.ApplicationId = role.ApplicationId;

    DELETE capability
    FROM [Identity].[ModuleCapability] AS capability
    JOIN #DeleteApplications AS deleted ON deleted.ApplicationId = capability.ApplicationId;

    DELETE module
    FROM [Identity].[ApplicationModule] AS module
    JOIN #DeleteApplications AS deleted ON deleted.ApplicationId = module.ApplicationId;

    DELETE client
    FROM [Identity].[ApplicationClient] AS client
    JOIN #DeleteApplications AS deleted ON deleted.ApplicationId = client.ApplicationId;

    DELETE application
    FROM [Identity].[Application] AS application
    JOIN #DeleteApplications AS deleted ON deleted.ApplicationId = application.ApplicationId;

    /* Delete organization hierarchy from child to parent. */
    DELETE FROM [Identity].[Location];
    DELETE FROM [Identity].[Team];
    DELETE FROM [Identity].[Department];
    DELETE FROM [Identity].[Branch];
    DELETE FROM [Identity].[State];
    DELETE FROM [Identity].[Region];
    DELETE FROM [Identity].[Country];
    DELETE FROM [Identity].[OrganizationUnit];
    DELETE FROM [Identity].[Organization];

    IF @Apply = 1
        COMMIT TRANSACTION;
    ELSE
        ROLLBACK TRANSACTION;

    SELECT CASE WHEN @Apply = 1 THEN N'COMMITTED' ELSE N'DRY RUN - ROLLED BACK' END AS Result,
           @KeepEmployeeCode AS PreservedEmployeeCode,
           @KeepApplicationCode AS PreservedApplicationCode,
           @UsersToDelete AS UsersTargeted,
           @ApplicationsToDelete AS ApplicationsTargeted,
           @OrganizationsToDelete AS OrganizationsTargeted;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;

    IF EXISTS
    (
        SELECT 1
        FROM sys.triggers
        WHERE name = N'TrAuthenticationAuditAppendOnly'
          AND is_disabled = 1
    )
        ENABLE TRIGGER [Identity].[TrAuthenticationAuditAppendOnly]
        ON [Identity].[AuthenticationAudit];

    DROP TABLE IF EXISTS #DeleteInstallations;
    DROP TABLE IF EXISTS #DeleteDevices;
    DROP TABLE IF EXISTS #DeleteApplications;
    DROP TABLE IF EXISTS #DeleteUsers;
    THROW;
END CATCH;

/* Final verification. */
SELECT (SELECT COUNT(*) FROM [Identity].[UserAccount]) AS RemainingUsers,
       (SELECT COUNT(*) FROM [Identity].[Application]) AS RemainingApplications,
       (SELECT COUNT(*) FROM [Identity].[Organization]) AS RemainingOrganizations,
       (SELECT is_disabled FROM sys.triggers
        WHERE name = N'TrAuthenticationAuditAppendOnly') AS AuditTriggerDisabled;

SELECT UserId, EmployeeCode, DisplayName, IsActive
FROM [Identity].[UserAccount]
ORDER BY UserId;

SELECT ApplicationId, ApplicationCode, ApplicationName, IsActive
FROM [Identity].[Application]
ORDER BY ApplicationId;

DROP TABLE IF EXISTS #DeleteInstallations;
DROP TABLE IF EXISTS #DeleteDevices;
DROP TABLE IF EXISTS #DeleteApplications;
DROP TABLE IF EXISTS #DeleteUsers;
GO
