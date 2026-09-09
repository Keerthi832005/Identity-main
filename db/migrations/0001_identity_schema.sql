/*
    Forward-only IAM baseline migration owned by Identity.Database.

    Purpose
    -------
    Add a separate Identity and Access Management (IAM) bounded context without
    an external identity provider. Identity owns user and application management.

    Proposed tables
    ---------------
    1. Identity.Application          IAM-managed application and token settings.
    2. Identity.ApplicationClient    Public/confidential/service STS clients.
    3. Identity.ApplicationModule    Hierarchical modules owned by an application.
    4. Identity.ModuleCapability     Fine-grained capabilities owned by a module.
    5. Identity.UserAccount          Local employee/user identity and lockout state.
    6. Identity.UserCredential       Password/PIN hash history; never stores plaintext.
    7. Identity.Device               User devices, trust state, and revocation.
    8. Identity.UserApplication      User access to an application and auth version.
    9. Identity.Role                 Application-scoped roles.
   10. Identity.UserRole             Auditable user-to-role assignments.
   11. Identity.RolePermission       Auditable role-to-capability assignments.
   12. Identity.UserPermissionOverride
                                        Per-user Allow/Deny overrides of role permissions.
   13. Identity.RefreshToken         Hashed, rotating, revocable refresh tokens.
   14. Identity.AuthenticationAudit  Append-only IAM security/management audit.
   15. Identity.UserMfaMethod        Encrypted MFA method configuration.
   16. Identity.MfaChallenge         Short-lived hashed MFA challenges.

    Cryptography contract for the later application implementation
    ---------------------------------------------------------------
    - Password/PIN: PBKDF2-HMAC-SHA256 using a unique random 32-byte salt.
    - Hash length: 32 bytes. Iteration count is stored per credential for upgrades.
    - Refresh tokens: at least 32 random bytes; only SHA-256 token hashes are stored.
    - Confidential/service client secrets: at least 32 random bytes; only SHA-256
      hashes are stored. Public clients do not have a client secret.
    - MFA secrets/destinations are encrypted using an application-managed key;
      temporary OTP values are stored only as keyed hashes.
    - JWT signing keys remain outside SQL Server and source control.
    - Security audit stores hashes of network/client identifiers, not raw values.

    Existing operational UserId columns
    -----------------------------------
    Existing Production tables currently describe UserId as an external reference.
    This draft intentionally does not add foreign keys to those columns. Before the
    final migration, existing UserId values must be inventoried and mapped/backfilled;
    foreign keys can then be added safely in a separately reviewed step.

    Review decisions represented in this draft
    -------------------------------------------
    - Device, STS application-client authentication, and MFA are included.
    - UserApplication is retained as explicit application access.
    - UserPermissionOverride is retained because individual Allow/Deny was requested.
    - Role hierarchy is excluded until a concrete inheritance requirement exists.
    - Employee/organization profile data is not duplicated in IAM.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Identity')
    EXEC(N'CREATE SCHEMA [Identity]');
GO

/* 1. Applications registered and managed by IAM. */
CREATE TABLE [Identity].[Application]
(
    [ApplicationId] BIGINT IDENTITY(1,1) NOT NULL,
    [ApplicationCode] NVARCHAR(100) NOT NULL,
    [NormalizedApplicationCode] AS UPPER(LTRIM(RTRIM([ApplicationCode]))) PERSISTED,
    [ApplicationName] NVARCHAR(150) NOT NULL,
    [Description] NVARCHAR(500) NULL,
    [TokenAudience] NVARCHAR(200) NOT NULL,
    [NormalizedTokenAudience] AS UPPER(LTRIM(RTRIM([TokenAudience]))) PERSISTED,
    [AccessTokenLifetimeMinutes] INT NOT NULL
        CONSTRAINT [DfApplicationAccessTokenLifetimeMinutes] DEFAULT 15,
    [RefreshTokenLifetimeDays] INT NOT NULL
        CONSTRAINT [DfApplicationRefreshTokenLifetimeDays] DEFAULT 7,
    [IsActive] BIT NOT NULL CONSTRAINT [DfApplicationIsActive] DEFAULT 1,
    [CreatedAt] DATETIME2(3) NOT NULL
        CONSTRAINT [DfApplicationCreatedAt] DEFAULT SYSUTCDATETIME(),
    [UpdatedAt] DATETIME2(3) NULL,
    CONSTRAINT [PkApplication] PRIMARY KEY CLUSTERED ([ApplicationId]),
    CONSTRAINT [CkApplicationCode]
        CHECK (LEN(LTRIM(RTRIM([ApplicationCode]))) BETWEEN 1 AND 100),
    CONSTRAINT [CkApplicationName]
        CHECK (LEN(LTRIM(RTRIM([ApplicationName]))) BETWEEN 1 AND 150),
    CONSTRAINT [CkApplicationTokenAudience]
        CHECK (LEN(LTRIM(RTRIM([TokenAudience]))) BETWEEN 1 AND 200),
    CONSTRAINT [CkApplicationTokenLifetimes]
        CHECK ([AccessTokenLifetimeMinutes] BETWEEN 1 AND 60
           AND [RefreshTokenLifetimeDays] BETWEEN 1 AND 90)
);
GO

CREATE UNIQUE INDEX [UqApplicationNormalizedCode]
    ON [Identity].[Application]([NormalizedApplicationCode]);
GO

CREATE UNIQUE INDEX [UqApplicationNormalizedAudience]
    ON [Identity].[Application]([NormalizedTokenAudience]);
GO

