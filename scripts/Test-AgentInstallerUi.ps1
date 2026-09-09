#requires -Version 5.1
[CmdletBinding()]
param([string]$ScreenshotDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'AgentInstallerUi.ps1')
function Assert([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Click($Button) {
    $Button.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Primitives.ButtonBase]::ClickEvent))
}
function Capture($View, [string]$Name) {
    if (-not $ScreenshotDirectory) { return }
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($ScreenshotDirectory))
    # Offscreen layout/render only: never show a credential window or contact IAM.
    $content = $View.Window.Content
    $width = $View.Window.Width
    $content.Measure([Windows.Size]::new($width, 900))
    $height = [Math]::Ceiling($content.DesiredSize.Height)
    $content.Arrange([Windows.Rect]::new(0, 0, $width, $height))
    $content.UpdateLayout()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new([int]$width, [int]$height, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($content)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create((Join-Path $ScreenshotDirectory $Name))
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}
$server = [uri]'https://iam.example.test/identity/'
$view = New-IamAgentAuthWindow -Server $server
Assert ($view.ServerName.Text -eq 'https://iam.example.test') 'IAM destination was not displayed.'
Assert ($view.Continue.IsDefault -and $view.Cancel.IsCancel) 'Enter/Escape navigation is missing.'
Assert ($null -ne $view.BrandLogo.Source -and $null -ne $view.Window.Icon) 'Fujitec branding failed to load.'
Capture $view 'modern-iam-sign-in.png'
Click $view.Continue
Assert ($null -eq $view.Result -and $view.ErrorPanel.Visibility -eq 'Visible') 'Empty credentials accepted.'
$view.Employee.Text = '  SYNTHETIC-ADMIN  '
$view.Password.Password = 'synthetic-fixture-password'
Click $view.Continue
Assert ($view.Result -is [PSCredential] -and $view.Result.UserName -eq 'SYNTHETIC-ADMIN') 'Credential conversion failed.'
Assert ($view.Result.GetNetworkCredential().Password -ceq 'synthetic-fixture-password') 'Password was changed.'
Assert ($view.Password.SecurePassword.Length -eq 0) 'Password control was not cleared after acceptance.'
$view.Result.Password.Dispose(); $view.Result = $null
Write-Output 'PASS: branded credential dialog, validation, keyboard actions and secret clearing.'
$view = New-IamAgentAuthWindow -Server $server
$view.Password.Password = 'synthetic-cancel'
Click $view.Cancel
Assert ($null -eq $view.Result -and $view.Password.SecurePassword.Length -eq 0) 'Cancel returned credentials or retained the field.'
Write-Output 'PASS: cancel does not approve or retain credentials.'
$view = New-IamAgentAuthWindow -Server $server -Mfa
Assert ($view.CredentialFields.Visibility -eq 'Collapsed' -and $view.MfaFields.Visibility -eq 'Visible') 'MFA fields incorrect.'
Capture $view 'modern-iam-mfa.png'
$view.Code.Password = '12AB56'
Click $view.Continue
Assert ($null -eq $view.Result -and $view.ErrorPanel.Visibility -eq 'Visible') 'Invalid MFA code accepted.'
$view.Code.Password = '012345'
Click $view.Continue
Assert ($view.Result -is [Security.SecureString]) 'MFA code not retained as SecureString.'
Assert (([PSCredential]::new('test',$view.Result)).GetNetworkCredential().Password -ceq '012345') 'MFA leading zero lost.'
Assert ($view.Code.SecurePassword.Length -eq 0) 'MFA control was not cleared.'
$view.Result.Dispose(); $view.Result = $null
Write-Output 'PASS: MFA validation, leading zero and secret clearing.'
$view = New-IamAgentAuthWindow -Server $server -Mfa
$view.Code.Password = '012345'
$view.Window.Close()
Assert ($null -eq $view.Result -and $view.Code.SecurePassword.Length -eq 0) 'Window close retained MFA.'
Write-Output 'PASS: closing MFA does not approve enrollment. No network, service or database changes.'
