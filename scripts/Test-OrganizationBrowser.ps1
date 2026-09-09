[CmdletBinding()]
param([ValidateRange(1024,65535)][int]$ApiPort = 5107, [ValidateRange(1024,65535)][int]$WebPort = 4303)
$ErrorActionPreference = 'Stop'
$iamRoot = Split-Path -Parent $PSScriptRoot
$server = 'lpc:HOCOM18502627\SQLEXPRESS2022'
$database = 'FIN_IAM_OrgBrowserTests_' + (Get-Date -Format yyyyMMdd) + '_' + [Guid]::NewGuid().ToString('N').Substring(0,8)
if ($database -notmatch '^FIN_IAM_OrgBrowserTests_[0-9]{8}_[a-f0-9]{8}$') { throw 'Unsafe disposable database name.' }
foreach ($port in @($ApiPort,$WebPort)) {
    if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) { throw "Port $port is already in use." }
}
$artifacts = Join-Path $iamRoot "artifacts/organization-verification/$database"
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$api = $null
$created = $false
$priorEnvironment = @{}
function Set-TestEnvironment([string]$Name, [string]$Value) {
    if (-not $priorEnvironment.ContainsKey($Name)) { $priorEnvironment[$Name] = [Environment]::GetEnvironmentVariable($Name) }
    [Environment]::SetEnvironmentVariable($Name, $Value)
}
function Assert-Exit([string]$Step) { if ($LASTEXITCODE -ne 0) { throw "$Step failed with exit code $LASTEXITCODE." } }
function Random-Key { [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) }
try {
    $actualServer = (& sqlcmd -S $server -E -N -C -d master -h -1 -W -b -Q 'SET NOCOUNT ON; SELECT @@SERVERNAME;').Trim()
    Assert-Exit 'SQL server verification'
    if ($actualServer -ne 'HOCOM18502627\SQLEXPRESS2022') { throw 'Unexpected SQL server.' }
    Write-Output "Creating disposable target $database on $actualServer. Business databases are excluded."
    & sqlcmd -S $server -E -N -C -d master -b -Q "CREATE DATABASE [$database];"
    Assert-Exit 'Disposable database creation'
    $created = $true
    $connection = "Server=$server;Database=$database;Integrated Security=true;Encrypt=true;TrustServerCertificate=true"
    Set-TestEnvironment 'IDENTITY_DATABASE_CONNECTION' $connection
    & dotnet run --project (Join-Path $iamRoot 'src/Backend/Identity.Database') -c Release --no-build --no-restore
    Assert-Exit 'Migration application'
    & dotnet run --project (Join-Path $iamRoot 'src/Backend/Identity.Database') -c Release --no-build --no-restore
    Assert-Exit 'Migration replay'
    $rsa = [System.Security.Cryptography.RSA]::Create(2048)
    try { $pem = $rsa.ExportPkcs8PrivateKeyPem() } finally { $rsa.Dispose() }
    $issuer = "http://127.0.0.1:$ApiPort"
    $password = (Random-Key) + '!aA4'
    $encryption = Random-Key; $challenge = Random-Key; $identifier = Random-Key
    $settings = @{
        IDENTITY_JWT_ISSUER=$issuer; IDENTITY_JWT_KEY_ID='disposable-browser-key'; IDENTITY_JWT_PRIVATE_KEY_PEM=$pem
        IDENTITY_SECURITY_KEY_ID='disposable-browser-protection'; IDENTITY_ENCRYPTION_KEY=$encryption
        IDENTITY_CHALLENGE_KEY=$challenge; IDENTITY_IDENTIFIER_HASH_KEY=$identifier
        IDENTITY_BOOTSTRAP_PASSWORD=$password; IDENTITY_BOOTSTRAP_CLIENT_SECRET=(Random-Key)
        ConnectionStrings__Identity=$connection; Identity__Jwt__Issuer=$issuer; Identity__Jwt__KeyId='disposable-browser-key'
        Identity__Jwt__PrivateKeyPem=$pem; Identity__Jwt__AdministrationAudience='urn:identity:administration'
        Identity__Security__KeyId='disposable-browser-protection'; Identity__Security__EncryptionKey=$encryption
        Identity__Security__ChallengeKey=$challenge; Identity__Security__IdentifierHashKey=$identifier
        Identity__BrowserSession__CookieName='iam-e2e-refresh'; Identity__BrowserSession__CookiePath='/identity'
        Identity__BrowserSession__RequireSecureCookie='false'; Identity__RateLimit__AuthenticationPermitLimit='100'
        Logging__LogLevel__Default='Warning'; AllowedHosts='127.0.0.1;localhost'; ASPNETCORE_ENVIRONMENT='Production'
        IAM_E2E_DATABASE=$database; IAM_E2E_PASSWORD=$password; IAM_E2E_WEB_PORT=$WebPort.ToString()
    }
    foreach ($name in $settings.Keys) { Set-TestEnvironment $name $settings[$name] }
    $adminDll = Join-Path $iamRoot 'src/Backend/Identity.AdminCli/bin/Release/net10.0/Identity.AdminCli.dll'
    $bootstrap = & dotnet $adminDll bootstrap --employee-code IAM-E2E-ADMIN --display-name 'Browser test administrator' --client-id iam-e2e-confidential
    Assert-Exit 'Disposable administrator bootstrap'
    $bootstrapResult = $bootstrap | ConvertFrom-Json
    & dotnet $adminDll provision-application --manifest (Join-Path $PSScriptRoot 'fixtures/organization-browser-application.json') --actor-user-id $bootstrapResult.AdministratorUserId.ToString()
    Assert-Exit 'Disposable web client provisioning'
    Set-TestEnvironment 'IAM_E2E_APP_ID' $bootstrapResult.ApplicationId.ToString()
    $proxyPath = Join-Path $artifacts 'proxy.json'
    @{ '/identity' = @{target=$issuer;secure=$false;changeOrigin=$true;pathRewrite=@{'^/identity'=''}} } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $proxyPath
    Set-TestEnvironment 'IAM_E2E_PROXY_PATH' $proxyPath
    $apiDll = Join-Path $iamRoot 'src/Backend/Identity.Api/bin/Release/net10.0/Identity.Api.dll'
    $api = Start-Process dotnet -ArgumentList @(('"' + $apiDll + '"'),'--urls',$issuer) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $artifacts 'api.stdout.log') -RedirectStandardError (Join-Path $artifacts 'api.stderr.log')
    $ready = $false
    for ($attempt=0; $attempt -lt 40; $attempt++) {
        if ($api.HasExited) { throw 'Disposable API exited during startup. Inspect the safe API logs.' }
        try { $response = Invoke-WebRequest "$issuer/health/ready" -TimeoutSec 2; if ($response.StatusCode -eq 200) { $ready=$true; break } } catch { Start-Sleep -Milliseconds 250 }
    }
    if (-not $ready) { throw 'Disposable API did not become ready.' }
    Push-Location (Join-Path $iamRoot 'src/Frontend')
    try { & npm run test:e2e:live; Assert-Exit 'Actual browser/API/SQL tests' } finally { Pop-Location }
    & sqlcmd -S $server -E -N -C -d $database -b -Q @"
