<#
.SYNOPSIS
Full automated deployment of IAM to UAT environment
.DESCRIPTION
One-command deployment: provisions infrastructure, database, migrates schema, bootstraps admin, deploys API/frontend/agent
.PARAMETER Environment
Environment name (default: UAT)
.PARAMETER DryRun
Preview mode - shows what would be done without making changes
#>

[CmdletBinding()]
param(
    [string]$Environment = 'UAT',
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# ============================================================================
# Configuration
# ============================================================================

$config = @{
    Environment         = $Environment
    Hostname            = 'iam-uat.fujitecindia.com'
    InternalHostname    = 'fujitecapp2'
    PublicUrl           = "https://iam-uat.fujitecindia.com"
    ApiPort             = 18100
    ApiBinding          = "127.0.0.1:18100"
    IisSiteName         = 'IAM-UAT-Web'
    IisApiPoolName      = 'IAM-UAT-Api'
    IisWebPoolName      = 'IAM-UAT-Web-Pool'
    SqlServer           = 'fujitecapp2\SQLDEVELOPER2022'
    SqlDatabase         = 'FIN_IAM_UAT'
    SqlUser             = 'iam-uat-app'
    DeploymentPath      = 'C:\IAM-UAT-Deployment'
    ArtifactPath        = 'C:\IAM-UAT-Artifacts'
    LogPath             = 'C:\IAM-UAT-Logs'
    RepositoryRoot      = (Get-Location).Path
}

# ============================================================================
# Logging
# ============================================================================

function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $logFile = Join-Path $config.LogPath "deployment-$(Get-Date -Format 'yyyyMMdd').log"
    $output = "[$timestamp] [$Level] $Message"
    Write-Host $output
    Add-Content -Path $logFile -Value $output -ErrorAction SilentlyContinue
}

function Write-Section {
    param([string]$Title)
    Write-Host "`n$('='*70)" -ForegroundColor Cyan
    Write-Host "  $Title" -ForegroundColor Cyan
    Write-Host "$('='*70)`n" -ForegroundColor Cyan
    Write-Log "=== $Title ==="
}

# ============================================================================
# Prerequisites Validation
# ============================================================================

function Test-Prerequisites {
    Write-Section "Validating Prerequisites"

    $checks = @(
        @{ Name = "PowerShell 5.1+"; Test = { $PSVersionTable.PSVersion.Major -ge 5 } },
        @{ Name = ".NET 10 SDK"; Test = { dotnet --version } },
        @{ Name = "Node.js"; Test = { node --version } },
        @{ Name = "npm"; Test = { npm --version } },
        @{ Name = "Git"; Test = { git --version } },
        @{ Name = "SQL Server Client"; Test = { sqlcmd -? } },
        @{ Name = "IIS Installed"; Test = { $null = Get-Service W3SVC -ErrorAction Stop; $true } },
        @{ Name = "Administrator"; Test = { [bool]([Security.Principal.WindowsIdentity]::GetCurrent().Groups -match 'S-1-5-32-544') } }
    )

    $allPassed = $true
    foreach ($check in $checks) {
        try {
            $result = & $check.Test
            Write-Log "$($check.Name): ✓ PASS" 'SUCCESS'
            Write-Host "  ✓ $($check.Name)" -ForegroundColor Green
        } catch {
            Write-Log "$($check.Name): ✗ FAIL - $_" 'ERROR'
            Write-Host "  ✗ $($check.Name)" -ForegroundColor Red
            $allPassed = $false
        }
    }

    if (-not $allPassed) {
        throw "Prerequisites validation failed. Install missing tools and retry."
    }
}

# ============================================================================
# Directory Setup
# ============================================================================

function Initialize-Directories {
    Write-Section "Initializing Directories"

    $dirs = @($config.DeploymentPath, $config.ArtifactPath, $config.LogPath)
    foreach ($dir in $dirs) {
        if (-not (Test-Path $dir)) {
            if ($DryRun) {
                Write-Log "DRYRUN: Would create directory $dir"
            } else {
                New-Item -ItemType Directory -Path $dir -Force | Out-Null
                Write-Log "Created directory: $dir" 'SUCCESS'
            }
        }
        Write-Host "  ✓ $dir" -ForegroundColor Green
    }
}