/*
    2. STS clients registered for an application.
    Public clients cannot safely keep a secret and therefore have no secret hash.
*/
CREATE TABLE [Identity].[ApplicationClient]
(
    [ApplicationClientId] BIGINT IDENTITY(1,1) NOT NULL,
    [ApplicationId] BIGINT NOT NULL,
    [ClientId] NVARCHAR(150) NOT NULL,
    [NormalizedClientId] AS UPPER(LTRIM(RTRIM([ClientId]))) PERSISTED,
    [ClientName] NVARCHAR(150) NOT NULL,
    [ClientType] NVARCHAR(20) NOT NULL,
    [ClientSecretHash] VARBINARY(32) NULL,
    [SecretVersion] INT NOT NULL CONSTRAINT [DfApplicationClientSecretVersion] DEFAULT 1,
    [IsActive] BIT NOT NULL CONSTRAINT [DfApplicationClientIsActive] DEFAULT 1,
    [CreatedAt] DATETIME2(3) NOT NULL
        CONSTRAINT [DfApplicationClientCreatedAt] DEFAULT SYSUTCDATETIME(),
    [ExpiresAt] DATETIME2(3) NULL,
    [RevokedAt] DATETIME2(3) NULL,
    CONSTRAINT [PkApplicationClient] PRIMARY KEY CLUSTERED ([ApplicationClientId]),
    CONSTRAINT [UqApplicationClientIdentity]
        UNIQUE ([ApplicationClientId], [ApplicationId]),
    CONSTRAINT [FkApplicationClientApplication]
        FOREIGN KEY ([ApplicationId]) REFERENCES [Identity].[Application]([ApplicationId]),
    CONSTRAINT [CkApplicationClientId]
        CHECK (LEN(LTRIM(RTRIM([ClientId]))) BETWEEN 1 AND 150),
    CONSTRAINT [CkApplicationClientName]
        CHECK (LEN(LTRIM(RTRIM([ClientName]))) BETWEEN 1 AND 150),
    CONSTRAINT [CkApplicationClientType]
        CHECK ([ClientType] IN (N'Public', N'Confidential', N'Service')),
    CONSTRAINT [CkApplicationClientSecret]
        CHECK (([ClientType] = N'Public' AND [ClientSecretHash] IS NULL)
            OR ([ClientType] IN (N'Confidential', N'Service')
                AND DATALENGTH([ClientSecretHash]) = 32)),
    CONSTRAINT [CkApplicationClientSecretVersion]
        CHECK ([SecretVersion] > 0),
    CONSTRAINT [CkApplicationClientDates]
        CHECK (([ExpiresAt] IS NULL OR [ExpiresAt] > [CreatedAt])
           AND ([RevokedAt] IS NULL OR [RevokedAt] >= [CreatedAt]))
);
GO

CREATE UNIQUE INDEX [UqApplicationClientNormalizedClientId]
    ON [Identity].[ApplicationClient]([NormalizedClientId]);
GO

CREATE INDEX [IxApplicationClientApplicationActive]
    ON [Identity].[ApplicationClient]([ApplicationId], [IsActive], [ClientType]);
GO

/* 3. Application modules with optional same-application hierarchy. */
CREATE TABLE [Identity].[ApplicationModule]
(
    [ApplicationModuleId] BIGINT IDENTITY(1,1) NOT NULL,
    [ApplicationId] BIGINT NOT NULL,
    [ModuleCode] NVARCHAR(100) NOT NULL,
    [NormalizedModuleCode] AS UPPER(LTRIM(RTRIM([ModuleCode]))) PERSISTED,
    [ModuleName] NVARCHAR(150) NOT NULL,
    [Description] NVARCHAR(500) NULL,
    [ParentApplicationModuleId] BIGINT NULL,
    [DisplayOrder] INT NOT NULL CONSTRAINT [DfApplicationModuleDisplayOrder] DEFAULT 0,
    [IsSystem] BIT NOT NULL CONSTRAINT [DfApplicationModuleIsSystem] DEFAULT 0,
    [IsActive] BIT NOT NULL CONSTRAINT [DfApplicationModuleIsActive] DEFAULT 1,
    [CreatedAt] DATETIME2(3) NOT NULL
        CONSTRAINT [DfApplicationModuleCreatedAt] DEFAULT SYSUTCDATETIME(),
    [UpdatedAt] DATETIME2(3) NULL,
    CONSTRAINT [PkApplicationModule] PRIMARY KEY CLUSTERED ([ApplicationModuleId]),
    CONSTRAINT [UqApplicationModuleIdentity]
        UNIQUE ([ApplicationModuleId], [ApplicationId]),
    CONSTRAINT [FkApplicationModuleApplication]
        FOREIGN KEY ([ApplicationId]) REFERENCES [Identity].[Application]([ApplicationId]),
    CONSTRAINT [FkApplicationModuleParent]
        FOREIGN KEY ([ParentApplicationModuleId], [ApplicationId])
        REFERENCES [Identity].[ApplicationModule]([ApplicationModuleId], [ApplicationId]),
    CONSTRAINT [CkApplicationModuleCode]
        CHECK (LEN(LTRIM(RTRIM([ModuleCode]))) BETWEEN 1 AND 100),
    CONSTRAINT [CkApplicationModuleName]
        CHECK (LEN(LTRIM(RTRIM([ModuleName]))) BETWEEN 1 AND 150),
    CONSTRAINT [CkApplicationModuleDisplayOrder]
        CHECK ([DisplayOrder] >= 0)
);
GO

CREATE UNIQUE INDEX [UqApplicationModuleCode]
    ON [Identity].[ApplicationModule]([ApplicationId], [NormalizedModuleCode]);
GO

CREATE INDEX [IxApplicationModuleParentDisplay]
    ON [Identity].[ApplicationModule]
       ([ApplicationId], [ParentApplicationModuleId], [DisplayOrder], [ModuleName]);
GO

/*
    4. Capabilities owned by application modules.
    CapabilityCode is the stable authorization value placed in access tokens,
    for example production.execute or identity.manage. Codes are not display text
    and must not be renamed after assignment.
*/
CREATE TABLE [Identity].[ModuleCapability]
(
    [ModuleCapabilityId] BIGINT IDENTITY(1,1) NOT NULL,
    [ApplicationId] BIGINT NOT NULL,
    [ApplicationModuleId] BIGINT NOT NULL,
    [CapabilityCode] NVARCHAR(150) NOT NULL,
    [NormalizedCapabilityCode] AS UPPER(LTRIM(RTRIM([CapabilityCode]))) PERSISTED,
    [CapabilityName] NVARCHAR(150) NOT NULL,
    [Description] NVARCHAR(500) NULL,
    [IsActive] BIT NOT NULL CONSTRAINT [DfModuleCapabilityIsActive] DEFAULT 1,
    [CreatedAt] DATETIME2(3) NOT NULL
        CONSTRAINT [DfModuleCapabilityCreatedAt] DEFAULT SYSUTCDATETIME(),
    [UpdatedAt] DATETIME2(3) NULL,
    CONSTRAINT [PkModuleCapability] PRIMARY KEY CLUSTERED ([ModuleCapabilityId]),
    CONSTRAINT [UqModuleCapabilityIdentity]
        UNIQUE ([ModuleCapabilityId], [ApplicationId]),
    CONSTRAINT [FkModuleCapabilityApplicationModule]
        FOREIGN KEY ([ApplicationModuleId], [ApplicationId])
        REFERENCES [Identity].[ApplicationModule]([ApplicationModuleId], [ApplicationId]),
    CONSTRAINT [CkModuleCapabilityCode]
        CHECK (LEN(LTRIM(RTRIM([CapabilityCode]))) BETWEEN 3 AND 150
           AND [CapabilityCode] LIKE N'%.%'),
    CONSTRAINT [CkModuleCapabilityName]
        CHECK (LEN(LTRIM(RTRIM([CapabilityName]))) BETWEEN 1 AND 150)
);
GO

