#requires -Version 5.1
# Native Windows UI only. No network, enrollment, service or credential persistence.
function New-IamAgentAuthWindow {
    [CmdletBinding()]
    param([Parameter(Mandatory)][uri]$Server, [switch]$Mfa, [string]$EmployeeCode = '')
    if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
        throw 'Open the installer using Windows PowerShell -STA.'
    }
    Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
    [xml]$markup = @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="IAM Agent setup" Width="440" SizeToContent="Height" ResizeMode="NoResize"
        WindowStartupLocation="CenterScreen" Background="#F4F5F8" FontFamily="Segoe UI"
        FontSize="13" Foreground="#182633" UseLayoutRounding="True">
  <Window.Resources>
    <Style TargetType="Button">
      <Setter Property="MinHeight" Value="36"/><Setter Property="Padding" Value="14,7"/>
      <Setter Property="FontWeight" Value="SemiBold"/><Setter Property="Cursor" Value="Hand"/>
      <Setter Property="Background" Value="White"/><Setter Property="Foreground" Value="#253544"/>
      <Setter Property="BorderBrush" Value="#CCD3DE"/>
      <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
        <Border x:Name="Chrome" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                BorderThickness="1" CornerRadius="7" Padding="{TemplateBinding Padding}">
          <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Chrome" Property="Opacity" Value="0.85"/></Trigger>
          <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Chrome" Property="BorderBrush" Value="#2563EB"/><Setter TargetName="Chrome" Property="BorderThickness" Value="2"/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style TargetType="TextBox">
      <Setter Property="Padding" Value="10,7"/><Setter Property="MinHeight" Value="36"/>
      <Setter Property="FontSize" Value="14"/><Setter Property="BorderBrush" Value="#B8C3D2"/>
      <Setter Property="Background" Value="White"/><Setter Property="VerticalContentAlignment" Value="Center"/>
    </Style>
    <Style TargetType="PasswordBox">
      <Setter Property="Padding" Value="10,7"/><Setter Property="MinHeight" Value="36"/>
      <Setter Property="FontSize" Value="14"/><Setter Property="BorderBrush" Value="#B8C3D2"/>
      <Setter Property="Background" Value="White"/><Setter Property="VerticalContentAlignment" Value="Center"/>
    </Style>
  </Window.Resources>
  <ScrollViewer Background="#F4F5F8" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">
    <StackPanel>
      <Border Background="White" BorderBrush="#DCE1E9" BorderThickness="0,0,0,1" Padding="20,12">
        <Grid><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
          <StackPanel><Image x:Name="BrandLogo" Width="112" Height="28" Stretch="Uniform" HorizontalAlignment="Left" AutomationProperties.Name="Fujitec" RenderOptions.BitmapScalingMode="HighQuality"/>
            <TextBlock Text="Identity Administration" Foreground="#536478" FontSize="11" Margin="0,3,0,0"/>
          </StackPanel>
          <Border Grid.Column="1" Background="#F1F4F8" CornerRadius="6" Padding="9,5" VerticalAlignment="Center">
            <TextBlock Text="AGENT SETUP" FontSize="10" FontWeight="SemiBold" Foreground="#536478"/>
          </Border>
        </Grid>
      </Border>
      <StackPanel Margin="20,16,20,16">
        <TextBlock x:Name="Heading" Text="Approve this computer" FontSize="21" FontWeight="SemiBold"/>
        <TextBlock x:Name="Description" Text="Sign in as an IAM administrator to enroll this computer."
                   Foreground="#536478" Margin="0,5,0,12" TextWrapping="Wrap"/>
        <Border Background="#EAF0F7" CornerRadius="7" Padding="10,8" Margin="0,0,0,12">
          <StackPanel>
            <TextBlock Text="IAM SERVER" Foreground="#536478" FontSize="10" FontWeight="Bold"/>
            <TextBlock x:Name="ServerName" Margin="0,3,0,0" FontWeight="SemiBold" FontSize="12" TextWrapping="Wrap"/>
            <TextBlock x:Name="MachineName" Foreground="#536478" FontSize="11" Margin="0,3,0,0" TextWrapping="Wrap"/>
          </StackPanel>
        </Border>
        <StackPanel x:Name="CredentialFields">
          <Label Target="{Binding ElementName=Employee}" Content="_Employee code" Padding="0,0,0,5" FontWeight="SemiBold"/>
          <TextBox x:Name="Employee" MaxLength="128" AutomationProperties.Name="Employee code"/>
          <Label Target="{Binding ElementName=Password}" Content="_Password" Padding="0,12,0,5" FontWeight="SemiBold"/>
          <PasswordBox x:Name="Password" MaxLength="1024" AutomationProperties.Name="Password"/>
          <TextBlock Text="Use your IAM password, not your terminal PIN." Foreground="#536478" FontSize="11" Margin="0,5,0,0" TextWrapping="Wrap"/>
        </StackPanel>
        <StackPanel x:Name="MfaFields" Visibility="Collapsed">
          <Label Target="{Binding ElementName=Code}" Content="_Authenticator code" Padding="0,0,0,5" FontWeight="SemiBold"/>
          <PasswordBox x:Name="Code" MaxLength="6" AutomationProperties.Name="Authenticator code"/>
          <TextBlock Text="Enter the six-digit code from your authenticator app." Foreground="#536478" FontSize="11" Margin="0,5,0,0" TextWrapping="Wrap"/>
        </StackPanel>
        <Border x:Name="ErrorPanel" Background="#FFF0F1" CornerRadius="6" Padding="10" Margin="0,10,0,0" Visibility="Collapsed">
          <TextBlock x:Name="ErrorText" Foreground="#AC1028" TextWrapping="Wrap" AutomationProperties.LiveSetting="Assertive"/>
        </Border>
        <Grid Margin="0,16,0,12"><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="10"/><ColumnDefinition/></Grid.ColumnDefinitions>
          <Button x:Name="Cancel" Content="Cancel" IsCancel="True"/>
          <Button x:Name="Continue" Grid.Column="2" Content="Sign in and continue" Background="#D5092A" BorderBrush="#D5092A" Foreground="White" IsDefault="True"/>
        </Grid>
        <TextBlock Text="No terminal number needed. IAM approval is required."
                   FontSize="11" Foreground="#536478" TextWrapping="Wrap"/>
      </StackPanel>
    </StackPanel>
  </ScrollViewer>