# ============================================================================
# SQL Server Setup
# ============================================================================

function Test-SqlConnection {
    Write-Section "Testing SQL Server Connection"

    try {
        $connectionString = "Server=$($config.SqlServer);Database=master;Integrated Security=true;Connection Timeout=10;"
        $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $connection.Open()
        $connection.Close()
        Write-Log "SQL Server connection successful: $($config.SqlServer)" 'SUCCESS'
        Write-Host "  ✓ Connected to $($config.SqlServer)" -ForegroundColor Green
        return $true
    } catch {
        Write-Log "SQL Server connection failed: $_" 'ERROR'
        Write-Host "  ✗ Failed to connect to $($config.SqlServer): $_" -ForegroundColor Red
        return $false
    }
}

function Initialize-Database {
    Write-Section "Initializing Database"

    if ($DryRun) {
        Write-Log "DRYRUN: Would initialize database $($config.SqlDatabase)"
        return
    }

    $sqlConnectionString = "Server=$($config.SqlServer);Database=master;Integrated Security=true;"

    # Create database
    $createDbSql = @"
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '$($config.SqlDatabase)')
BEGIN
    CREATE DATABASE [$($config.SqlDatabase)]
    ALTER DATABASE [$($config.SqlDatabase)] SET RECOVERY FULL
    ALTER DATABASE [$($config.SqlDatabase)] SET PAGE_VERIFY CHECKSUM
END
"@

    try {
        Invoke-Sqlcmd -ServerInstance $config.SqlServer -Query $createDbSql -ErrorAction Stop
        Write-Log "Database $($config.SqlDatabase) initialized" 'SUCCESS'
        Write-Host "  ✓ Database created/verified" -ForegroundColor Green
    } catch {
        Write-Log "Database initialization failed: $_" 'ERROR'
        throw
    }

    # Create login and user
    $createUserSql = @"
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = '$($config.SqlUser)')
BEGIN
    CREATE LOGIN [$($config.SqlUser)] FROM WINDOWS
END

USE [$($config.SqlDatabase)]

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = '$($config.SqlUser)')
BEGIN
    CREATE USER [$($config.SqlUser)] FROM LOGIN [$($config.SqlUser)]
    ALTER ROLE db_owner ADD MEMBER [$($config.SqlUser)]
END
"@

    try {
        Invoke-Sqlcmd -ServerInstance $config.SqlServer -Query $createUserSql -ErrorAction Stop
        Write-Log "Database user $($config.SqlUser) configured" 'SUCCESS'
        Write-Host "  ✓ Database user configured" -ForegroundColor Green
    } catch {
        Write-Log "Database user configuration failed: $_" 'ERROR'
        throw
    }
}

# ============================================================================
# Database Migrations
# ============================================================================

function Apply-Migrations {
    Write-Section "Applying Database Migrations"

    if ($DryRun) {
        Write-Log "DRYRUN: Would apply database migrations"
        return
    }

    $dbConnectionString = "Server=$($config.SqlServer);Database=$($config.SqlDatabase);Integrated Security=true;"
    $env:IDENTITY_DATABASE_CONNECTION = $dbConnectionString

    try {
        Set-Location $config.RepositoryRoot
        Write-Log "Running migrations from: $($config.RepositoryRoot)"

        $result = & dotnet run --project IAM/src/Backend/Identity.Database/Identity.Database.csproj 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Log "Database migrations applied successfully" 'SUCCESS'
            Write-Host "  ✓ Migrations applied" -ForegroundColor Green
        } else {
            throw "Migration runner exited with code $LASTEXITCODE"
        }
    } catch {
        Write-Log "Migration failed: $_" 'ERROR'
        throw
    } finally {
        Remove-Item env:\IDENTITY_DATABASE_CONNECTION -ErrorAction SilentlyContinue
    }
}