CREATE UNIQUE INDEX [UqModuleCapabilityNormalizedCode]
    ON [Identity].[ModuleCapability]([NormalizedCapabilityCode]);
GO

CREATE INDEX [IxModuleCapabilityModuleActive]
    ON [Identity].[ModuleCapability]
       ([ApplicationId], [ApplicationModuleId], [IsActive], [CapabilityName]);
GO

/* 5. Local account and lockout state. */
CREATE TABLE [Identity].[UserAccount]
(
    [UserId] BIGINT IDENTITY(1,1) NOT NULL,
    [EmployeeCode] NVARCHAR(50) NOT NULL,
    [NormalizedEmployeeCode] AS UPPER(LTRIM(RTRIM([EmployeeCode]))) PERSISTED,
    [DisplayName] NVARCHAR(200) NOT NULL,
    [IsActive] BIT NOT NULL CONSTRAINT [DfUserAccountIsActive] DEFAULT 1,
    [FailedLoginCount] INT NOT NULL CONSTRAINT [DfUserAccountFailedLoginCount] DEFAULT 0,
    [LockoutEndAt] DATETIME2(3) NULL,
    [LastLoginAt] DATETIME2(3) NULL,
    [SecurityVersion] INT NOT NULL CONSTRAINT [DfUserAccountSecurityVersion] DEFAULT 1,
    [CreatedAt] DATETIME2(3) NOT NULL CONSTRAINT [DfUserAccountCreatedAt] DEFAULT SYSUTCDATETIME(),
    [UpdatedAt] DATETIME2(3) NULL,
    CONSTRAINT [PkUserAccount] PRIMARY KEY CLUSTERED ([UserId]),
    CONSTRAINT [CkUserAccountEmployeeCode]
        CHECK (LEN(LTRIM(RTRIM([EmployeeCode]))) BETWEEN 1 AND 50),
    CONSTRAINT [CkUserAccountDisplayName]
        CHECK (LEN(LTRIM(RTRIM([DisplayName]))) BETWEEN 1 AND 200),
    CONSTRAINT [CkUserAccountLoginState]
        CHECK ([FailedLoginCount] >= 0 AND [SecurityVersion] > 0)
);
GO

CREATE UNIQUE INDEX [UqUserAccountNormalizedEmployeeCode]
    ON [Identity].[UserAccount]([NormalizedEmployeeCode]);
GO

CREATE INDEX [IxUserAccountActive]
    ON [Identity].[UserAccount]([IsActive], [LockoutEndAt])
    INCLUDE ([EmployeeCode], [DisplayName], [SecurityVersion]);
GO

/* 6. Credential history. Revocation appends a replacement credential. */
CREATE TABLE [Identity].[UserCredential]
(
    [UserCredentialId] BIGINT IDENTITY(1,1) NOT NULL,
    [UserId] BIGINT NOT NULL,
    [CredentialType] NVARCHAR(20) NOT NULL,
    [Algorithm] NVARCHAR(30) NOT NULL,
    [IterationCount] INT NOT NULL,
    [Salt] VARBINARY(32) NOT NULL,
    [SecretHash] VARBINARY(64) NOT NULL,
    [CreatedAt] DATETIME2(3) NOT NULL CONSTRAINT [DfUserCredentialCreatedAt] DEFAULT SYSUTCDATETIME(),
    [ExpiresAt] DATETIME2(3) NULL,
    [RevokedAt] DATETIME2(3) NULL,
    [CreatedByUserId] BIGINT NULL,
    CONSTRAINT [PkUserCredential] PRIMARY KEY CLUSTERED ([UserCredentialId]),
    CONSTRAINT [FkUserCredentialUser]
        FOREIGN KEY ([UserId]) REFERENCES [Identity].[UserAccount]([UserId]),
    CONSTRAINT [FkUserCredentialCreatedBy]
        FOREIGN KEY ([CreatedByUserId]) REFERENCES [Identity].[UserAccount]([UserId]),
    CONSTRAINT [CkUserCredentialType]
        CHECK ([CredentialType] IN (N'Password', N'Pin')),
    CONSTRAINT [CkUserCredentialAlgorithm]
        CHECK ([Algorithm] = N'PBKDF2-SHA256'),
    CONSTRAINT [CkUserCredentialIterations]
        CHECK ([IterationCount] BETWEEN 100000 AND 10000000),
    CONSTRAINT [CkUserCredentialHashLengths]
        CHECK (DATALENGTH([Salt]) = 32 AND DATALENGTH([SecretHash]) BETWEEN 32 AND 64),
    CONSTRAINT [CkUserCredentialDates]
        CHECK (([ExpiresAt] IS NULL OR [ExpiresAt] > [CreatedAt])
           AND ([RevokedAt] IS NULL OR [RevokedAt] >= [CreatedAt]))
);
GO

CREATE UNIQUE INDEX [UqUserCredentialCurrent]
    ON [Identity].[UserCredential]([UserId], [CredentialType])
    WHERE [RevokedAt] IS NULL;
GO

CREATE INDEX [IxUserCredentialHistory]
    ON [Identity].[UserCredential]([UserId], [CredentialType], [CreatedAt] DESC);
GO

