-- Optional personnel classification; these links never grant authorization.
ALTER TABLE [Identity].[UserAccount] ADD [DepartmentId] BIGINT NULL, [TeamId] BIGINT NULL;

ALTER TABLE [Identity].[Team] ADD CONSTRAINT [UqTeamDepartmentMapping] UNIQUE ([TeamId], [DepartmentId]);
ALTER TABLE [Identity].[UserAccount] ADD
    CONSTRAINT [FkUserDepartment] FOREIGN KEY ([DepartmentId]) REFERENCES [Identity].[Department] ([DepartmentId]),
    CONSTRAINT [FkUserTeamDepartment] FOREIGN KEY ([TeamId], [DepartmentId]) REFERENCES [Identity].[Team] ([TeamId], [DepartmentId]),
    CONSTRAINT [CkUserTeamRequiresDepartment] CHECK ([TeamId] IS NULL OR [DepartmentId] IS NOT NULL);

CREATE INDEX [IxUserDepartmentTeam] ON [Identity].[UserAccount] ([DepartmentId], [TeamId]);