</Window>
'@
    $reader = [Xml.XmlNodeReader]::new($markup)
    try { $window = [Windows.Markup.XamlReader]::Load($reader) } finally { $reader.Dispose() }
    $window.MaxHeight = [Windows.SystemParameters]::WorkArea.Height - 32
    $window.Width = [Math]::Min(440, [Windows.SystemParameters]::WorkArea.Width - 32)
    $view = @{ Window = $window; Result = $null; IsMfa = [bool]$Mfa }
    foreach ($name in @('BrandLogo','Heading','Description','ServerName','MachineName','CredentialFields','MfaFields',
        'Employee','Password','Code','ErrorPanel','ErrorText','Continue','Cancel')) {
        $view[$name] = $window.FindName($name)
    }
    # Use the same original image assets as IAM Web, not a font imitation.
    # Published setup has adjacent assets; source-tree previews use the originals.
    $assets = $PSScriptRoot
    if (-not (Test-Path -LiteralPath (Join-Path $assets 'fujitec-logo.png'))) {
        $assets = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../src/Frontend/public'))
    }
    foreach ($name in @('fujitec-logo.png', 'favicon.ico')) {
        $bitmap = [Windows.Media.Imaging.BitmapImage]::new()
        $bitmap.BeginInit()
        $bitmap.CacheOption = [Windows.Media.Imaging.BitmapCacheOption]::OnLoad
        $bitmap.UriSource = [uri]::new((Join-Path $assets $name), [UriKind]::Absolute)
        $bitmap.EndInit()
        $bitmap.Freeze()
        if ($name -eq 'fujitec-logo.png') { $view.BrandLogo.Source = $bitmap }
        else { $window.Icon = $bitmap }
    }
    $view.ServerName.Text = $Server.GetLeftPart([UriPartial]::Authority)
    $view.MachineName.Text = 'This computer: ' + [Environment]::MachineName
    $view.Employee.Text = $EmployeeCode
    if ($Mfa) {
        $view.Heading.Text = 'Verify your sign-in'
        $view.Description.Text = 'Your account requires an additional verification step.'
        $view.CredentialFields.Visibility = 'Collapsed'
        $view.MfaFields.Visibility = 'Visible'
        $view.Continue.Content = 'Verify and continue'
    }
    $view.Continue.Add_Click({
        if ($view.IsMfa) {
            if ($view.Code.Password -cnotmatch '^[0-9]{6}$') {
                $view.ErrorText.Text = 'Enter the six-digit authenticator code.'
                $view.ErrorPanel.Visibility = 'Visible'
                [void]$view.Code.Focus()
                return
            }
            $view.Result = $view.Code.SecurePassword.Copy()
        } else {
            if ([string]::IsNullOrWhiteSpace($view.Employee.Text) -or $view.Password.SecurePassword.Length -eq 0) {
                $view.ErrorText.Text = 'Enter your employee code and IAM password.'
                $view.ErrorPanel.Visibility = 'Visible'
                if ([string]::IsNullOrWhiteSpace($view.Employee.Text)) { [void]$view.Employee.Focus() }
                else { [void]$view.Password.Focus() }
                return
            }
            $view.Result = [PSCredential]::new($view.Employee.Text.Trim(), $view.Password.SecurePassword.Copy())
        }
        $view.Window.Close()
    }.GetNewClosure())
    $view.Cancel.Add_Click({ $view.Window.Close() }.GetNewClosure())
    $window.Add_Closed({ $view.Password.Clear(); $view.Code.Clear() }.GetNewClosure())
    $window.Add_ContentRendered({
        if ($view.IsMfa) { [void]$view.Code.Focus() }
        elseif ($view.Employee.Text) { [void]$view.Password.Focus() }
        else { [void]$view.Employee.Focus() }
    }.GetNewClosure())
    return $view
}

function Show-IamAgentCredentialDialog([uri]$Server) {
    $view = New-IamAgentAuthWindow -Server $Server
    [void]$view.Window.ShowDialog()
    return $view.Result
}

function Show-IamAgentMfaDialog([uri]$Server) {
    $view = New-IamAgentAuthWindow -Server $Server -Mfa
    [void]$view.Window.ShowDialog()
    return $view.Result
}