/* 7. Registered user devices used for trust, revocation, tokens, and audit. */
CREATE TABLE [Identity].[Device]
(
    [DeviceId] BIGINT IDENTITY(1,1) NOT NULL,
    [UserId] BIGINT NOT NULL,
    [DeviceName] NVARCHAR(200) NOT NULL,
    [DeviceType] NVARCHAR(50) NOT NULL,
    [DeviceFingerprintHash] VARBINARY(32) NOT NULL,
    [UserAgentHash] VARBINARY(32) NULL,
    [ClientAddressHash] VARBINARY(32) NULL,
    [IsTrusted] BIT NOT NULL CONSTRAINT [DfDeviceIsTrusted] DEFAULT 0,
    [IsActive] BIT NOT NULL CONSTRAINT [DfDeviceIsActive] DEFAULT 1,
    [FirstSeenAt] DATETIME2(3) NOT NULL CONSTRAINT [DfDeviceFirstSeenAt] DEFAULT SYSUTCDATETIME(),
    [LastSeenAt] DATETIME2(3) NULL,
    [CreatedAt] DATETIME2(3) NOT NULL CONSTRAINT [DfDeviceCreatedAt] DEFAULT SYSUTCDATETIME(),
    [RevokedAt] DATETIME2(3) NULL,
    [RevokedByUserId] BIGINT NULL,
    CONSTRAINT [PkDevice] PRIMARY KEY CLUSTERED ([DeviceId]),
    CONSTRAINT [UqDeviceIdentity] UNIQUE ([DeviceId], [UserId]),
    CONSTRAINT [FkDeviceUser]
        FOREIGN KEY ([UserId]) REFERENCES [Identity].[UserAccount]([UserId]),
    CONSTRAINT [FkDeviceRevokedBy]
        FOREIGN KEY ([RevokedByUserId]) REFERENCES [Identity].[UserAccount]([UserId]),
    CONSTRAINT [CkDeviceName]
        CHECK (LEN(LTRIM(RTRIM([DeviceName]))) BETWEEN 1 AND 200),
    CONSTRAINT [CkDeviceType]
        CHECK (LEN(LTRIM(RTRIM([DeviceType]))) BETWEEN 1 AND 50),
    CONSTRAINT [CkDeviceHashLengths]
        CHECK (DATALENGTH([DeviceFingerprintHash]) = 32
           AND ([UserAgentHash] IS NULL OR DATALENGTH([UserAgentHash]) = 32)
           AND ([ClientAddressHash] IS NULL OR DATALENGTH([ClientAddressHash]) = 32)),
    CONSTRAINT [CkDeviceDates]
        CHECK (([LastSeenAt] IS NULL OR [LastSeenAt] >= [FirstSeenAt])
           AND ([RevokedAt] IS NULL OR [RevokedAt] >= [CreatedAt]))
);
GO

CREATE UNIQUE INDEX [UqDeviceActiveFingerprint]
    ON [Identity].[Device]([UserId], [DeviceFingerprintHash])
    WHERE [RevokedAt] IS NULL;
GO

CREATE INDEX [IxDeviceUserActive]
    ON [Identity].[Device]([UserId], [IsActive], [IsTrusted], [LastSeenAt] DESC);
GO

/* 8. Explicit user access to each application. */
CREATE TABLE [Identity].[UserApplication]
(
    [UserId] BIGINT NOT NULL,
    [ApplicationId] BIGINT NOT NULL,
    [IsActive] BIT NOT NULL CONSTRAINT [DfUserApplicationIsActive] DEFAULT 1,
    [AuthorizationVersion] INT NOT NULL
        CONSTRAINT [DfUserApplicationAuthorizationVersion] DEFAULT 1,
    [AssignedAt] DATETIME2(3) NOT NULL
        CONSTRAINT [DfUserApplicationAssignedAt] DEFAULT SYSUTCDATETIME(),
    [AssignedByUserId] BIGINT NULL,
    [UpdatedAt] DATETIME2(3) NULL,
    [UpdatedByUserId] BIGINT NULL,
    [RevokedAt] DATETIME2(3) NULL,
    [RevokedByUserId] BIGINT NULL,
    CONSTRAINT [PkUserApplication] PRIMARY KEY CLUSTERED ([UserId], [ApplicationId]),
    CONSTRAINT [FkUserApplicationUser]
        FOREIGN KEY ([UserId]) REFERENCES [Identity].[UserAccount]([UserId]),
    CONSTRAINT [FkUserApplicationApplication]
        FOREIGN KEY ([ApplicationId]) REFERENCES [Identity].[Application]([ApplicationId]),
    CONSTRAINT [FkUserApplicationAssignedBy]
        FOREIGN KEY ([AssignedByUserId]) REFERENCES [Identity].[UserAccount]([UserId]),
    CONSTRAINT [FkUserApplicationUpdatedBy]
        FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Identity].[UserAccount]([UserId]),
    CONSTRAINT [FkUserApplicationRevokedBy]
        FOREIGN KEY ([RevokedByUserId]) REFERENCES [Identity].[UserAccount]([UserId]),
    CONSTRAINT [CkUserApplicationAuthorizationVersion]
        CHECK ([AuthorizationVersion] > 0),
    CONSTRAINT [CkUserApplicationDates]
        CHECK (([UpdatedAt] IS NULL OR [UpdatedAt] >= [AssignedAt])
           AND ([RevokedAt] IS NULL OR [RevokedAt] >= [AssignedAt]))
);
GO

CREATE INDEX [IxUserApplicationApplicationActive]
    ON [Identity].[UserApplication]([ApplicationId], [IsActive], [UserId]);
GO

/* 9. Application-scoped roles such as Operator, Supervisor, and Administrator. */
CREATE TABLE [Identity].[Role]
(
    [RoleId] BIGINT IDENTITY(1,1) NOT NULL,
    [ApplicationId] BIGINT NOT NULL,
    [RoleCode] NVARCHAR(50) NOT NULL,
    [NormalizedRoleCode] AS UPPER(LTRIM(RTRIM([RoleCode]))) PERSISTED,
    [RoleName] NVARCHAR(100) NOT NULL,
    [Description] NVARCHAR(500) NULL,
    [IsSystem] BIT NOT NULL CONSTRAINT [DfRoleIsSystem] DEFAULT 0,
    [IsActive] BIT NOT NULL CONSTRAINT [DfRoleIsActive] DEFAULT 1,
    [CreatedAt] DATETIME2(3) NOT NULL CONSTRAINT [DfRoleCreatedAt] DEFAULT SYSUTCDATETIME(),
    [UpdatedAt] DATETIME2(3) NULL,
    CONSTRAINT [PkRole] PRIMARY KEY CLUSTERED ([RoleId]),
    CONSTRAINT [UqRoleIdentity] UNIQUE ([RoleId], [ApplicationId]),
    CONSTRAINT [FkRoleApplication]
        FOREIGN KEY ([ApplicationId]) REFERENCES [Identity].[Application]([ApplicationId]),
    CONSTRAINT [CkRoleCode]
        CHECK (LEN(LTRIM(RTRIM([RoleCode]))) BETWEEN 1 AND 50),
    CONSTRAINT [CkRoleName]
        CHECK (LEN(LTRIM(RTRIM([RoleName]))) BETWEEN 1 AND 100)
);
GO