# ============================================================================
# Secrets Generation
# ============================================================================

function Get-SecureRandomBytes {
    param([int]$Length = 32)
    $bytes = New-Object byte[] $Length
    [System.Security.Cryptography.RNGCryptoServiceProvider]::new().GetBytes($bytes)
    return $bytes
}

function Initialize-Secrets {
    Write-Section "Initializing Security Secrets"

    $secretsFile = Join-Path $config.DeploymentPath "secrets.json"

    if (Test-Path $secretsFile) {
        Write-Log "Secrets already exist: $secretsFile"
        Write-Host "  ✓ Using existing secrets" -ForegroundColor Green
        return
    }

    if ($DryRun) {
        Write-Log "DRYRUN: Would generate new secrets"
        return
    }

    Write-Host "  Generating security keys..." -ForegroundColor Cyan

    # Generate encryption keys
    $encryptionKey = [Convert]::ToBase64String((Get-SecureRandomBytes 32))
    $challengeKey = [Convert]::ToBase64String((Get-SecureRandomBytes 32))
    $identifierHashKey = [Convert]::ToBase64String((Get-SecureRandomBytes 32))

    # Generate RSA key
    $rsa = [System.Security.Cryptography.RSA]::Create(2048)
    $privateKeyPem = $rsa.ExportRSAPrivateKeyPem()
    $keyId = "uat-$(Get-Date -Format 'yyyy-MM')-$([guid]::NewGuid().ToString().Substring(0,8))"

    # Create secrets object
    $secrets = @{
        Issuer               = $config.PublicUrl
        KeyId                = $keyId
        PrivateKeyPem        = $privateKeyPem
        EncryptionKey        = $encryptionKey
        ChallengeKey         = $challengeKey
        IdentifierHashKey    = $identifierHashKey
        BootstrapPassword    = [Convert]::ToBase64String((Get-SecureRandomBytes 16))
        BootstrapClientSecret = [Convert]::ToBase64String((Get-SecureRandomBytes 24))
        GeneratedAt          = (Get-Date -Format 'o')
        Environment          = $config.Environment
    }

    # Save secrets securely
    $secrets | ConvertTo-Json -Depth 10 | Set-Content -Path $secretsFile
    (Get-Item $secretsFile).Attributes = 'Hidden'

    Write-Log "Security secrets generated: $secretsFile" 'SUCCESS'
    Write-Host "  ✓ Secrets generated and stored securely" -ForegroundColor Green
    Write-Host "  ⚠ IMPORTANT: Keep $secretsFile secure - it contains private keys" -ForegroundColor Yellow
}

# ============================================================================
# Bootstrap IAM
# ============================================================================

function Invoke-Bootstrap {
    Write-Section "Bootstrapping IAM"

    $secretsFile = Join-Path $config.DeploymentPath "secrets.json"
    $secrets = Get-Content $secretsFile | ConvertFrom-Json

    if ($DryRun) {
        Write-Log "DRYRUN: Would bootstrap IAM admin user"
        return
    }

    $env:IDENTITY_DATABASE_CONNECTION = "Server=$($config.SqlServer);Database=$($config.SqlDatabase);Integrated Security=true;"
    $env:IDENTITY_JWT_ISSUER = $secrets.Issuer
    $env:IDENTITY_JWT_KEY_ID = $secrets.KeyId
    $env:IDENTITY_JWT_PRIVATE_KEY_PEM = $secrets.PrivateKeyPem
    $env:IDENTITY_SECURITY_KEY_ID = $secrets.KeyId
    $env:IDENTITY_ENCRYPTION_KEY = $secrets.EncryptionKey
    $env:IDENTITY_CHALLENGE_KEY = $secrets.ChallengeKey
    $env:IDENTITY_IDENTIFIER_HASH_KEY = $secrets.IdentifierHashKey
    $env:IDENTITY_BOOTSTRAP_PASSWORD = $secrets.BootstrapPassword
    $env:IDENTITY_BOOTSTRAP_CLIENT_SECRET = $secrets.BootstrapClientSecret

    try {
        Set-Location $config.RepositoryRoot

        $bootstrapResult = & dotnet run --project IAM/src/Backend/Identity.AdminCli/Identity.AdminCli.csproj -- bootstrap `
            --employee-code "ADMIN-001" `
            --display-name "UAT Administrator" `
            --client-id "iam-uat-admin" `
            --email "admin@fujitecindia.com" 2>&1

        if ($LASTEXITCODE -eq 0) {
            $resultFile = Join-Path $config.DeploymentPath "bootstrap-result.json"
            $bootstrapResult | Out-String | Set-Content $resultFile
            Write-Log "Bootstrap completed successfully" 'SUCCESS'
            Write-Host "  ✓ Admin user created" -ForegroundColor Green
        } else {
            throw "Bootstrap failed with exit code $LASTEXITCODE"
        }
    } catch {
        Write-Log "Bootstrap failed: $_" 'ERROR'
        throw
    } finally {
        Remove-Item env:\IDENTITY_* -ErrorAction SilentlyContinue
    }
}

