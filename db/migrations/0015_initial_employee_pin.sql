SET XACT_ABORT ON;
GO
-- Existing credentials are not reset or marked temporary by this migration.
ALTER TABLE [Identity].[UserCredential]
ADD RequiresChange BIT NOT NULL CONSTRAINT DfUserCredentialRequiresChange DEFAULT (0);
GO