CREATE UNIQUE INDEX [UqRoleApplicationCode]
    ON [Identity].[Role]([ApplicationId], [NormalizedRoleCode]);
GO

/* 10. User-role history; only one active assignment per user/role. */
CREATE TABLE [Identity].[UserRole]
(
    [UserRoleId] BIGINT IDENTITY(1,1) NOT NULL,
    [UserId] BIGINT NOT NULL,
    [ApplicationId] BIGINT NOT NULL,
    [RoleId] BIGINT NOT NULL,
    [AssignedAt] DATETIME2(3) NOT NULL CONSTRAINT [DfUserRoleAssignedAt] DEFAULT SYSUTCDATETIME(),
    [AssignedByUserId] BIGINT NULL,
    [RevokedAt] DATETIME2(3) NULL,
    [RevokedByUserId] BIGINT NULL,
    CONSTRAINT [PkUserRole] PRIMARY KEY CLUSTERED ([UserRoleId]),
    CONSTRAINT [FkUserRoleUserApplication]
        FOREIGN KEY ([UserId], [ApplicationId])
        REFERENCES [Identity].[UserApplication]([UserId], [ApplicationId]),
    CONSTRAINT [FkUserRoleRole]
        FOREIGN KEY ([RoleId], [ApplicationId])
        REFERENCES [Identity].[Role]([RoleId], [ApplicationId]),
    CONSTRAINT [FkUserRoleAssignedBy]
        FOREIGN KEY ([AssignedByUserId]) REFERENCES [Identity].[UserAccount]([UserId]),
    CONSTRAINT [FkUserRoleRevokedBy]
        FOREIGN KEY ([RevokedByUserId]) REFERENCES [Identity].[UserAccount]([UserId]),
    CONSTRAINT [CkUserRoleDates]
        CHECK ([RevokedAt] IS NULL OR [RevokedAt] >= [AssignedAt])
);
GO

CREATE UNIQUE INDEX [UqUserRoleActive]
    ON [Identity].[UserRole]([UserId], [ApplicationId], [RoleId])
    WHERE [RevokedAt] IS NULL;
GO

CREATE INDEX [IxUserRoleRoleActive]
    ON [Identity].[UserRole]([ApplicationId], [RoleId], [UserId])
    WHERE [RevokedAt] IS NULL;
GO

/* 11. Role-permission history; only one active grant per role/capability pair. */
CREATE TABLE [Identity].[RolePermission]
(
    [RolePermissionId] BIGINT IDENTITY(1,1) NOT NULL,
    [ApplicationId] BIGINT NOT NULL,
    [RoleId] BIGINT NOT NULL,
    [ModuleCapabilityId] BIGINT NOT NULL,
    [GrantedAt] DATETIME2(3) NOT NULL CONSTRAINT [DfRolePermissionGrantedAt] DEFAULT SYSUTCDATETIME(),
    [GrantedByUserId] BIGINT NULL,
    [RevokedAt] DATETIME2(3) NULL,
    [RevokedByUserId] BIGINT NULL,
    CONSTRAINT [PkRolePermission] PRIMARY KEY CLUSTERED ([RolePermissionId]),
    CONSTRAINT [FkRolePermissionRole]
        FOREIGN KEY ([RoleId], [ApplicationId])
        REFERENCES [Identity].[Role]([RoleId], [ApplicationId]),
    CONSTRAINT [FkRolePermissionModuleCapability]
        FOREIGN KEY ([ModuleCapabilityId], [ApplicationId])
        REFERENCES [Identity].[ModuleCapability]([ModuleCapabilityId], [ApplicationId]),
    CONSTRAINT [FkRolePermissionGrantedBy]
        FOREIGN KEY ([GrantedByUserId]) REFERENCES [Identity].[UserAccount]([UserId]),
    CONSTRAINT [FkRolePermissionRevokedBy]
        FOREIGN KEY ([RevokedByUserId]) REFERENCES [Identity].[UserAccount]([UserId]),
    CONSTRAINT [CkRolePermissionDates]
        CHECK ([RevokedAt] IS NULL OR [RevokedAt] >= [GrantedAt])
);
GO

CREATE UNIQUE INDEX [UqRolePermissionActive]
    ON [Identity].[RolePermission]([ApplicationId], [RoleId], [ModuleCapabilityId])
    WHERE [RevokedAt] IS NULL;
GO

/*
    12. Per-user permission overrides.

    Effective permission precedence:
    - An active Deny override always denies the permission, including role grants.
    - An active Allow override grants a permission not supplied by the user's roles.
    - Without an active override, permissions are inherited from active roles.
    - Role/permission changes must increment UserApplication.AuthorizationVersion
      in the same transaction so tokens for that application are rejected.
*/
CREATE TABLE [Identity].[UserPermissionOverride]
(
    [UserPermissionOverrideId] BIGINT IDENTITY(1,1) NOT NULL,
    [UserId] BIGINT NOT NULL,
    [ApplicationId] BIGINT NOT NULL,
    [ModuleCapabilityId] BIGINT NOT NULL,
    [Effect] NVARCHAR(10) NOT NULL,
    [Reason] NVARCHAR(500) NOT NULL,
    [AssignedAt] DATETIME2(3) NOT NULL
        CONSTRAINT [DfUserPermissionOverrideAssignedAt] DEFAULT SYSUTCDATETIME(),
    [AssignedByUserId] BIGINT NOT NULL,
    [ExpiresAt] DATETIME2(3) NULL,
    [RevokedAt] DATETIME2(3) NULL,
    [RevokedByUserId] BIGINT NULL,
    CONSTRAINT [PkUserPermissionOverride]
        PRIMARY KEY CLUSTERED ([UserPermissionOverrideId]),
    CONSTRAINT [FkUserPermissionOverrideUserApplication]
        FOREIGN KEY ([UserId], [ApplicationId])
        REFERENCES [Identity].[UserApplication]([UserId], [ApplicationId]),
    CONSTRAINT [FkUserPermissionOverrideModuleCapability]
        FOREIGN KEY ([ModuleCapabilityId], [ApplicationId])
        REFERENCES [Identity].[ModuleCapability]([ModuleCapabilityId], [ApplicationId]),
    CONSTRAINT [FkUserPermissionOverrideAssignedBy]
        FOREIGN KEY ([AssignedByUserId]) REFERENCES [Identity].[UserAccount]([UserId]),
    CONSTRAINT [FkUserPermissionOverrideRevokedBy]
        FOREIGN KEY ([RevokedByUserId]) REFERENCES [Identity].[UserAccount]([UserId]),
    CONSTRAINT [CkUserPermissionOverrideEffect]
        CHECK ([Effect] IN (N'Allow', N'Deny')),
    CONSTRAINT [CkUserPermissionOverrideReason]
        CHECK (LEN(LTRIM(RTRIM([Reason]))) BETWEEN 1 AND 500),
    CONSTRAINT [CkUserPermissionOverrideDates]
        CHECK (([ExpiresAt] IS NULL OR [ExpiresAt] > [AssignedAt])
           AND ([RevokedAt] IS NULL OR [RevokedAt] >= [AssignedAt]))
);
GO