# ============================================================================
# Publish Application
# ============================================================================

function Publish-Application {
    Write-Section "Publishing IAM Application"

    if ($DryRun) {
        Write-Log "DRYRUN: Would publish IAM application"
        return
    }

    try {
        Set-Location $config.RepositoryRoot

        $publishOutput = Join-Path $config.ArtifactPath "publish-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

        Write-Log "Publishing to: $publishOutput"

        & .\IAM\scripts\Publish-Identity.ps1 `
            -Configuration Release `
            -OutputRoot $publishOutput `
            -ArtifactsOnly

        if ($LASTEXITCODE -eq 0) {
            Write-Log "Application published successfully: $publishOutput" 'SUCCESS'
            Write-Host "  ✓ Published to $publishOutput" -ForegroundColor Green
            return $publishOutput
        } else {
            throw "Publish failed with exit code $LASTEXITCODE"
        }
    } catch {
        Write-Log "Publish failed: $_" 'ERROR'
        throw
    }
}

# ============================================================================
# IIS Configuration
# ============================================================================

function Setup-IisInfrastructure {
    Write-Section "Setting Up IIS Infrastructure"

    if ($DryRun) {
        Write-Log "DRYRUN: Would configure IIS"
        return
    }

    # Import IIS module
    Import-Module WebAdministration -ErrorAction Stop

    # Create application pools
    Write-Host "  Creating application pools..." -ForegroundColor Cyan

    @(
        @{ Name = $config.IisApiPoolName; ManagedRuntime = 'v4.0'; Pipeline = 'Integrated' },
        @{ Name = $config.IisWebPoolName; ManagedRuntime = 'v4.0'; Pipeline = 'Integrated' }
    ) | ForEach-Object {
        if (-not (Test-Path "IIS:\AppPools\$($_.Name)")) {
            $pool = New-WebAppPool -Name $_.Name
            $pool.ManagedRuntimeVersion = $_.ManagedRuntime
            $pool.ManagedPipelineMode = $_.Pipeline
            $pool.StartMode = 'AlwaysRunning'
            $pool.AutoStart = $true
            $pool | Set-Item
            Write-Log "Created IIS app pool: $($_.Name)" 'SUCCESS'
        }
    }

    # Create website
    Write-Host "  Creating website..." -ForegroundColor Cyan

    if (-not (Test-Path "IIS:\Sites\$($config.IisSiteName)")) {
        $physicalPath = Join-Path $config.DeploymentPath 'webapp'
        New-Item -ItemType Directory -Path $physicalPath -Force | Out-Null

        New-WebSite `
            -Name $config.IisSiteName `
            -PhysicalPath $physicalPath `
            -Port 80 `
            -HostHeader $config.Hostname `
            -ApplicationPool $config.IisWebPoolName

        Write-Log "Created IIS website: $($config.IisSiteName)" 'SUCCESS'
    }

    # Create API application
    Write-Host "  Creating API application..." -ForegroundColor Cyan

    $apiAppPath = "IIS:\Sites\$($config.IisSiteName)\identity"
    if (-not (Test-Path $apiAppPath)) {
        $apiPhysicalPath = Join-Path $config.DeploymentPath 'api'
        New-Item -ItemType Directory -Path $apiPhysicalPath -Force | Out-Null

        New-WebApplication `
            -Site $config.IisSiteName `
            -Name 'identity' `
            -PhysicalPath $apiPhysicalPath `
            -ApplicationPool $config.IisApiPoolName

        Write-Log "Created IIS API application" 'SUCCESS'
    }

    Write-Host "  ✓ IIS infrastructure ready" -ForegroundColor Green
}

