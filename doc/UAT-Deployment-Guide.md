# IAM UAT Automated Deployment Guide

## Quick Start (5 minutes)

### Prerequisites
- Windows Server 2019+ on `fujitecapp2`
- PowerShell 5.1 (Run as Administrator)
- SQL Server 2022 (SQLDEVELOPER2022 instance)
- .NET 10 SDK installed
- Node.js and npm installed
- IIS with URL Rewrite, ARR, WebSockets enabled

### One-Command Deployment

```powershell
# From repo root, administrator PowerShell
cd C:\Users\siddeswaran.s\Documents\ChatGPT\FIN_PTS
.\IAM\scripts\Deploy-UAT-Full.ps1
```

**Expected runtime:** 8-12 minutes (depends on build time)

**Output:** All messages logged to `C:\IAM-UAT-Logs\`

---

## Full Deployment Details

### Environment Configuration

| Setting | Value |
|---------|-------|
| **Hostname** | `iam-uat.fujitecindia.com` |
| **Public URL** | `https://iam-uat.fujitecindia.com` |
| **Internal Server** | `fujitecapp2` |
| **SQL Server** | `fujitecapp2\SQLDEVELOPER2022` |
| **SQL Database** | `FIN_IAM_UAT` |
| **API Port** | `127.0.0.1:18100` (internal only) |
| **IIS Site** | `IAM-UAT-Web` |
| **Deployment Root** | `C:\IAM-UAT-Deployment` |
| **Admin Login** | `ADMIN-001` |

### What the Script Does (Automated)

1. **Validates prerequisites** — PowerShell, .NET, Node, IIS, SQL Server
2. **Creates directories** — Deployment, artifacts, logs
3. **Initializes database** — Creates `FIN_IAM_UAT` if missing
4. **Generates secrets** — RSA keys, encryption keys, random passwords
5. **Applies migrations** — Schema versions 0001-0018
6. **Bootstraps admin** — Initial IAM administrator account
7. **Publishes application** — Builds API, frontend, database runner
8. **Configures IIS** — Creates site, app pools, applications
9. **Deploys files** — Copies binaries and static content
10. **Configures app** — Creates config.json and appsettings
11. **Verifies deployment** — Health checks, connectivity tests

---

## Deployment Modes

### Standard Deployment (Default)
Executes all steps, modifies infrastructure, creates databases, deploys code.

```powershell
.\IAM\scripts\Deploy-UAT-Full.ps1
```

### Dry-Run Mode
Shows what would be done without making changes. **Use this first!**

```powershell
.\IAM\scripts\Deploy-UAT-Full.ps1 -DryRun
```

### Verify Configuration
Check prerequisites without deployment:

```powershell
# Only validates, exits after
.\IAM\scripts\Deploy-UAT-Full.ps1 -DryRun
```

---

## Manual Verification After Deployment

### 1. Check IIS Sites
```powershell
Get-WebSite
Get-WebApplication
```

**Expected:**
- Site: `IAM-UAT-Web` (running)
- App: `identity` (running)
- App Pools: `IAM-UAT-Api`, `IAM-UAT-Web-Pool` (running)

### 2. Check Database
```powershell
sqlcmd -S "fujitecapp2\SQLDEVELOPER2022" -d FIN_IAM_UAT -Q "SELECT @@VERSION; SELECT * FROM SchemaVersions ORDER BY Version DESC;"
```

**Expected:**
- Database: `FIN_IAM_UAT` exists
- Latest migration: `0018_agent_collect_rejected_result`
- User: `iam-uat-app` has db_owner

### 3. Test API Health
```powershell
Invoke-WebRequest -Uri "http://127.0.0.1:18100/health/live" -ErrorAction SilentlyContinue
```

**Expected:** Status 200 OK

### 4. Test Discovery
```powershell
Invoke-WebRequest -Uri "http://127.0.0.1:18100/.well-known/openid-configuration" -ErrorAction SilentlyContinue | ConvertFrom-Json
```

**Expected:** Returns issuer, jwks_uri, algorithms