CREATE UNIQUE INDEX [UqUserPermissionOverrideActive]
    ON [Identity].[UserPermissionOverride]([UserId], [ApplicationId], [ModuleCapabilityId])
    WHERE [RevokedAt] IS NULL;
GO

CREATE INDEX [IxUserPermissionOverrideAuthorization]
    ON [Identity].[UserPermissionOverride]
       ([UserId], [ApplicationId], [ModuleCapabilityId], [Effect])
    INCLUDE ([ExpiresAt], [RevokedAt]);
GO

/* 13. Rotating refresh tokens. The raw token is returned once and never stored. */
CREATE TABLE [Identity].[RefreshToken]
(
    [RefreshTokenId] BIGINT IDENTITY(1,1) NOT NULL,
    [UserId] BIGINT NOT NULL,
    [ApplicationId] BIGINT NOT NULL,
    [ApplicationClientId] BIGINT NOT NULL,
    [TokenHash] VARBINARY(32) NOT NULL,
    [TokenFamilyId] UNIQUEIDENTIFIER NOT NULL,
    [SecurityVersion] INT NOT NULL,
    [AuthorizationVersion] INT NOT NULL,
    [DeviceId] BIGINT NULL,
    [IssuedAt] DATETIME2(3) NOT NULL,
    [ExpiresAt] DATETIME2(3) NOT NULL,
    [ConsumedAt] DATETIME2(3) NULL,
    [RevokedAt] DATETIME2(3) NULL,
    [ReplacedByRefreshTokenId] BIGINT NULL,
    [ClientAddressHash] VARBINARY(32) NULL,
    [UserAgentHash] VARBINARY(32) NULL,
    CONSTRAINT [PkRefreshToken] PRIMARY KEY CLUSTERED ([RefreshTokenId]),
    CONSTRAINT [UqRefreshTokenHash] UNIQUE ([TokenHash]),
    CONSTRAINT [FkRefreshTokenUserApplication]
        FOREIGN KEY ([UserId], [ApplicationId])
        REFERENCES [Identity].[UserApplication]([UserId], [ApplicationId]),
    CONSTRAINT [FkRefreshTokenApplicationClient]
        FOREIGN KEY ([ApplicationClientId], [ApplicationId])
        REFERENCES [Identity].[ApplicationClient]([ApplicationClientId], [ApplicationId]),
    CONSTRAINT [FkRefreshTokenDevice]
        FOREIGN KEY ([DeviceId], [UserId])
        REFERENCES [Identity].[Device]([DeviceId], [UserId]),
    CONSTRAINT [CkRefreshTokenHashLength]
        CHECK (DATALENGTH([TokenHash]) = 32
           AND ([ClientAddressHash] IS NULL OR DATALENGTH([ClientAddressHash]) = 32)
           AND ([UserAgentHash] IS NULL OR DATALENGTH([UserAgentHash]) = 32)),
    CONSTRAINT [CkRefreshTokenVersions]
        CHECK ([SecurityVersion] > 0 AND [AuthorizationVersion] > 0),
    CONSTRAINT [CkRefreshTokenDates]
        CHECK ([ExpiresAt] > [IssuedAt]
           AND ([ConsumedAt] IS NULL OR [ConsumedAt] >= [IssuedAt])
           AND ([RevokedAt] IS NULL OR [RevokedAt] >= [IssuedAt]))
);
GO

ALTER TABLE [Identity].[RefreshToken]
    ADD CONSTRAINT [FkRefreshTokenReplacement]
        FOREIGN KEY ([ReplacedByRefreshTokenId])
        REFERENCES [Identity].[RefreshToken]([RefreshTokenId]);
GO

CREATE UNIQUE INDEX [UqRefreshTokenActiveFamily]
    ON [Identity].[RefreshToken]([UserId], [ApplicationId], [TokenFamilyId])
    WHERE [ConsumedAt] IS NULL AND [RevokedAt] IS NULL;
GO

CREATE INDEX [IxRefreshTokenUserExpiry]
    ON [Identity].[RefreshToken]([UserId], [ApplicationId], [ExpiresAt])
    INCLUDE ([ApplicationClientId], [DeviceId], [TokenFamilyId], [SecurityVersion],
             [AuthorizationVersion], [ConsumedAt], [RevokedAt]);
GO

