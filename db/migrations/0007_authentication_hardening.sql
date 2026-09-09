/* T017 adds expiring device trust and single-use TOTP time-step persistence. */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'Identity.Device', N'TrustedUntil') IS NULL
BEGIN
    ALTER TABLE [Identity].[Device]
        ADD [TrustedUntil] DATETIME2(3) NULL;
END;
GO

IF COL_LENGTH(N'Identity.UserMfaMethod', N'LastAcceptedTimeStep') IS NULL
BEGIN
    ALTER TABLE [Identity].[UserMfaMethod]
        ADD [LastAcceptedTimeStep] BIGINT NULL;
END;
GO

IF OBJECT_ID(N'Identity.CkDeviceTrustedUntil', N'C') IS NULL
BEGIN
    ALTER TABLE [Identity].[Device] WITH CHECK
        ADD CONSTRAINT [CkDeviceTrustedUntil]
        CHECK ([TrustedUntil] IS NULL OR [TrustedUntil] >= [CreatedAt]);
END;
GO

IF OBJECT_ID(N'Identity.CkUserMfaMethodAcceptedTimeStep', N'C') IS NULL
BEGIN
    ALTER TABLE [Identity].[UserMfaMethod] WITH CHECK
        ADD CONSTRAINT [CkUserMfaMethodAcceptedTimeStep]
        CHECK ([LastAcceptedTimeStep] IS NULL OR [LastAcceptedTimeStep] >= 0);
END;
GO