SET NOCOUNT ON;
IF EXISTS(SELECT 1 FROM [Identity].[OrganizationUnit] WHERE HierarchyPath IS NULL) THROW 51020, 'Incomplete path committed.', 1;
IF EXISTS(SELECT 1 FROM [Identity].[Organization] o LEFT JOIN [Identity].[OrganizationUnit] u ON u.OrganizationId=o.OrganizationId AND u.UnitType=N'Organization' WHERE u.OrganizationUnitId IS NULL) THROW 51021, 'Orphan root anchor.', 1;
IF (SELECT COUNT(DISTINCT UnitType) FROM [Identity].[OrganizationUnit]) <> 8 THROW 51022, 'Missing unit type.', 1;
IF (SELECT COUNT(*) FROM dbo.SchemaVersions) <> 10 THROW 51023, 'Unexpected migration history.', 1;
IF NOT EXISTS(SELECT 1 FROM [Identity].[AuthenticationAudit] WHERE EventType=N'OrganizationUnitUpdated') THROW 51024, 'Update audit missing.', 1;
IF NOT EXISTS(SELECT 1 FROM [Identity].[AuthenticationAudit] WHERE EventType=N'OrganizationUnitStateChanged') THROW 51025, 'State audit missing.', 1;
SELECT DB_NAME() AS VerifiedDisposableTarget, COUNT(*) AS Units FROM [Identity].[OrganizationUnit];
SELECT EventType,COUNT(*) AS EventCount FROM [Identity].[AuthenticationAudit] WHERE EventType LIKE N'Organization%' GROUP BY EventType;
"@
    Assert-Exit 'SQL readback and audit verification'
    Write-Output 'Actual UI -> API -> disposable SQL verification passed.'
} finally {
    if ($api -and -not $api.HasExited) { Stop-Process -Id $api.Id -Force; $api.WaitForExit() }
    foreach ($name in $priorEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name,$priorEnvironment[$name]) }
    if ($created) {
        if ($database -notmatch '^FIN_IAM_OrgBrowserTests_[0-9]{8}_[a-f0-9]{8}$') { throw 'Unsafe cleanup target.' }
        $actualTarget = (& sqlcmd -S $server -E -N -C -d $database -h -1 -W -b -Q 'SET NOCOUNT ON; SELECT DB_NAME();').Trim()
        Assert-Exit 'Cleanup target verification'
        if ($actualTarget -ne $database) { throw 'Cleanup database mismatch.' }
        Write-Output "Verified disposable cleanup target: $actualTarget"
        & sqlcmd -S $server -E -N -C -d master -b -Q "ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database];"
        Assert-Exit 'Disposable cleanup'
    }
}