# ============================================================================
# Deploy Application
# ============================================================================

function Deploy-Application {
    param([string]$PublishPath)

    Write-Section "Deploying Application"

    if ($DryRun) {
        Write-Log "DRYRUN: Would deploy application from $PublishPath"
        return
    }

    # Deploy frontend
    Write-Host "  Deploying frontend..." -ForegroundColor Cyan
    $frontendSource = Join-Path $PublishPath 'frontend'
    $frontendDest = Join-Path $config.DeploymentPath 'webapp'

    if (Test-Path $frontendSource) {
        Copy-Item -Path "$frontendSource\*" -Destination $frontendDest -Recurse -Force
        Write-Log "Frontend deployed" 'SUCCESS'
    }

    # Deploy API
    Write-Host "  Deploying API..." -ForegroundColor Cyan
    $apiSource = Join-Path $PublishPath 'api'
    $apiDest = Join-Path $config.DeploymentPath 'api'

    if (Test-Path $apiSource) {
        if (-not (Test-Path $apiDest)) {
            New-Item -ItemType Directory -Path $apiDest -Force | Out-Null
        }
        Copy-Item -Path "$apiSource\*" -Destination $apiDest -Recurse -Force
        Write-Log "API deployed" 'SUCCESS'
    }

    # Deploy database runner
    Write-Host "  Deploying database runner..." -ForegroundColor Cyan
    $dbSource = Join-Path $PublishPath 'database'
    $dbDest = Join-Path $config.DeploymentPath 'database'

    if (Test-Path $dbSource) {
        if (-not (Test-Path $dbDest)) {
            New-Item -ItemType Directory -Path $dbDest -Force | Out-Null
        }
        Copy-Item -Path "$dbSource\*" -Destination $dbDest -Recurse -Force
        Write-Log "Database runner deployed" 'SUCCESS'
    }

    Write-Host "  ✓ Application deployed" -ForegroundColor Green
}

# ============================================================================
# Configuration
# ============================================================================

function Configure-Application {
    Write-Section "Configuring Application"

    $secretsFile = Join-Path $config.DeploymentPath "secrets.json"
    $secrets = Get-Content $secretsFile | ConvertFrom-Json

    # Frontend config
    $frontendConfigPath = Join-Path $config.DeploymentPath "webapp\config.json"
    $frontendConfig = @{
        identityBaseUrl              = "/identity"
        identityAdministrationUrl    = "https://iam-uat.fujitecindia.com"
        identityClientId             = "iam-uat-admin"
        applicationName              = "IAM UAT Administration"
        devExtremeLicenseKey         = ""
        theme                        = "system"
    } | ConvertTo-Json -Depth 10

    if ($DryRun) {
        Write-Log "DRYRUN: Would create frontend config"
    } else {
        Set-Content -Path $frontendConfigPath -Value $frontendConfig
        Write-Log "Frontend config created: $frontendConfigPath" 'SUCCESS'
    }

    # API appsettings
    $apiConfigPath = Join-Path $config.DeploymentPath "api\appsettings.Production.json"
    $apiConfig = @{
        Identity = @{
            Jwt = @{
                Issuer          = $secrets.Issuer
                KeyId           = $secrets.KeyId
                AdministrationAudience = "iam-administration"
            }
            BrowserSession = @{
                CookieName          = "__Secure-identity-refresh-uat"
                CookiePath          = "/identity"
                RequireSecureCookie = $false
            }
            RateLimit = @{
                AuthenticationPermitLimit = 10
            }
        }
        AllowedHosts = "$($config.Hostname);localhost;127.0.0.1"
        Logging = @{
            LogLevel = @{
                Default = "Information"
                Microsoft = "Warning"
            }
        }
    } | ConvertTo-Json -Depth 10

    if ($DryRun) {
        Write-Log "DRYRUN: Would create API config"
    } else {
        Set-Content -Path $apiConfigPath -Value $apiConfig
        Write-Log "API config created: $apiConfigPath" 'SUCCESS'
    }

    Write-Host "  ✓ Application configured" -ForegroundColor Green
}

