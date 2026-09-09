SET XACT_ABORT ON;
GO
-- On-demand inventory collection reuses the agent command table; existing rows are update requests.
ALTER TABLE [Identity].[AgentUpdateRequest]
    ADD Kind NVARCHAR(20) NOT NULL CONSTRAINT DfAgentUpdateKind DEFAULT N'update';
GO
ALTER TABLE [Identity].[AgentUpdateRequest] DROP CONSTRAINT CkAgentUpdateResult;
GO
ALTER TABLE [Identity].[AgentUpdateRequest] WITH CHECK
    ADD CONSTRAINT CkAgentUpdateKind CHECK (Kind IN (N'update',N'collect'));
GO
ALTER TABLE [Identity].[AgentUpdateRequest] WITH CHECK
    ADD CONSTRAINT CkAgentUpdateResult CHECK ((CompletedAt IS NULL AND Result IS NULL) OR
        (CompletedAt IS NOT NULL AND DeliveredAt IS NOT NULL AND Result IS NOT NULL
            AND Result IN (N'updated',N'upToDate',N'collected',N'failed')));
GO
CREATE INDEX IxAgentUpdateInstallationKindTime
    ON [Identity].[AgentUpdateRequest](InstallationId, Kind, RequestedAt DESC);
GO
