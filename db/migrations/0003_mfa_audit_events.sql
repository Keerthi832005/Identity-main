/*
    Forward-only migration 0003: add the explicit MFA enrollment verification rejection event.
*/
ALTER TABLE [Identity].[AuthenticationAudit]
    DROP CONSTRAINT [CkAuthenticationAuditEventType];
GO

ALTER TABLE [Identity].[AuthenticationAudit]
    ADD CONSTRAINT [CkAuthenticationAuditEventType]
        CHECK ([EventType] IN
        (
            N'LoginSucceeded', N'LoginFailed', N'AccountLocked',
            N'TokenRefreshed', N'TokenRefreshRejected', N'LoggedOut',
            N'ClientAuthenticated', N'ClientAuthenticationFailed',
            N'CredentialChanged', N'UserCreated', N'AccountEnabled', N'AccountDisabled',
            N'DeviceRegistered', N'DeviceTrusted', N'DeviceRevoked',
            N'MfaMethodAdded', N'MfaMethodVerified', N'MfaMethodVerificationRejected',
            N'MfaMethodRevoked', N'MfaChallengeCreated', N'MfaChallengeVerified',
            N'MfaChallengeRejected',
            N'UserApplicationAssigned', N'UserApplicationRevoked',
            N'RoleCreated', N'RoleUpdated', N'RoleDisabled',
            N'RoleAssigned', N'RoleRevoked', N'RolePermissionRevoked',
            N'PermissionAllowed', N'PermissionDenied', N'PermissionOverrideRevoked',
            N'ApplicationCreated', N'ApplicationUpdated', N'ApplicationDisabled',
            N'ApplicationClientCreated', N'ApplicationClientRotated',
            N'ApplicationClientRevoked',
            N'ModuleCreated', N'ModuleUpdated', N'ModuleDisabled',
            N'CapabilityCreated', N'CapabilityUpdated', N'CapabilityDisabled'
        ));
GO
