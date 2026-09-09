# IAM.Agent bootstrap. Run in Windows PowerShell as administrator.
# Downloads only from this IAM installation; Windows execution policy remains enforced.
& {
    $ErrorActionPreference = 'Stop'
    Set-StrictMode -Version Latest

    function Expand-IamAgentSetup([string]$ZipPath, [string]$Destination, [string]$ExpectedHash) {
        if ((Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash -cne $ExpectedHash) {
            throw 'Agent setup checksum mismatch. Nothing was installed. Retry the command to get the current release.'
        }
        Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
        $archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
        try {
            $required = @('Install-IAMAgent.ps1', 'Uninstall-IAMAgent.ps1', 'Install.cmd', 'README.txt',
                'setup.json', 'latest.json', 'release-public.pem', 'IAM.Agent.Service.exe')
            # Keep earlier bundles supported; compact setup adds the original branding assets.
            $allowed = $required + @('AgentInstallerUi.ps1', 'fujitec-logo.png', 'favicon.ico')
            $names = @{}
            $total = 0L
            if ($archive.Entries.Count -notin @(9, 10, 12)) { throw 'Unexpected agent setup archive contents.' }
            foreach ($entry in $archive.Entries) {
                $name = $entry.FullName
                if (($name -cnotin $allowed -and $name -cnotmatch '^IAM\.Agent-\d{1,5}\.\d{1,5}\.\d{1,5}-win-x64\.zip$') -or
                    $names.ContainsKey($name) -or ($entry.ExternalAttributes -band 0x400) -ne 0 -or
                    (($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) {
                    throw 'Unsafe or unexpected file in agent setup archive.'
                }
                $names[$name] = $true
                $total += $entry.Length
                if ($total -gt 1GB) { throw 'Agent setup archive is too large.' }
            }
            foreach ($name in $required) {
                if (-not $names.ContainsKey($name)) { throw "Agent setup is missing $name." }
            }
            if ($names.ContainsKey('fujitec-logo.png') -or $names.ContainsKey('favicon.ico')) {
                foreach ($name in @('AgentInstallerUi.ps1', 'fujitec-logo.png', 'favicon.ico')) {
                    if (-not $names.ContainsKey($name)) { throw 'Incomplete branded agent setup.' }
                }
            }
            if (@($names.Keys | Where-Object { $_ -cmatch '^IAM\.Agent-\d{1,5}\.\d{1,5}\.\d{1,5}-win-x64\.zip$' }).Count -ne 1) {
                throw 'Agent setup must contain exactly one worker package.'
            }
            if (Test-Path -LiteralPath $Destination) { throw 'Agent extraction directory already exists.' }
            [void][IO.Directory]::CreateDirectory($Destination)
            foreach ($entry in $archive.Entries) {
                [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $Destination $entry.FullName))
            }
        } finally { $archive.Dispose() }
    }

    if ($PSVersionTable.PSVersion -lt [version]'5.1' -or [Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT -or
        -not [Environment]::Is64BitOperatingSystem) { throw 'IAM.Agent requires Windows x64 and PowerShell 5.1 or later.' }
    $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Open Windows PowerShell using Run as administrator, then paste the install command again.'
    }

    $download = [uri]'__SETUP_URL__'
    $expectedHash = '__SETUP_SHA256__'
    if ($download.Scheme -ne 'https' -or $download.UserInfo -or $download.Query -or $download.Fragment -or
        $expectedHash -cnotmatch '^[A-F0-9]{64}$') { throw 'Invalid IAM installer configuration.' }
    $work = Join-Path ([IO.Path]::GetTempPath()) ('IAM.Agent.Setup-' + [guid]::NewGuid().ToString('N'))
    if (Test-Path -LiteralPath $work) { throw 'Temporary installation directory already exists.' }
    [void][IO.Directory]::CreateDirectory($work)
    # Only administrators and SYSTEM may change the downloaded installer before execution.
    $acl = [Security.AccessControl.DirectorySecurity]::new()
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($sid in @('S-1-5-18', 'S-1-5-32-544')) {
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
            [Security.Principal.SecurityIdentifier]::new($sid), 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    }
    Set-Acl -LiteralPath $work -AclObject $acl
    Write-Host 'Downloading IAM.Agent setup. Existing installations will be checked first; new enrollment requires IAM administrator sign-in.'
    Add-Type -AssemblyName System.Net.Http
    $handler = [Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromMinutes(5)
    $zip = Join-Path $work 'setup.zip'
    try {
        $response = $client.GetAsync($download, [Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        try {
            if ($response.StatusCode -ne [Net.HttpStatusCode]::OK) { throw 'IAM setup download failed. No redirects are allowed.' }
            if ($response.Content.Headers.ContentLength -gt 512MB) { throw 'IAM setup download is too large.' }
            $inputStream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
            $outputStream = [IO.File]::Open($zip, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
            try {
                $buffer = New-Object byte[] 65536
                $received = 0L
                $deadline = [DateTime]::UtcNow.AddMinutes(5)
                while ($true) {
                    $read = $inputStream.ReadAsync($buffer, 0, $buffer.Length)
                    if (-not $read.Wait(30000)) { throw 'IAM setup download timed out.' }
                    $count = $read.GetAwaiter().GetResult()
                    if ($count -eq 0) { break }
                    $received += $count
                    if ($received -gt 512MB -or [DateTime]::UtcNow -gt $deadline) { throw 'IAM setup download limit exceeded.' }
                    $outputStream.Write($buffer, 0, $count)
                }
            } finally { $outputStream.Dispose(); $inputStream.Dispose() }
        } finally { $response.Dispose() }
    } finally { $client.Dispose() }
    $files = Join-Path $work 'files'
    Expand-IamAgentSetup $zip $files $expectedHash
    $installer = Join-Path $files 'Install-IAMAgent.ps1'
    $windowsPowerShell = Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
    if (-not [Environment]::Is64BitProcess) {
        $windowsPowerShell = Join-Path $env:SystemRoot 'Sysnative/WindowsPowerShell/v1.0/powershell.exe'
    }
    Write-Host 'Setup verified. Starting installation; Windows script policy and IAM approval remain required.'
    & $windowsPowerShell -NoProfile -STA -File $installer
    if ($LASTEXITCODE -ne 0) { throw "IAM.Agent installation failed. See the installer error above. Setup files are retained at $work. Windows script policy remains enforced." }
    Get-Service -Name 'IAM.Agent'
    Write-Host 'Refresh Machines & agents in IAM to check enrollment and inventory.'
}