### 5. Browser Login
1. Open `https://iam-uat.fujitecindia.com`
2. Click "Sign In"
3. Employee Code: `ADMIN-001`
4. Password: (check `C:\IAM-UAT-Deployment\secrets.json` for `BootstrapPassword` — base64 decode it)
5. Should land on IAM dashboard

---

## Troubleshooting

### Script Fails on Prerequisites
```
✗ Administrator
```
**Fix:** Run PowerShell as Administrator (right-click → Run as administrator)

### SQL Server Connection Fails
```
✗ SQL Server connection failed
```
**Fix:**
```powershell
# Test connection manually
sqlcmd -S "fujitecapp2\SQLDEVELOPER2022" -Q "SELECT @@VERSION"
```

If fails, check:
- SQL Server is running: `Get-Service MSSQL$SQLDEVELOPER2022 | Start-Service`
- Windows authentication enabled in SQL Server
- Firewall allows named pipes

### Database Migration Timeout
**Cause:** Large schema initialization takes time  
**Fix:** Increase timeout in script (edit `Deploy-UAT-Full.ps1`, line ~280)

### IIS Site Won't Start
**Cause:** Port conflict, missing .NET Hosting Bundle  
**Fix:**
```powershell
# Install .NET 10 Hosting Bundle
# Download from https://dotnet.microsoft.com/en-us/download/dotnet
# Run installer, restart IIS

iisreset
```

### Secrets File Already Exists
**To regenerate:**
```powershell
Remove-Item "C:\IAM-UAT-Deployment\secrets.json"
.\IAM\scripts\Deploy-UAT-Full.ps1
```

⚠️ **Warning:** Regenerating invalidates all existing tokens!

---

## File Structure After Deployment

```
C:\IAM-UAT-Deployment\
├── webapp\                    # Static frontend files
├── api\                       # API binaries
├── database\                  # Database runner
├── secrets.json              # Private keys & passwords ⚠️ KEEP SECURE
├── bootstrap-result.json     # Admin user IDs
└── api\appsettings.Production.json

C:\IAM-UAT-Artifacts\
└── publish-YYYYMMDD-HHMMSS\  # Application build output

C:\IAM-UAT-Logs\
└── deployment-YYYYMMDD.log   # Deployment log file
```

---

## Secrets Management

### Location
`C:\IAM-UAT-Deployment\secrets.json` (auto-generated)

### Contains
```json
{
  "Issuer": "https://iam-uat.fujitecindia.com",
  "KeyId": "uat-2026-09-abc12345",
  "PrivateKeyPem": "-----BEGIN PRIVATE KEY-----...",
  "EncryptionKey": "base64-encoded-32-bytes",
  "ChallengeKey": "base64-encoded-32-bytes",
  "IdentifierHashKey": "base64-encoded-32-bytes",
  "BootstrapPassword": "base64-password",
  "BootstrapClientSecret": "base64-secret",
  "GeneratedAt": "2026-09-01T12:34:56.789Z",
  "Environment": "UAT"
}
```

### ⚠️ Security
- **Never commit to Git**
- **Never share** Private Key
- **Backup securely** (encrypted)
- **Rotate annually**
- **Restrict access** to administrators only

To use bootstrap password:
```powershell
$secrets = Get-Content "C:\IAM-UAT-Deployment\secrets.json" | ConvertFrom-Json
$password = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($secrets.BootstrapPassword))
Write-Host "Bootstrap Password: $password"
```

---

## Environment Variables (For Custom Deployments)

When running manual commands, set:

```powershell
$env:IDENTITY_DATABASE_CONNECTION = "Server=fujitecapp2\SQLDEVELOPER2022;Database=FIN_IAM_UAT;Integrated Security=true;"
$env:IDENTITY_JWT_ISSUER = "https://iam-uat.fujitecindia.com"
$env:IDENTITY_JWT_KEY_ID = "uat-2026-09-abc12345"
$env:IDENTITY_JWT_PRIVATE_KEY_PEM = "-----BEGIN PRIVATE KEY-----..."
$env:IDENTITY_SECURITY_KEY_ID = "uat-2026-09-abc12345"
$env:IDENTITY_ENCRYPTION_KEY = "base64-key"
$env:IDENTITY_CHALLENGE_KEY = "base64-key"
$env:IDENTITY_IDENTIFIER_HASH_KEY = "base64-key"
```

