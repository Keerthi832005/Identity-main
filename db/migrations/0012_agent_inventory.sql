SET XACT_ABORT ON;
GO
CREATE TABLE [Identity].[AgentInstallation]
(
    InstallationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PkAgentInstallation PRIMARY KEY,
    DeviceId BIGINT NOT NULL CONSTRAINT UqAgentInstallationDevice UNIQUE,
    Hostname NVARCHAR(200) NOT NULL,
    PublicKey NVARCHAR(256) NOT NULL,
    AgentVersion NVARCHAR(50) NULL,
    CreatedAt DATETIME2(3) NOT NULL,
    LastCapturedAt DATETIME2(7) NULL,
    LastReportAt DATETIME2(3) NULL,
    InventoryJson NVARCHAR(MAX) NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT FkAgentInstallationDevice FOREIGN KEY (DeviceId) REFERENCES [Identity].[Device](DeviceId),
    CONSTRAINT CkAgentInventoryJson CHECK (InventoryJson IS NULL OR ISJSON(InventoryJson)=1)
);
GO
