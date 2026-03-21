[CmdletBinding()]
param(
    [string]$DeploymentRoot = 'C:\Deployment',
    [string]$PublishedSitePath = '',
    [string]$SettingsFile = '',
    [string]$SmtpHost = 'localhost',
    [int]$SmtpPort = 25,
    [bool]$UseStartTls = $false,
    [bool]$UseSsl = $false,
    [string]$Username = '',
    [string]$Password = '',
    [string]$FromEmail = '',
    [string]$FromName = 'TheCertMaster',
    [string]$TestRecipientEmail = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Test-TcpPort {
    param(
        [string]$HostName,
        [int]$Port
    )

    $client = $null
    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $async = $client.BeginConnect($HostName, $Port, $null, $null)
        if (-not $async.AsyncWaitHandle.WaitOne(5000, $false)) {
            return $false
        }

        $client.EndConnect($async)
        return $true
    }
    catch {
        return $false
    }
    finally {
        if ($client) {
            $client.Dispose()
        }
    }
}

function Resolve-DefaultValuesFromSettings {
    param([string]$SettingsPath)

    if ([string]::IsNullOrWhiteSpace($SettingsPath) -or -not (Test-Path -LiteralPath $SettingsPath)) {
        return $null
    }

    try {
        return Import-PowerShellDataFile -Path $SettingsPath
    }
    catch {
        throw "Unable to parse settings file '$SettingsPath'. $($_.Exception.Message)"
    }
}

function Ensure-ServiceIsRunning {
    param([string]$Name)

    $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if (-not $service) {
        return
    }

    $serviceConfig = Get-CimInstance -ClassName Win32_Service -Filter "Name='$Name'" -ErrorAction SilentlyContinue
    if ($serviceConfig -and $serviceConfig.StartMode -ne 'Auto') {
        Set-Service -Name $Name -StartupType Automatic
    }

    if ($service.Status -ne 'Running') {
        Start-Service -Name $Name
    }
}

function Ensure-SmtpFeatureInstalled {
    if (-not (Get-Command Get-WindowsFeature -ErrorAction SilentlyContinue)) {
        throw "Get-WindowsFeature is not available on this server. Install the Windows SMTP Server feature manually, then rerun this script."
    }

    $requiredFeatures = @(
        'SMTP-Server',
        'RSAT-SMTP',
        'Web-Metabase',
        'Web-Lgcy-Mgmt-Console',
        'Web-WMI'
    )

    $featuresToInstall = @()
    foreach ($featureName in $requiredFeatures) {
        $feature = Get-WindowsFeature -Name $featureName -ErrorAction SilentlyContinue
        if ($feature -and -not $feature.Installed) {
            $featuresToInstall += $featureName
        }
    }

    if ($featuresToInstall.Count -eq 0) {
        return
    }

    Write-Step "Installing Windows SMTP feature and dependencies"
    $installResult = Install-WindowsFeature -Name $featuresToInstall -IncludeManagementTools
    if (-not $installResult.Success) {
        throw "Failed to install required Windows features for SMTP: $($featuresToInstall -join ', ')"
    }
}

if ([string]::IsNullOrWhiteSpace($PublishedSitePath)) {
    $PublishedSitePath = Join-Path $DeploymentRoot 'publish'
}

if ([string]::IsNullOrWhiteSpace($SettingsFile)) {
    $SettingsFile = Join-Path $DeploymentRoot 'scripts\production-settings.psd1'
}

$settings = Resolve-DefaultValuesFromSettings -SettingsPath $SettingsFile

if ($settings) {
    if ([string]::IsNullOrWhiteSpace($FromEmail)) {
        $resolvedHost = ''

        if ($settings.ContainsKey('PublicBaseUrl') -and -not [string]::IsNullOrWhiteSpace($settings.PublicBaseUrl)) {
            try {
                $resolvedHost = ([Uri]$settings.PublicBaseUrl).Host
            }
            catch {
                $resolvedHost = ''
            }
        }

        if (-not [string]::IsNullOrWhiteSpace($resolvedHost)) {
            $FromEmail = "no-reply@$resolvedHost"
        }
        elseif ($settings.ContainsKey('BootstrapAdminEmail') -and -not [string]::IsNullOrWhiteSpace($settings.BootstrapAdminEmail)) {
            $FromEmail = $settings.BootstrapAdminEmail
        }
    }

    if ([string]::IsNullOrWhiteSpace($FromName) -and $settings.ContainsKey('BootstrapAdminFirstName')) {
        $FromName = "$($settings.BootstrapAdminFirstName) TheCertMaster".Trim()
    }
}

if ([string]::IsNullOrWhiteSpace($FromEmail)) {
    $FromEmail = 'no-reply@localhost'
}