---

## Scaling to Production

### Step 1: Create Production Config
Copy `Deploy-UAT-Config.json` to include production environment:

```json
{
  "environments": {
    "uat": { ... },
    "prod": {
      "hostname": "iam.fujitecindia.com",
      "sqlServer": "prod-sql-server",
      "sqlDatabase": "FIN_IAM",
      ...
    }
  }
}
```

### Step 2: Adapt Script
Create `Deploy-Production-Full.ps1` from UAT script, update:
- Hostname: `iam.fujitecindia.com`
- SQL Server: production instance
- Certificate: production-signed (not self-signed)
- Backup strategy: daily with 30-day retention

### Step 3: Pre-Flight Checklist
- [ ] Network connectivity to production server
- [ ] SQL Server with production backups
- [ ] HTTPS certificate from trusted CA
- [ ] Firewall rules for `iam.fujitecindia.com`
- [ ] Admin access to production IIS
- [ ] Backup of existing production data (if any)
- [ ] Security review complete
- [ ] Change approval documented

### Step 4: Deploy
```powershell
.\IAM\scripts\Deploy-Production-Full.ps1 -DryRun
# Review output carefully
.\IAM\scripts\Deploy-Production-Full.ps1
```

---

## Daily Operations

### Start/Stop Services
```powershell
# IIS
iisreset /start    # Start
iisreset /stop     # Stop
iisreset           # Restart

# Individual app pool
Start-WebAppPool -Name "IAM-UAT-Api"
Stop-WebAppPool -Name "IAM-UAT-Api"
Restart-WebAppPool -Name "IAM-UAT-Api"
```

### View Logs
```powershell
# Deployment logs
Get-Content "C:\IAM-UAT-Logs\deployment-*.log" -Tail 50

# IIS logs
Get-Content "C:\inetpub\logs\LogFiles\W3SVC\*.log" -Tail 100

# API event logs
Get-WinEvent -LogName "Application" -FilterXPath "*[EventData[Data[@Name='ProviderName']='Identity.Api']]" | Select-Object TimeCreated, Message
```

### Backup Database
```powershell
Backup-SqlDatabase -ServerInstance "fujitecapp2\SQLDEVELOPER2022" -Database "FIN_IAM_UAT" -BackupFile "C:\Backups\FIN_IAM_UAT_$(Get-Date -Format 'yyyyMMdd_HHmmss').bak"
```

### Restore from Backup
```powershell
Restore-SqlDatabase -ServerInstance "fujitecapp2\SQLDEVELOPER2022" -BackupFile "C:\Backups\FIN_IAM_UAT_20260901_120000.bak" -Database "FIN_IAM_UAT"
```

---

## Monitoring & Health Checks

### Setup Automated Health Checks
```powershell
# Add to Task Scheduler
$trigger = New-ScheduledTaskTrigger -AtStartup
$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-Command `"Invoke-WebRequest 'http://127.0.0.1:18100/health/ready'`""
Register-ScheduledTask -TaskName "IAM-UAT-HealthCheck" -Trigger $trigger -Action $action -RunLevel Highest
```

### Check Status
```powershell
Get-ScheduledTaskInfo -TaskName "IAM-UAT-HealthCheck"
```

---

## Support & Documentation

- **Deployment Issues:** Check logs in `C:\IAM-UAT-Logs\`
- **Configuration Reference:** `IAM/doc/Identity-Operations.md`
- **API Reference:** `IAM/doc/Consumer-Integration.md`
- **Troubleshooting:** This guide's "Troubleshooting" section
- **Team Contact:** iam-admins@fujitecindia.com

---

**Version:** 1.0  
**Last Updated:** 2026-09-01  
**Status:** UAT Automation Complete
