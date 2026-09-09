-- Optional personnel branch assignment. Location remains asset-only.
ALTER TABLE [Identity].[UserAccount] ADD [BranchId] BIGINT NULL;
GO

ALTER TABLE [Identity].[UserAccount] ADD
    CONSTRAINT [FkUserBranch] FOREIGN KEY ([BranchId]) REFERENCES [Identity].[Branch] ([BranchId]);
GO

CREATE INDEX [IxUserAccountBranch] ON [Identity].[UserAccount] ([BranchId]);
GO

EXEC sys.sp_addextendedproperty
    @name=N'MS_Description',
    @value=N'Optional employee branch. State, region and country are derived through the Branch hierarchy; Location is reserved for assets.',
    @level0type=N'SCHEMA', @level0name=N'Identity',
    @level1type=N'TABLE', @level1name=N'UserAccount',
    @level2type=N'COLUMN', @level2name=N'BranchId';
GO