if (-not (Test-Path -LiteralPath $PublishedSitePath)) {
    throw "Published site path '$PublishedSitePath' does not exist."
}

$appDataPath = Join-Path $PublishedSitePath 'App_Data'
$smtpSettingsPath = Join-Path $appDataPath 'smtp_settings.json'

Write-Step "Checking SMTP service installation"
$smtpService = Get-Service -Name 'SMTPSVC' -ErrorAction SilentlyContinue
if (-not $smtpService) {
    Ensure-SmtpFeatureInstalled
    $smtpService = Get-Service -Name 'SMTPSVC' -ErrorAction SilentlyContinue
    if (-not $smtpService) {
        throw "The SMTP service 'SMTPSVC' was not found after Windows SMTP feature installation."
    }
}

Write-Step "Starting SMTP services"
Ensure-ServiceIsRunning -Name 'IISADMIN'
Ensure-ServiceIsRunning -Name 'SMTPSVC'

Write-Step "Writing TheCertMaster SMTP settings for application testing"
New-Item -ItemType Directory -Path $appDataPath -Force | Out-Null

$payload = [ordered]@{
    Host = $SmtpHost
    Port = $SmtpPort
    UseStartTls = $UseStartTls
    UseSsl = $UseSsl
    Username = $Username
    Password = $Password
    ProtectedPassword = ''
    FromEmail = $FromEmail
    FromName = $FromName
}

$payload | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $smtpSettingsPath -Encoding UTF8

Write-Step "Testing SMTP endpoint reachability"
$portReachable = Test-TcpPort -HostName $SmtpHost -Port $SmtpPort
if (-not $portReachable) {
    Write-Warning "Could not connect to $SmtpHost`:$SmtpPort. The application settings were written, but the SMTP endpoint did not accept a local TCP connection."
}

if (-not [string]::IsNullOrWhiteSpace($TestRecipientEmail)) {
    Write-Step "Sending SMTP test message"
    $mailMessage = New-Object System.Net.Mail.MailMessage
    $mailMessage.From = New-Object System.Net.Mail.MailAddress($FromEmail, $FromName)
    [void]$mailMessage.To.Add($TestRecipientEmail)
    $mailMessage.Subject = 'TheCertMaster SMTP Test'
    $mailMessage.Body = 'This is a test email from the TheCertMaster deployment SMTP test script.'

    $smtpClient = New-Object System.Net.Mail.SmtpClient($SmtpHost, $SmtpPort)
    $smtpClient.EnableSsl = $UseSsl
    $smtpClient.DeliveryMethod = [System.Net.Mail.SmtpDeliveryMethod]::Network
    $smtpClient.UseDefaultCredentials = $false

    if (-not [string]::IsNullOrWhiteSpace($Username)) {
        $smtpClient.Credentials = New-Object System.Net.NetworkCredential($Username, $Password)
    }

    try {
        $smtpClient.Send($mailMessage)
        Write-Host "SMTP test email sent to $TestRecipientEmail" -ForegroundColor Green
    }
    catch {
        $baseMessage = $_.Exception.Message
        $innerMessage = $_.Exception.InnerException.Message

        Write-Error "SMTP test send failed. $baseMessage"

        if (-not [string]::IsNullOrWhiteSpace($innerMessage)) {
            Write-Warning "Inner SMTP error: $innerMessage"
        }

        Write-Host ""
        Write-Host "Common causes:" -ForegroundColor Yellow
        Write-Host "- The Windows SMTP virtual server is installed but not configured to relay outbound mail."
        Write-Host "- The server cannot resolve external DNS or reach remote mail hosts on port 25."
        Write-Host "- Your network or ISP blocks outbound SMTP on port 25."
        Write-Host "- A smart host or authenticated relay is required for external delivery."
        Write-Host ""
        Write-Host "Next checks:" -ForegroundColor Yellow
        Write-Host "- Verify DNS resolution from the server."
        Write-Host "- Verify outbound TCP 25 is allowed."
        Write-Host "- If you want external delivery, configure a smart host or authenticated SMTP relay."
        Write-Host "- If you only want local app testing, keep the app pointed at localhost and test against a local mailbox or pickup flow."
        throw
    }
    finally {
        $mailMessage.Dispose()
        $smtpClient.Dispose()
    }
}

Write-Host ""
Write-Host "SMTP test configuration is complete." -ForegroundColor Green
Write-Host "Published site path: $PublishedSitePath"
Write-Host "SMTP settings file: $smtpSettingsPath"
Write-Host "SMTP host: $SmtpHost"
Write-Host "SMTP port: $SmtpPort"
Write-Host "From email: $FromEmail"
