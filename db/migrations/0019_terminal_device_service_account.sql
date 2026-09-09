SET XACT_ABORT ON;
GO
-- A trusted Terminal device attests itself with its agent key instead of a typed password.
-- The token is issued for this account, never for the administrator who enrolled the device,
-- so a terminal can never inherit administrative rights. NULL means attestation is refused.
ALTER TABLE [Identity].[Device]
ADD TerminalServiceUserId BIGINT NULL;
GO
ALTER TABLE [Identity].[Device]
ADD CONSTRAINT FkDeviceTerminalServiceUser
    FOREIGN KEY (TerminalServiceUserId) REFERENCES [Identity].[UserAccount](UserId);
GO
CREATE INDEX IxDeviceTerminalServiceUser
    ON [Identity].[Device](TerminalServiceUserId)
    WHERE TerminalServiceUserId IS NOT NULL;
GO
