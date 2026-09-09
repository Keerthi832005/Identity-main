/* Forward-only: staging for bulk import from an Excel upload or a smart paste. Rows are held and
   validated here; nothing reaches the identity tables until an administrator commits the batch.
   Staged rows carry administrator-supplied identity data, so they inherit the same audit and
   retention obligations as their targets and never hold a password, PIN, OTP or client secret.
   This script never clears or rewrites dbo.SchemaVersions or existing application data. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

CREATE TABLE [Identity].[BulkImportBatch]
(
    [BulkImportBatchId] BIGINT IDENTITY(1,1) NOT NULL,
    -- Client-facing identity. Also the commit idempotency key, so a replayed commit is a no-op.
    [BatchKey] UNIQUEIDENTIFIER NOT NULL,
    [EntityKey] NVARCHAR(60) NOT NULL,
    [TemplateVersion] INT NOT NULL,
    [Source] NVARCHAR(10) NOT NULL,
    [FileName] NVARCHAR(260) NULL,
    [SubmittedByUserId] BIGINT NOT NULL,
    [SubmittedAt] DATETIME2(3) NOT NULL,
    [ExpiresAt] DATETIME2(3) NOT NULL,
    [State] NVARCHAR(12) NOT NULL CONSTRAINT [DfBulkImportBatchState] DEFAULT N'Staged',
    [CommittedAt] DATETIME2(3) NULL,
    [CreatedRowCount] INT NOT NULL CONSTRAINT [DfBulkImportBatchCreatedRows] DEFAULT 0,
    [UpdatedRowCount] INT NOT NULL CONSTRAINT [DfBulkImportBatchUpdatedRows] DEFAULT 0,
    [RowVersion] ROWVERSION NOT NULL,
    CONSTRAINT [PkBulkImportBatch] PRIMARY KEY CLUSTERED ([BulkImportBatchId]),
    CONSTRAINT [UqBulkImportBatchKey] UNIQUE ([BatchKey]),
    CONSTRAINT [FkBulkImportBatchUser] FOREIGN KEY ([SubmittedByUserId])
        REFERENCES [Identity].[UserAccount] ([UserId]),
    CONSTRAINT [CkBulkImportBatchSource] CHECK ([Source] IN (N'Excel', N'Paste')),
    CONSTRAINT [CkBulkImportBatchState] CHECK ([State] IN (N'Staged', N'Committed', N'Discarded')),
    CONSTRAINT [CkBulkImportBatchCommitted] CHECK
        (([State] = N'Committed' AND [CommittedAt] IS NOT NULL)
        OR ([State] <> N'Committed' AND [CommittedAt] IS NULL)),
    CONSTRAINT [CkBulkImportBatchExpiry] CHECK ([ExpiresAt] > [SubmittedAt]),
    CONSTRAINT [CkBulkImportBatchCounts] CHECK ([CreatedRowCount] >= 0 AND [UpdatedRowCount] >= 0)
);
GO

/* A batch is readable only by the administrator who submitted it; this index serves that filter. */
CREATE INDEX [IxBulkImportBatchOwner]
    ON [Identity].[BulkImportBatch] ([SubmittedByUserId], [State], [SubmittedAt] DESC);
GO

CREATE INDEX [IxBulkImportBatchExpiry]
    ON [Identity].[BulkImportBatch] ([ExpiresAt])
    WHERE [State] = N'Staged';
GO

CREATE TABLE [Identity].[BulkImportRow]
(
    [BulkImportRowId] BIGINT IDENTITY(1,1) NOT NULL,
    [BulkImportBatchId] BIGINT NOT NULL,
    -- The row number Excel shows, so an error message quotes what the administrator can see.
    [SourceRowNumber] INT NOT NULL,
    [CellValues] NVARCHAR(MAX) NOT NULL,
    [CellErrors] NVARCHAR(MAX) NULL,
    [State] NVARCHAR(10) NOT NULL,
    [AppliedResourceId] BIGINT NULL,
    [UpdatedAt] DATETIME2(3) NULL,
    CONSTRAINT [PkBulkImportRow] PRIMARY KEY CLUSTERED ([BulkImportRowId]),
    CONSTRAINT [UqBulkImportRowNumber] UNIQUE ([BulkImportBatchId], [SourceRowNumber]),
    CONSTRAINT [FkBulkImportRowBatch] FOREIGN KEY ([BulkImportBatchId])
        REFERENCES [Identity].[BulkImportBatch] ([BulkImportBatchId]) ON DELETE CASCADE,
    CONSTRAINT [CkBulkImportRowState] CHECK
        ([State] IN (N'Create', N'Update', N'Invalid', N'Applied')),
    CONSTRAINT [CkBulkImportRowValuesJson] CHECK (ISJSON([CellValues]) = 1),
    CONSTRAINT [CkBulkImportRowErrorsJson] CHECK ([CellErrors] IS NULL OR ISJSON([CellErrors]) = 1),
    CONSTRAINT [CkBulkImportRowErrorState] CHECK
        (([State] = N'Invalid' AND [CellErrors] IS NOT NULL)
        OR ([State] <> N'Invalid' AND [CellErrors] IS NULL)),
    CONSTRAINT [CkBulkImportRowApplied] CHECK
        ([State] = N'Applied' OR [AppliedResourceId] IS NULL),
    CONSTRAINT [CkBulkImportRowSourceRow] CHECK ([SourceRowNumber] > 0)
);
GO

/* The preview pages by status, which is the only listing the workspace performs. */
CREATE INDEX [IxBulkImportRowBatchState]
    ON [Identity].[BulkImportRow] ([BulkImportBatchId], [State], [SourceRowNumber]);
GO

DECLARE @existingDefinition NVARCHAR(MAX) = (
    SELECT [definition] FROM sys.check_constraints
    WHERE [parent_object_id] = OBJECT_ID(N'Identity.AuthenticationAudit')
      AND [name] = N'CkAuthenticationAuditEventType'
);
IF @existingDefinition IS NULL
    THROW 51011, 'Authentication audit event constraint is missing.', 1;

ALTER TABLE [Identity].[AuthenticationAudit] DROP CONSTRAINT [CkAuthenticationAuditEventType];
DECLARE @constraintSql NVARCHAR(MAX) =
    N'ALTER TABLE [Identity].[AuthenticationAudit] WITH CHECK ADD CONSTRAINT '
    + N'[CkAuthenticationAuditEventType] CHECK ((' + @existingDefinition
    + N') OR [EventType] IN (N''BulkImportSubmitted'', N''BulkImportRowCorrected'', '
    + N'N''BulkImportCommitted'', N''BulkImportDiscarded'', N''BulkImportExpired''));';
EXEC sys.sp_executesql @constraintSql;
GO