/*
    14. Append-only IAM audit. Raw tokens, passwords, PINs, OTPs, secrets, and
    sensitive headers are forbidden, including inside EventDataJson.
*/
CREATE TABLE [Identity].[AuthenticationAudit]
(
    [AuthenticationAuditId] BIGINT IDENTITY(1,1) NOT NULL,
    [UserId] BIGINT NULL,
    [ApplicationId] BIGINT NULL,
    [ApplicationClientId] BIGINT NULL,
    [LoginIdentifierHash] VARBINARY(32) NULL,
    [EventType] NVARCHAR(50) NOT NULL,
    [Succeeded] BIT NOT NULL,
    [FailureCode] NVARCHAR(100) NULL,
    [DeviceId] BIGINT NULL,
    [ClientAddressHash] VARBINARY(32) NULL,
    [UserAgentHash] VARBINARY(32) NULL,
    [CorrelationId] UNIQUEIDENTIFIER NOT NULL,
    [OccurredAt] DATETIME2(3) NOT NULL,
    [EventDataJson] NVARCHAR(MAX) NULL,
    CONSTRAINT [PkAuthenticationAudit] PRIMARY KEY CLUSTERED ([AuthenticationAuditId]),
    CONSTRAINT [FkAuthenticationAuditUser]
        FOREIGN KEY ([UserId]) REFERENCES [Identity].[UserAccount]([UserId]),
    CONSTRAINT [FkAuthenticationAuditApplication]
        FOREIGN KEY ([ApplicationId]) REFERENCES [Identity].[Application]([ApplicationId]),
    CONSTRAINT [FkAuthenticationAuditApplicationClient]
        FOREIGN KEY ([ApplicationClientId], [ApplicationId])
        REFERENCES [Identity].[ApplicationClient]([ApplicationClientId], [ApplicationId]),
    CONSTRAINT [FkAuthenticationAuditDevice]
        FOREIGN KEY ([DeviceId], [UserId])
        REFERENCES [Identity].[Device]([DeviceId], [UserId]),
    CONSTRAINT [CkAuthenticationAuditReferences]
        CHECK (([ApplicationClientId] IS NULL OR [ApplicationId] IS NOT NULL)
           AND ([DeviceId] IS NULL OR [UserId] IS NOT NULL)),
    CONSTRAINT [CkAuthenticationAuditEventType]
        CHECK ([EventType] IN
        (
            N'LoginSucceeded', N'LoginFailed', N'AccountLocked',
            N'TokenRefreshed', N'TokenRefreshRejected', N'LoggedOut',
            N'ClientAuthenticated', N'ClientAuthenticationFailed',
            N'CredentialChanged', N'AccountEnabled', N'AccountDisabled',
            N'DeviceRegistered', N'DeviceTrusted', N'DeviceRevoked',
            N'MfaMethodAdded', N'MfaMethodVerified', N'MfaMethodRevoked',
            N'MfaChallengeCreated', N'MfaChallengeVerified', N'MfaChallengeRejected',
            N'UserApplicationAssigned', N'UserApplicationRevoked',
            N'RoleAssigned', N'RoleRevoked',
            N'PermissionAllowed', N'PermissionDenied', N'PermissionOverrideRevoked',
            N'ApplicationCreated', N'ApplicationUpdated', N'ApplicationDisabled',
            N'ApplicationClientCreated', N'ApplicationClientRotated',
            N'ApplicationClientRevoked',
            N'ModuleCreated', N'ModuleUpdated', N'ModuleDisabled',
            N'CapabilityCreated', N'CapabilityUpdated', N'CapabilityDisabled'
        )),
    CONSTRAINT [CkAuthenticationAuditHashLengths]
        CHECK (([LoginIdentifierHash] IS NULL OR DATALENGTH([LoginIdentifierHash]) = 32)
           AND ([ClientAddressHash] IS NULL OR DATALENGTH([ClientAddressHash]) = 32)
           AND ([UserAgentHash] IS NULL OR DATALENGTH([UserAgentHash]) = 32)),
    CONSTRAINT [CkAuthenticationAuditEventDataJson]
        CHECK ([EventDataJson] IS NULL OR ISJSON([EventDataJson]) = 1)
);
GO

CREATE INDEX [IxAuthenticationAuditUserTime]
    ON [Identity].[AuthenticationAudit]
       ([UserId], [ApplicationId], [OccurredAt] DESC, [EventType]);
GO

CREATE INDEX [IxAuthenticationAuditCorrelation]
    ON [Identity].[AuthenticationAudit]([CorrelationId], [OccurredAt]);
GO

CREATE OR ALTER TRIGGER [Identity].[TrAuthenticationAuditAppendOnly]
ON [Identity].[AuthenticationAudit]
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    THROW 51030, 'AuthenticationAudit is append-only.', 1;
END;
GO

/*
    15. MFA methods. SecretEncrypted and DestinationEncrypted contain ciphertext,
    never plaintext. Encryption keys remain outside SQL Server and source control.
*/
CREATE TABLE [Identity].[UserMfaMethod]
(
    [UserMfaMethodId] BIGINT IDENTITY(1,1) NOT NULL,
    [UserId] BIGINT NOT NULL,
    [MethodType] NVARCHAR(20) NOT NULL,
    [MethodName] NVARCHAR(100) NOT NULL,
    [SecretEncrypted] VARBINARY(MAX) NULL,
    [DestinationEncrypted] VARBINARY(MAX) NULL,
    [EncryptionKeyId] NVARCHAR(100) NOT NULL,
    [IsPrimary] BIT NOT NULL CONSTRAINT [DfUserMfaMethodIsPrimary] DEFAULT 0,
    [IsEnabled] BIT NOT NULL CONSTRAINT [DfUserMfaMethodIsEnabled] DEFAULT 1,
    [IsVerified] BIT NOT NULL CONSTRAINT [DfUserMfaMethodIsVerified] DEFAULT 0,
    [CreatedAt] DATETIME2(3) NOT NULL
        CONSTRAINT [DfUserMfaMethodCreatedAt] DEFAULT SYSUTCDATETIME(),
    [VerifiedAt] DATETIME2(3) NULL,
    [LastUsedAt] DATETIME2(3) NULL,
    [RevokedAt] DATETIME2(3) NULL,
    CONSTRAINT [PkUserMfaMethod] PRIMARY KEY CLUSTERED ([UserMfaMethodId]),
    CONSTRAINT [UqUserMfaMethodIdentity] UNIQUE ([UserMfaMethodId], [UserId]),
    CONSTRAINT [FkUserMfaMethodUser]
        FOREIGN KEY ([UserId]) REFERENCES [Identity].[UserAccount]([UserId]),
    CONSTRAINT [CkUserMfaMethodType]
        CHECK ([MethodType] IN (N'Totp', N'EmailOtp', N'SmsOtp')),
    CONSTRAINT [CkUserMfaMethodName]
        CHECK (LEN(LTRIM(RTRIM([MethodName]))) BETWEEN 1 AND 100),
    CONSTRAINT [CkUserMfaMethodEncryptedValues]
        CHECK (([MethodType] = N'Totp'
                AND [SecretEncrypted] IS NOT NULL
                AND DATALENGTH([SecretEncrypted]) > 0
                AND [DestinationEncrypted] IS NULL)
            OR ([MethodType] IN (N'EmailOtp', N'SmsOtp')
                AND [SecretEncrypted] IS NULL
                AND [DestinationEncrypted] IS NOT NULL
                AND DATALENGTH([DestinationEncrypted]) > 0)),
    CONSTRAINT [CkUserMfaMethodEncryptionKey]
        CHECK (LEN(LTRIM(RTRIM([EncryptionKeyId]))) BETWEEN 1 AND 100),
    CONSTRAINT [CkUserMfaMethodVerification]
        CHECK (([IsVerified] = 0 AND [VerifiedAt] IS NULL)
            OR ([IsVerified] = 1 AND [VerifiedAt] IS NOT NULL)),
    CONSTRAINT [CkUserMfaMethodDates]
        CHECK (([VerifiedAt] IS NULL OR [VerifiedAt] >= [CreatedAt])
           AND ([LastUsedAt] IS NULL OR [LastUsedAt] >= [CreatedAt])
           AND ([RevokedAt] IS NULL OR [RevokedAt] >= [CreatedAt]))
);
GO

