param([ValidateSet('Initialize','Api','Password')][string]$Mode = 'Api')
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Run with PowerShell 7 on Windows.' }
$projectRoot = Split-Path $PSScriptRoot -Parent
$stateDirectory = Join-Path $projectRoot 'artifacts/local-test'
$secretFile = Join-Path $stateDirectory 'secrets.clixml'
$sqlServer = 'lpc:.\SQLEXPRESS2022'
$databaseName = 'IAM_LocalTest'
$connection = 'Server=lpc:.\SQLEXPRESS2022;Database=IAM_LocalTest;Integrated Security=True;Encrypt=False;Connect Timeout=10'
$databaseDll = Join-Path $projectRoot 'src/Backend/Identity.Database/bin/Debug/net10.0/Identity.Database.dll'
$adminDll = Join-Path $projectRoot 'src/Backend/Identity.AdminCli/bin/Debug/net10.0/Identity.AdminCli.dll'
function Invoke-CheckedDotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet command failed (exit $LASTEXITCODE). Stopped without retrying." }
}
if ($Mode -eq 'Initialize') {
    if (Test-Path $secretFile) { throw 'Local test secrets already exist. Refusing to replace them. Use -Mode Api.' }
    $sqlcmd = (Get-Command sqlcmd -ErrorAction Stop).Source
    & $sqlcmd -S $sqlServer -d master -E -C -l 5 -b -Q "IF DB_ID(N'IAM_LocalTest') IS NOT NULL THROW 51000, 'IAM_LocalTest already exists; initialization refused.', 1; CREATE DATABASE [IAM_LocalTest];"
    if ($LASTEXITCODE -ne 0) { throw 'Database creation failed. No initialization attempted.' }
    New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
    $rsa = [Security.Cryptography.RSA]::Create(3072)
    try {
        $generated = @{
            PrivateKeyPem = $rsa.ExportPkcs8PrivateKeyPem()
            EncryptionKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
            ChallengeKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
            IdentifierHashKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
            Password = 'Local!9a' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(10))
            ClientSecret = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
        }
        $protected = @{}
        foreach ($name in $generated.Keys) { $protected[$name] = ConvertTo-SecureString $generated[$name] -AsPlainText -Force }
        $protected | Export-Clixml -LiteralPath $secretFile
        $generated.Clear()
    } finally { $rsa.Dispose() }
}
if (-not (Test-Path $secretFile)) { throw 'Initialize the isolated test database first with -Mode Initialize.' }
$protected = Import-Clixml -LiteralPath $secretFile
function Read-LocalSecret([string]$Name) { [Net.NetworkCredential]::new('', $protected[$Name]).Password }
if ($Mode -eq 'Password') { Write-Output (Read-LocalSecret 'Password'); return }
$env:ConnectionStrings__Identity = $connection
$env:Identity__Jwt__Issuer = 'http://localhost:5000'
$env:Identity__Jwt__KeyId = 'iam-local-test-signing-v1'
$env:Identity__Jwt__PrivateKeyPem = Read-LocalSecret 'PrivateKeyPem'
$env:Identity__Jwt__AdministrationAudience = 'urn:identity:administration'
$env:Identity__Security__KeyId = 'iam-local-test-protection-v1'
$env:Identity__Security__EncryptionKey = Read-LocalSecret 'EncryptionKey'
$env:Identity__Security__ChallengeKey = Read-LocalSecret 'ChallengeKey'
$env:Identity__Security__IdentifierHashKey = Read-LocalSecret 'IdentifierHashKey'
$env:Identity__BrowserSession__CookieName = 'iam-local-test-refresh'
$env:Identity__BrowserSession__CookiePath = '/identity'
$env:Identity__BrowserSession__RequireSecureCookie = 'false'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Logging__EventLog__LogLevel__Default = 'None'
$env:AllowedHosts = 'localhost;127.0.0.1'
if ($Mode -eq 'Initialize') {
    $env:IDENTITY_DATABASE_CONNECTION = $connection
    Invoke-CheckedDotnet @($databaseDll, '--expected-server', $sqlServer, '--expected-database', $databaseName, '--receipt', (Join-Path $stateDirectory 'migration-receipt.json'))
    $env:IDENTITY_JWT_ISSUER = $env:Identity__Jwt__Issuer
    $env:IDENTITY_JWT_KEY_ID = $env:Identity__Jwt__KeyId
    $env:IDENTITY_JWT_PRIVATE_KEY_PEM = $env:Identity__Jwt__PrivateKeyPem
    $env:IDENTITY_SECURITY_KEY_ID = $env:Identity__Security__KeyId
    $env:IDENTITY_ENCRYPTION_KEY = $env:Identity__Security__EncryptionKey
    $env:IDENTITY_CHALLENGE_KEY = $env:Identity__Security__ChallengeKey
    $env:IDENTITY_IDENTIFIER_HASH_KEY = $env:Identity__Security__IdentifierHashKey
    try {
        $env:IDENTITY_BOOTSTRAP_PASSWORD = Read-LocalSecret 'Password'
        $env:IDENTITY_BOOTSTRAP_CLIENT_SECRET = Read-LocalSecret 'ClientSecret'
        Invoke-CheckedDotnet @($adminDll, 'bootstrap', '--employee-code', 'IND04316', '--display-name', 'Keerthivasan Admin', '--client-id', 'local-test-bootstrap')
        Invoke-CheckedDotnet @($adminDll, 'provision-administration-web', '--employee-code', 'IND04316')
    } finally {
        Remove-Item Env:IDENTITY_BOOTSTRAP_PASSWORD,Env:IDENTITY_BOOTSTRAP_CLIENT_SECRET -ErrorAction SilentlyContinue
    }
    Write-Output 'IAM_LocalTest initialized. Shared FIN_IAM was not targeted. Start with -Mode Api.'
    return
}
Write-Output 'Starting API on localhost:5000 with isolated IAM_LocalTest. HTTP cookies are LOCAL TEST ONLY.'
Invoke-CheckedDotnet @('run', '--no-build', '--project', (Join-Path $projectRoot 'src/Backend/Identity.Api'), '--launch-profile', 'IAM API')
