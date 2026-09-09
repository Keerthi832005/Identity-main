/*
    Public clients have no client secret and therefore use secret version zero.
    Confidential and service clients always carry a positive secret version.
*/
ALTER TABLE [Identity].[ApplicationClient]
    DROP CONSTRAINT [CkApplicationClientSecretVersion];
GO

ALTER TABLE [Identity].[ApplicationClient]
    DROP CONSTRAINT [DfApplicationClientSecretVersion];
GO

ALTER TABLE [Identity].[ApplicationClient]
    ADD CONSTRAINT [CkApplicationClientSecretVersion]
        CHECK (([ClientType] = N'Public' AND [SecretVersion] = 0)
            OR ([ClientType] IN (N'Confidential', N'Service') AND [SecretVersion] > 0));
GO
