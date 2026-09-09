SET XACT_ABORT ON;
GO
-- The worker signs its own collect channel; the supervisor channel keeps AgentControlState.
CREATE TABLE [Identity].[AgentCollectState]
(
    InstallationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PkAgentCollectState PRIMARY KEY,
    LastSignedAt DATETIME2(7) NOT NULL,
    LastSeenAt DATETIME2(3) NOT NULL,
    AgentVersion NVARCHAR(50) NOT NULL,
    CONSTRAINT FkAgentCollectInstallation FOREIGN KEY (InstallationId) REFERENCES [Identity].[AgentInstallation](InstallationId)
);
GO