CREATE UNIQUE INDEX [UqUserMfaMethodPrimary]
    ON [Identity].[UserMfaMethod]([UserId])
    WHERE [IsPrimary] = 1 AND [RevokedAt] IS NULL;
GO

CREATE INDEX [IxUserMfaMethodUserActive]
    ON [Identity].[UserMfaMethod]([UserId], [IsEnabled], [IsVerified], [MethodType])
    INCLUDE ([IsPrimary], [RevokedAt]);
GO

/*
    16. Short-lived MFA challenges. ChallengeHash is a keyed HMAC-SHA256 value;
    the HMAC key identified by ChallengeKeyId is stored outside SQL Server.
*/
CREATE TABLE [Identity].[MfaChallenge]
(
    [MfaChallengeId] UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT [DfMfaChallengeId] DEFAULT NEWSEQUENTIALID(),
    [UserId] BIGINT NOT NULL,
    [ApplicationId] BIGINT NOT NULL,
    [ApplicationClientId] BIGINT NOT NULL,
    [UserMfaMethodId] BIGINT NOT NULL,
    [DeviceId] BIGINT NULL,
    [ChallengeHash] VARBINARY(32) NOT NULL,
    [ChallengeKeyId] NVARCHAR(100) NOT NULL,
    [ExpiresAt] DATETIME2(3) NOT NULL,
    [VerifiedAt] DATETIME2(3) NULL,
    [AttemptCount] INT NOT NULL CONSTRAINT [DfMfaChallengeAttemptCount] DEFAULT 0,
    [MaximumAttemptCount] INT NOT NULL CONSTRAINT [DfMfaChallengeMaximumAttemptCount] DEFAULT 5,
    [CreatedAt] DATETIME2(3) NOT NULL CONSTRAINT [DfMfaChallengeCreatedAt] DEFAULT SYSUTCDATETIME(),
    [CorrelationId] UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PkMfaChallenge] PRIMARY KEY CLUSTERED ([MfaChallengeId]),
    CONSTRAINT [FkMfaChallengeUserApplication]
        FOREIGN KEY ([UserId], [ApplicationId])
        REFERENCES [Identity].[UserApplication]([UserId], [ApplicationId]),
    CONSTRAINT [FkMfaChallengeApplicationClient]
        FOREIGN KEY ([ApplicationClientId], [ApplicationId])
        REFERENCES [Identity].[ApplicationClient]([ApplicationClientId], [ApplicationId]),
    CONSTRAINT [FkMfaChallengeUserMfaMethod]
        FOREIGN KEY ([UserMfaMethodId], [UserId])
        REFERENCES [Identity].[UserMfaMethod]([UserMfaMethodId], [UserId]),
    CONSTRAINT [FkMfaChallengeDevice]
        FOREIGN KEY ([DeviceId], [UserId])
        REFERENCES [Identity].[Device]([DeviceId], [UserId]),
    CONSTRAINT [CkMfaChallengeHash]
        CHECK (DATALENGTH([ChallengeHash]) = 32),
    CONSTRAINT [CkMfaChallengeKey]
        CHECK (LEN(LTRIM(RTRIM([ChallengeKeyId]))) BETWEEN 1 AND 100),
    CONSTRAINT [CkMfaChallengeAttempts]
        CHECK ([MaximumAttemptCount] BETWEEN 1 AND 10
           AND [AttemptCount] BETWEEN 0 AND [MaximumAttemptCount]),
    CONSTRAINT [CkMfaChallengeDates]
        CHECK ([ExpiresAt] > [CreatedAt]
           AND ([VerifiedAt] IS NULL
                OR ([VerifiedAt] >= [CreatedAt] AND [VerifiedAt] <= [ExpiresAt])))
);
GO

CREATE INDEX [IxMfaChallengeUserExpiry]
    ON [Identity].[MfaChallenge]([UserId], [ApplicationId], [ExpiresAt])
    INCLUDE ([UserMfaMethodId], [DeviceId], [VerifiedAt], [AttemptCount], [MaximumAttemptCount]);
GO

CREATE INDEX [IxMfaChallengeCorrelation]
    ON [Identity].[MfaChallenge]([CorrelationId], [CreatedAt]);
GO

/*
    Proposed applications and modules (seed only after approval)
    ------------------------------------------------------------
    identity                    IAM application.
      identity.accounts         User and credential management.
      identity.applications     Application, module, and capability management.

    production-tracking         Production tracking application.
      production.operations     Production execution operations.
      production.costing        Production cost management.

    Proposed module capabilities (seed only after approval)
    -------------------------------------------------------
    identity.accounts.read          View local accounts and role assignments.
    identity.accounts.manage        Manage accounts, credentials, and roles.
    identity.applications.read      View applications, modules, and capabilities.
    identity.applications.manage    Manage applications, modules, and capabilities.
    production.operations.access    Access production operations.
    production.costing.manage       Recalculate production costs.

    Proposed system roles (seed only after approval)
    ------------------------------------------------
    Operator            production.operations.access
    Supervisor          production.operations.access
    CostManager         production.operations.access + production.costing.manage
    Administrator       all capabilities for its application
*/
