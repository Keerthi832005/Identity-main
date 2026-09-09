/* Forward-only extension: shared-detail and active-state organization audit events.
   Existing migration scripts and history are never rewritten. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
DECLARE @existingDefinition NVARCHAR(MAX) = (
    SELECT [definition] FROM sys.check_constraints
    WHERE [parent_object_id] = OBJECT_ID(N'Identity.AuthenticationAudit')
      AND [name] = N'CkAuthenticationAuditEventType'
);
IF @existingDefinition IS NULL
    THROW 51010, 'Authentication audit event constraint is missing.', 1;

ALTER TABLE [Identity].[AuthenticationAudit] DROP CONSTRAINT [CkAuthenticationAuditEventType];
DECLARE @constraintSql NVARCHAR(MAX) =
    N'ALTER TABLE [Identity].[AuthenticationAudit] WITH CHECK ADD CONSTRAINT '
    + N'[CkAuthenticationAuditEventType] CHECK ((' + @existingDefinition
    + N') OR [EventType] IN (N''OrganizationUnitUpdated'', N''OrganizationUnitStateChanged''));';
EXEC sys.sp_executesql @constraintSql;
GO
