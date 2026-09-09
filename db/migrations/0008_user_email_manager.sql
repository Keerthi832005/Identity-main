/* Contact email and optional reporting manager. Existing users remain valid.
   Do not delete, truncate, reseed, or rewrite dbo.SchemaVersions or any existing migration. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

ALTER TABLE [Identity].[UserAccount]
    ADD [Email] NVARCHAR(254) NULL,
        [ManagerUserId] BIGINT NULL;
GO

ALTER TABLE [Identity].[UserAccount] WITH CHECK
    ADD CONSTRAINT [FkUserAccountManager]
        FOREIGN KEY ([ManagerUserId]) REFERENCES [Identity].[UserAccount] ([UserId]),
        CONSTRAINT [CkUserAccountManagerNotSelf]
        CHECK ([ManagerUserId] IS NULL OR [ManagerUserId] <> [UserId]),
        CONSTRAINT [CkUserAccountEmailNotBlank]
        CHECK ([Email] IS NULL OR LEN(LTRIM(RTRIM([Email]))) > 0);
GO

CREATE INDEX [IxUserAccountManagerUserId]
    ON [Identity].[UserAccount] ([ManagerUserId]);
GO

/* Extend the deployed allow-list without losing any previously supported audit event. */
DECLARE @existingDefinition NVARCHAR(MAX) = (
    SELECT [definition] FROM sys.check_constraints
    WHERE [parent_object_id] = OBJECT_ID(N'Identity.AuthenticationAudit')
      AND [name] = N'CkAuthenticationAuditEventType'
);
IF @existingDefinition IS NULL
    THROW 51009, 'Authentication audit event constraint is missing.', 1;

ALTER TABLE [Identity].[AuthenticationAudit] DROP CONSTRAINT [CkAuthenticationAuditEventType];
DECLARE @constraintSql NVARCHAR(MAX) =
    N'ALTER TABLE [Identity].[AuthenticationAudit] WITH CHECK ADD CONSTRAINT '
    + N'[CkAuthenticationAuditEventType] CHECK ((' + @existingDefinition
    + N') OR [EventType] = N''UserProfileUpdated'');';
EXEC sys.sp_executesql @constraintSql;
GO
