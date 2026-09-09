SET XACT_ABORT ON;
GO
-- A worker that gathered its inventory but had the report refused reports "rejected", which is
-- neither a collection failure nor a success.
ALTER TABLE [Identity].[AgentUpdateRequest] DROP CONSTRAINT CkAgentUpdateResult;
GO
ALTER TABLE [Identity].[AgentUpdateRequest] WITH CHECK
    ADD CONSTRAINT CkAgentUpdateResult CHECK ((CompletedAt IS NULL AND Result IS NULL) OR
        (CompletedAt IS NOT NULL AND DeliveredAt IS NOT NULL AND Result IS NOT NULL
            AND Result IN (N'updated',N'upToDate',N'collected',N'rejected',N'failed')));
GO