# ============================================================================
# Verification
# ============================================================================

function Test-Deployment {
    Write-Section "Verifying Deployment"

    if ($DryRun) {
        Write-Log "DRYRUN: Would verify deployment"
        return
    }

    $checks = @(
        @{ Name = "Database"; Test = { Test-SqlConnection } },
        @{ Name = "API Health"; Test = { (Invoke-WebRequest "http://localhost:18100/health/live" -ErrorAction SilentlyContinue).StatusCode -eq 200 } },
        @{ Name = "Discovery"; Test = { (Invoke-WebRequest "http://localhost:18100/.well-known/openid-configuration" -ErrorAction SilentlyContinue).StatusCode -eq 200 } }
    )

    Write-Host "  Running verification checks..." -ForegroundColor Cyan

    foreach ($check in $checks) {
        try {
            $result = & $check.Test
            if ($result) {
                Write-Log "$($check.Name): ✓ PASS" 'SUCCESS'
                Write-Host "    ✓ $($check.Name)" -ForegroundColor Green
            } else {
                Write-Log "$($check.Name): ⚠ FAIL" 'WARNING'
                Write-Host "    ⚠ $($check.Name)" -ForegroundColor Yellow
            }
        } catch {
            Write-Log "$($check.Name): ✗ ERROR - $_" 'ERROR'
            Write-Host "    ✗ $($check.Name): $_" -ForegroundColor Red
        }
    }
}

# ============================================================================
# Main Execution
# ============================================================================

try {
    Write-Host "`n" -ForegroundColor Cyan
    Write-Host "╔════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║  IAM UAT Automated Deployment Script                              ║" -ForegroundColor Cyan
    Write-Host "║  Environment: $($config.Environment,-30) Hostname: $($config.Hostname) ║" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""

    if ($DryRun) {
        Write-Host "⚠ DRY-RUN MODE - No changes will be made" -ForegroundColor Yellow
    }

    Write-Log "=== IAM UAT Deployment Started ==="

    Test-Prerequisites
    Initialize-Directories
    Test-SqlConnection
    Initialize-Database
    Apply-Migrations
    Initialize-Secrets
    Invoke-Bootstrap
    $publishPath = Publish-Application
    Setup-IisInfrastructure
    Deploy-Application -PublishPath $publishPath
    Configure-Application
    Test-Deployment

    Write-Section "Deployment Complete"
    Write-Host "  ✓ IAM UAT deployment successful!" -ForegroundColor Green
    Write-Host "  ✓ Access: $($config.PublicUrl)" -ForegroundColor Green
    Write-Host "  ✓ Admin Login: ADMIN-001" -ForegroundColor Green
    Write-Host "  ✓ Logs: $($config.LogPath)" -ForegroundColor Green
    Write-Log "=== IAM UAT Deployment Completed Successfully ===" 'SUCCESS'

} catch {
    Write-Log "FATAL ERROR: $_" 'ERROR'
    Write-Host "`n✗ Deployment failed: $_" -ForegroundColor Red
    exit 1
}
