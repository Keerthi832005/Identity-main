SET XACT_ABORT ON;
GO
CREATE TABLE [Identity].[AgentControlState]
(
    InstallationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PkAgentControlState PRIMARY KEY,
    LastSignedAt DATETIME2(7) NOT NULL,
    LastSeenAt DATETIME2(3) NOT NULL,
    AgentVersion NVARCHAR(50) NOT NULL,
    SupervisorVersion NVARCHAR(50) NOT NULL,
    CONSTRAINT FkAgentControlInstallation FOREIGN KEY (InstallationId) REFERENCES [Identity].[AgentInstallation](InstallationId)
);
GO
CREATE TABLE [Identity].[AgentUpdateRequest]
(
    RequestId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PkAgentUpdateRequest PRIMARY KEY,
    InstallationId UNIQUEIDENTIFIER NOT NULL,
    RequestedByUserId BIGINT NOT NULL,
    CorrelationId UNIQUEIDENTIFIER NOT NULL,
    RequestedAt DATETIME2(3) NOT NULL,
    ExpiresAt DATETIME2(3) NOT NULL,
    DeliveredAt DATETIME2(3) NULL,
    CompletedAt DATETIME2(3) NULL,
    Result NVARCHAR(20) NULL,
    AgentVersion NVARCHAR(50) NULL,
    CONSTRAINT FkAgentUpdateInstallation FOREIGN KEY (InstallationId) REFERENCES [Identity].[AgentInstallation](InstallationId),
    CONSTRAINT FkAgentUpdateActor FOREIGN KEY (RequestedByUserId) REFERENCES [Identity].[UserAccount](UserId),
    CONSTRAINT CkAgentUpdateExpiry CHECK (ExpiresAt > RequestedAt),
    CONSTRAINT CkAgentUpdateResult CHECK ((CompletedAt IS NULL AND Result IS NULL) OR
        (CompletedAt IS NOT NULL AND DeliveredAt IS NOT NULL AND Result IS NOT NULL AND Result IN (N'updated',N'upToDate',N'failed')))
);
CREATE INDEX IxAgentUpdateInstallationTime ON [Identity].[AgentUpdateRequest](InstallationId, RequestedAt DESC);
GO
