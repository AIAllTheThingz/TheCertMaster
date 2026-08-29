#requires -Version 5.1
#requires -RunAsAdministrator

[CmdletBinding()]
param(
    [string]$SettingsFile = '',
    [string]$DeploymentRoot = 'C:\Deployment',
    [string]$SiteName = 'Default Web Site',
    [string]$AppPoolName = 'TheCertMaster',
    [int]$HttpPort = 80,
    [int]$HttpsPort = 443,
    [string]$HostName = '',
    [string]$CertificateThumbprint = '',
    [switch]$CreateSelfSignedCertificate,
    [string]$SqlInstance = '.\SQLEXPRESS',
    [string]$DatabaseName = 'TheCertMaster',
    [string]$ConnectionString = '',
    [string]$PublicBaseUrl = '',
    [string]$JwtKey = '',
    [string]$JwtIssuer = 'TheCertMaster',
    [string]$JwtAudience = 'TheCertMasterUsers',
    [string]$BootstrapAdminEmail = '',
    [string]$BootstrapAdminPassword = '',
    [string]$BootstrapAdminFirstName = '',
    [string]$BootstrapAdminLastName = '',
    [string[]]$CorsOrigins = @(),
    [int]$AuthAttemptsPerMinute = 8,
    [int]$GuestQuizLoadsPerMinute = 20,
    [int]$AuthenticatedQuizLoadsPerMinute = 60,
    [switch]$EnableSwagger,
    [switch]$EnableHttpsRedirection
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

if (-not [string]::IsNullOrWhiteSpace($SettingsFile)) {
    if (-not (Test-Path -LiteralPath $SettingsFile)) {
        throw "Settings file was not found: $SettingsFile"
    }

    $resolvedSettingsFile = (Resolve-Path -LiteralPath $SettingsFile).Path
    $settings = Import-PowerShellDataFile -Path $resolvedSettingsFile

    foreach ($entry in $settings.GetEnumerator()) {
        if ($PSBoundParameters.ContainsKey($entry.Key)) {
            continue
        }

        Set-Variable -Name $entry.Key -Value $entry.Value -Scope Script
    }
}

function Write-Step {
    param([string]$Message)
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Ensure-Directory {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Get-ReleaseArtifact {
    param([string]$DeploymentRootPath)

    if (-not (Test-Path -LiteralPath $DeploymentRootPath -PathType Container)) {
        throw "Deployment root was not found: $DeploymentRootPath"
    }

    $artifactRoot = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $DeploymentRootPath).Path).TrimEnd('\', '/')
    $scriptArtifactRoot = [System.IO.Path]::GetFullPath((Split-Path -Path $PSScriptRoot -Parent)).TrimEnd('\', '/')
    if (-not [string]::Equals($artifactRoot, $scriptArtifactRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Run this installer from the scripts directory in the extracted release artifact.'
    }

    $manifestPath = Join-Path $artifactRoot 'artifact-manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Release artifact manifest was not found: $manifestPath"
    }

    try {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    }
    catch {
        throw "Release artifact manifest is invalid: $manifestPath"
    }

    $commit = [string]$manifest.commit
    if ($commit -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'Release artifact manifest does not contain a valid commit SHA.'
    }

    $manifestProperties = @($manifest.files.PSObject.Properties)
    if ($manifestProperties.Count -eq 0) {
        throw 'Release artifact manifest does not contain file hashes.'
    }

    $artifactRootWithSeparator = $artifactRoot + [System.IO.Path]::DirectorySeparatorChar
    $validatedPaths = @()
    foreach ($entry in $manifestProperties) {
        $relativePath = [string]$entry.Name
        $expectedHash = [string]$entry.Value
        if ([System.IO.Path]::IsPathRooted($relativePath) -or $expectedHash -notmatch '^[0-9a-fA-F]{64}$') {
            throw "Release artifact manifest has an invalid entry: $relativePath"
        }

        $filePath = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot $relativePath))
        if (-not $filePath.StartsWith($artifactRootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
            throw "Release artifact is incomplete: $relativePath"
        }

        $file = Get-Item -LiteralPath $filePath -Force
        if (($file.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Release artifact contains a reparse-point file: $relativePath"
        }

        $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash
        if (-not [string]::Equals($actualHash, $expectedHash, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Release artifact integrity check failed for $relativePath."
        }

        $validatedPaths += $relativePath.Replace('/', '\')
    }

    foreach ($requiredPath in @('publish\TheCertMaster.dll', 'publish\web.config', 'bootstrapper\TheCertMaster.Bootstrapper.dll', 'migrations\efbundle.exe', 'source\TheCertMaster.csproj', 'source\commit-manifest.json')) {
        if ($validatedPaths -notcontains $requiredPath) {
            throw "Release artifact manifest is missing required file: $requiredPath"
        }
    }

    foreach ($payloadDirectory in @('publish', 'bootstrapper', 'migrations')) {
        $payloadPath = Join-Path $artifactRoot $payloadDirectory
        if (-not (Test-Path -LiteralPath $payloadPath -PathType Container)) {
            throw "Release artifact payload directory is missing: $payloadDirectory"
        }

        Get-ChildItem -LiteralPath $payloadPath -Force -Recurse | ForEach-Object {
            if (($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Release artifact contains a reparse-point payload: $($_.FullName)"
            }

            if (-not $_.PSIsContainer) {
                $payloadRelativePath = $_.FullName.Substring($artifactRootWithSeparator.Length).Replace('/', '\')
                if ($validatedPaths -notcontains $payloadRelativePath) {
                    throw "Release artifact contains an unexpected payload file: $payloadRelativePath"
                }
            }
        }
    }

    try {
        $sourceManifest = Get-Content -LiteralPath (Join-Path $artifactRoot 'source\commit-manifest.json') -Raw | ConvertFrom-Json
    }
    catch {
        throw 'Source commit manifest is invalid.'
    }

    if (-not [string]::Equals([string]$sourceManifest.commit, $commit, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Source commit manifest does not match the release artifact manifest.'
    }

    return [pscustomobject]@{
        Root = $artifactRoot
        Commit = $commit.ToLowerInvariant()
        PublishPath = Join-Path $artifactRoot 'publish'
        BootstrapperPath = Join-Path $artifactRoot 'bootstrapper\TheCertMaster.Bootstrapper.dll'
        MigrationBundlePath = Join-Path $artifactRoot 'migrations\efbundle.exe'
    }
}

function Ensure-DotNetInstalled {
    $dotnetExe = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (-not (Test-Path -LiteralPath $dotnetExe)) {
        throw '.NET was not found. Run ensure-server-prerequisites.ps1 first.'
    }

    $runtimes = & $dotnetExe --list-runtimes
    if ($LASTEXITCODE -ne 0 -or -not ($runtimes | Where-Object { $_ -match '^Microsoft\.AspNetCore\.App 9\.' })) {
        throw '.NET 9 ASP.NET Core runtime was not found. Run ensure-server-prerequisites.ps1 first.'
    }

    return $dotnetExe
}

function Initialize-DotNetEnvironment {
    param([string]$DotNetExe)

    $dotnetRoot = Split-Path -Path $DotNetExe -Parent
    $pathEntries = @($env:Path -split ';') | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    if ($pathEntries -notcontains $dotnetRoot) {
        $env:Path = $dotnetRoot + ';' + ($pathEntries -join ';')
    }

    $env:DOTNET_ROOT = $dotnetRoot
}

function Invoke-BootstrapAdminTool {
    param(
        [string]$DotNetExe,
        [string]$BootstrapperPath,
        [string]$ConnectionStringToUse,
        [string]$Email,
        [string]$Password,
        [string]$FirstName,
        [string]$LastName
    )

    if ([string]::IsNullOrWhiteSpace($Email)) {
        return
    }

    & $DotNetExe $BootstrapperPath `
        --connection $ConnectionStringToUse `
        --email $Email `
        --password $Password `
        --first-name $FirstName `
        --last-name $LastName

    if ($LASTEXITCODE -ne 0) {
        throw 'Bootstrap admin creation failed.'
    }
}

function Ensure-IisModule {
    Import-Module WebAdministration

    $appCmdExe = Join-Path $env:SystemRoot 'System32\inetsrv\appcmd.exe'
    if (-not (Test-Path -LiteralPath $appCmdExe)) {
        throw 'IIS appcmd.exe is not available. Verify IIS was installed correctly.'
    }

    return $appCmdExe
}

function Ensure-AppPool {
    param(
        [string]$AppCmdExe,
        [string]$PoolName
    )

    Import-Module WebAdministration

    if (-not (Test-Path -LiteralPath ("IIS:\AppPools\" + $PoolName))) {
        New-WebAppPool -Name $PoolName | Out-Null
    }

    & $AppCmdExe set apppool "/apppool.name:$PoolName" "/managedRuntimeVersion:" "/managedPipelineMode:Integrated" "/processModel.identityType:ApplicationPoolIdentity" "/processModel.loadUserProfile:true" "/autoStart:true" "/startMode:AlwaysRunning" | Out-Host
    & $AppCmdExe set config -section:system.applicationHost/applicationPools "/[name='$PoolName'].processModel.idleTimeout:00:00:00" /commit:apphost | Out-Host
}

function Ensure-SiteConfiguration {
    param(
        [string]$SiteNameToUse,
        [string]$PublishPath,
        [string]$PoolName,
        [int]$HttpBindingPort
    )

    Import-Module WebAdministration

    if (Test-Path -LiteralPath ("IIS:\Sites\" + $SiteNameToUse)) {
        Set-ItemProperty ("IIS:\Sites\" + $SiteNameToUse) -Name physicalPath -Value $PublishPath
        Set-ItemProperty ("IIS:\Sites\" + $SiteNameToUse) -Name applicationPool -Value $PoolName
        return $false
    }

    New-Website -Name $SiteNameToUse -Port $HttpBindingPort -PhysicalPath $PublishPath -ApplicationPool $PoolName | Out-Null
    return $true
}

function Get-TargetHostName {
    param(
        [string]$ConfiguredHostName,
        [string]$BaseUrl
    )

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredHostName)) {
        return $ConfiguredHostName.Trim()
    }

    $uri = [Uri]$BaseUrl
    return $uri.Host
}

function Ensure-HttpsCertificate {
    param(
        [string]$RequestedThumbprint,
        [string]$DnsName,
        [switch]$AllowCreateSelfSigned
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedThumbprint)) {
        $normalizedThumbprint = $RequestedThumbprint.Replace(' ', '').Trim()
        $existing = Get-Item -Path ("Cert:\LocalMachine\My\" + $normalizedThumbprint) -ErrorAction SilentlyContinue
        if (-not $existing) {
            throw "Certificate thumbprint '$RequestedThumbprint' was not found in Cert:\LocalMachine\My."
        }

        return $normalizedThumbprint
    }

    if (-not $AllowCreateSelfSigned) {
        throw 'No HTTPS certificate thumbprint was supplied and self-signed certificate creation is disabled.'
    }

    $existingByDns = Get-ChildItem Cert:\LocalMachine\My |
        Where-Object { $_.Subject -eq "CN=$DnsName" } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

    if ($existingByDns) {
        return $existingByDns.Thumbprint
    }

    $cert = New-SelfSignedCertificate `
        -DnsName $DnsName `
        -CertStoreLocation 'Cert:\LocalMachine\My' `
        -FriendlyName "TheCertMaster HTTPS ($DnsName)" `
        -NotAfter (Get-Date).AddYears(2)

    return $cert.Thumbprint
}

function Ensure-SiteBindings {
    param(
        [string]$SiteNameToUse,
        [int]$HttpBindingPort,
        [int]$HttpsBindingPort,
        [string]$DnsName,
        [string]$HttpsCertificateThumbprint
    )

    Import-Module WebAdministration

    $httpBindingInfo = "*:${HttpBindingPort}:"
    $existingHttpBinding = Get-WebBinding -Name $SiteNameToUse -Protocol 'http' -ErrorAction SilentlyContinue |
        Where-Object { $_.bindingInformation -eq $httpBindingInfo }

    if (-not $existingHttpBinding) {
        New-WebBinding -Name $SiteNameToUse -Protocol 'http' -Port $HttpBindingPort -IPAddress '*' | Out-Null
    }

    $httpsBindingInfo = "*:${HttpsBindingPort}:"
    $existingHttpsBinding = Get-WebBinding -Name $SiteNameToUse -Protocol 'https' -ErrorAction SilentlyContinue |
        Where-Object { $_.bindingInformation -eq $httpsBindingInfo }

    if (-not $existingHttpsBinding) {
        New-WebBinding -Name $SiteNameToUse -Protocol 'https' -Port $HttpsBindingPort -IPAddress '*' | Out-Null
    }

    $sslBindingPath = "IIS:\SslBindings\0.0.0.0!$HttpsBindingPort"
    if (Test-Path -LiteralPath $sslBindingPath) {
        Remove-Item -LiteralPath $sslBindingPath -Force
    }

    Get-Item -LiteralPath ("Cert:\LocalMachine\My\" + $HttpsCertificateThumbprint) | New-Item -Path $sslBindingPath -Force | Out-Null
}

function Stop-SiteAndPoolIfPresent {
    param(
        [string]$SiteNameToUse,
        [string]$PoolName,
        [string]$PreviousPoolName = ''
    )

    Import-Module WebAdministration

    if (Test-Path -LiteralPath ("IIS:\Sites\" + $SiteNameToUse)) {
        $site = Get-Website -Name $SiteNameToUse
        if ($site -and $site.State -ne 'Stopped') {
            Stop-WebSite -Name $SiteNameToUse | Out-Null
        }
    }

    foreach ($poolNameToStop in @($PoolName, $PreviousPoolName) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique) {
        if (Test-Path -LiteralPath ("IIS:\AppPools\" + $poolNameToStop)) {
            $appPoolState = Get-WebAppPoolState -Name $poolNameToStop
            if ($appPoolState -and $appPoolState.Value -ne 'Stopped') {
                Stop-WebAppPool -Name $poolNameToStop | Out-Null
            }
        }
    }
}

function Start-SiteAndPool {
    param(
        [string]$SiteNameToUse,
        [string]$PoolName
    )

    Import-Module WebAdministration
    Start-WebAppPool -Name $PoolName | Out-Null
    Start-WebSite -Name $SiteNameToUse | Out-Null
}

function Get-SiteDeploymentState {
    param(
        [string]$SiteNameToUse,
        [string]$FallbackPoolName
    )

    Import-Module WebAdministration
    $state = [pscustomobject]@{
        SiteExists = $false
        PhysicalPath = ''
        ApplicationPool = $FallbackPoolName
        SiteWasRunning = $false
        PoolWasRunning = $false
    }

    if (-not (Test-Path -LiteralPath ("IIS:\Sites\" + $SiteNameToUse))) {
        return $state
    }

    $site = Get-Website -Name $SiteNameToUse
    $state.SiteExists = $true
    $state.PhysicalPath = [string]$site.physicalPath
    $state.ApplicationPool = [string]$site.applicationPool
    $state.SiteWasRunning = $site.State -ne 'Stopped'

    if (Test-Path -LiteralPath ("IIS:\AppPools\" + $state.ApplicationPool)) {
        $poolState = Get-WebAppPoolState -Name $state.ApplicationPool
        $state.PoolWasRunning = $poolState.Value -ne 'Stopped'
    }

    return $state
}

function Restore-SiteDeploymentState {
    param(
        [psobject]$State,
        [string]$SiteNameToUse,
        [switch]$SiteWasCreated
    )

    Import-Module WebAdministration
    if ($SiteWasCreated) {
        if (Test-Path -LiteralPath ("IIS:\Sites\" + $SiteNameToUse)) {
            Stop-WebSite -Name $SiteNameToUse -ErrorAction SilentlyContinue | Out-Null
            Remove-Website -Name $SiteNameToUse
        }

        return
    }

    if (-not $State.SiteExists) {
        return
    }

    if (Test-Path -LiteralPath ("IIS:\Sites\" + $SiteNameToUse)) {
        Stop-WebSite -Name $SiteNameToUse -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty ("IIS:\Sites\" + $SiteNameToUse) -Name physicalPath -Value $State.PhysicalPath
        Set-ItemProperty ("IIS:\Sites\" + $SiteNameToUse) -Name applicationPool -Value $State.ApplicationPool
    }

    if ($State.PoolWasRunning -and (Test-Path -LiteralPath ("IIS:\AppPools\" + $State.ApplicationPool))) {
        Start-WebAppPool -Name $State.ApplicationPool | Out-Null
    }

    if ($State.SiteWasRunning) {
        Start-WebSite -Name $SiteNameToUse | Out-Null
    }
}

function Copy-ArtifactPublish {
    param(
        [string]$ArtifactPublishPath,
        [string]$ReleasePublishPath
    )

    Ensure-Directory -Path $ReleasePublishPath
    Get-ChildItem -LiteralPath $ArtifactPublishPath -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $ReleasePublishPath -Force -Recurse
    }
}

function Copy-RuntimeState {
    param(
        [string]$ExistingPublishPath,
        [string]$ReleasePublishPath
    )

    if ([string]::IsNullOrWhiteSpace($ExistingPublishPath) -or
        -not (Test-Path -LiteralPath $ExistingPublishPath) -or
        [string]::Equals($ExistingPublishPath, $ReleasePublishPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        return
    }

    foreach ($relativePath in @('App_Data', 'wwwroot\uploads\images')) {
        $sourcePath = Join-Path $ExistingPublishPath $relativePath
        if (-not (Test-Path -LiteralPath $sourcePath)) {
            continue
        }

        $destinationPath = Join-Path $ReleasePublishPath $relativePath
        Ensure-Directory -Path $destinationPath
        Get-ChildItem -LiteralPath $sourcePath -Force | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $destinationPath -Force -Recurse
        }
    }
}

function Invoke-MigrationBundle {
    param(
        [string]$MigrationBundlePath,
        [string]$PublishPath,
        [string]$ConnectionStringToUse,
        [string]$JwtKeyToUse,
        [string]$JwtIssuerToUse,
        [string]$JwtAudienceToUse,
        [string]$PublicBaseUrlToUse
    )

    $environmentValues = @{
        'ASPNETCORE_ENVIRONMENT' = 'Production'
        'ConnectionStrings__DefaultConnection' = $ConnectionStringToUse
        'Jwt__Key' = $JwtKeyToUse
        'Jwt__Issuer' = $JwtIssuerToUse
        'Jwt__Audience' = $JwtAudienceToUse
        'PublicApp__BaseUrl' = $PublicBaseUrlToUse
        'Cors__AllowedOrigins__0' = $PublicBaseUrlToUse
    }
    $previousValues = @{}
    $pushedLocation = $false

    try {
        foreach ($entry in $environmentValues.GetEnumerator()) {
            $previousValues[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
            [Environment]::SetEnvironmentVariable($entry.Key, [string]$entry.Value, 'Process')
        }

        Push-Location $PublishPath
        $pushedLocation = $true
        & $MigrationBundlePath --connection $ConnectionStringToUse
        if ($LASTEXITCODE -ne 0) {
            throw 'The bundled EF Core migration failed.'
        }
    }
    finally {
        if ($pushedLocation) {
            Pop-Location
        }

        foreach ($entry in $previousValues.GetEnumerator()) {
            [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
        }
    }
}

function Set-WebConfigEnvironmentVariables {
    param(
        [string]$WebConfigPath,
        [hashtable]$EnvironmentVariables
    )

    [xml]$webConfig = Get-Content -LiteralPath $WebConfigPath
    $configurationNode = $webConfig.SelectSingleNode('/configuration')
    if (-not $configurationNode) {
        throw "Invalid web.config at $WebConfigPath"
    }

    $systemWebServerNode = $webConfig.SelectSingleNode('/configuration/system.webServer')
    if (-not $systemWebServerNode) {
        $systemWebServerNode = $webConfig.SelectSingleNode('/configuration/location/system.webServer')
    }
    if (-not $systemWebServerNode) {
        $systemWebServerNode = $webConfig.CreateElement('system.webServer')
        [void]$configurationNode.AppendChild($systemWebServerNode)
    }

    $aspNetCoreNode = $webConfig.SelectSingleNode('//aspNetCore')
    if (-not $aspNetCoreNode) {
        throw 'The published web.config does not contain a system.webServer/aspNetCore node.'
    }

    $environmentVariablesNode = $aspNetCoreNode.SelectSingleNode('./environmentVariables')
    if (-not $environmentVariablesNode) {
        $environmentVariablesNode = $webConfig.CreateElement('environmentVariables')
        [void]$aspNetCoreNode.AppendChild($environmentVariablesNode)
    }

    foreach ($entry in $EnvironmentVariables.GetEnumerator()) {
        $name = [string]$entry.Key
        $value = [string]$entry.Value

        $existingNode = $null
        foreach ($envNode in $environmentVariablesNode.environmentVariable) {
            if ($envNode.name -eq $name) {
                $existingNode = $envNode
                break
            }
        }

        if ($existingNode -eq $null) {
            $existingNode = $webConfig.CreateElement('environmentVariable')
            [void]$existingNode.SetAttribute('name', $name)
            [void]$environmentVariablesNode.AppendChild($existingNode)
        }

        [void]$existingNode.SetAttribute('value', $value)
    }

    $webConfig.Save($WebConfigPath)
}

function Grant-DirectoryRights {
    param(
        [string]$Path,
        [string]$Identity,
        [System.Security.AccessControl.FileSystemRights]$Rights
    )

    $acl = Get-Acl -LiteralPath $Path
    $inheritanceFlags = [System.Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit'
    $propagationFlags = [System.Security.AccessControl.PropagationFlags]::None
    $accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule($Identity, $Rights, $inheritanceFlags, $propagationFlags, 'Allow')
    $acl.SetAccessRule($accessRule)
    Set-Acl -LiteralPath $Path -AclObject $acl
}

function Grant-ApplicationAccess {
    param(
        [string]$Path,
        [string]$AppPoolName,
        [System.Security.AccessControl.FileSystemRights]$Rights
    )

    $preferredIdentity = "IIS APPPOOL\$AppPoolName"

    try {
        Grant-DirectoryRights -Path $Path -Identity $preferredIdentity -Rights $Rights
        Write-Host "Granted $Rights to $preferredIdentity on $Path" -ForegroundColor Green
        return
    }
    catch [System.Management.Automation.MethodInvocationException] {
        Write-Warning "Could not grant rights to $preferredIdentity on $Path. Falling back to IIS_IUSRS."
    }
    catch {
        Write-Warning "Could not grant rights to $preferredIdentity on $Path. Falling back to IIS_IUSRS."
    }

    Grant-DirectoryRights -Path $Path -Identity 'IIS_IUSRS' -Rights $Rights
    Write-Host "Granted $Rights to IIS_IUSRS on $Path" -ForegroundColor Yellow
}

function Invoke-NonQuerySql {
    param(
        [string]$ConnectionStringToUse,
        [string]$Sql
    )

    Add-Type -AssemblyName System.Data
    $connection = New-Object System.Data.SqlClient.SqlConnection $ConnectionStringToUse
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $Sql
        [void]$command.ExecuteNonQuery()
    }
    finally {
        $connection.Dispose()
    }
}

function Ensure-AppPoolSqlAccess {
    param(
        [string]$SqlServerInstance,
        [string]$TargetDatabaseName,
        [string]$PoolName
    )

    $principal = "IIS APPPOOL\$PoolName"
    $escapedPrincipal = $principal.Replace("'", "''")
    $masterConnectionString = "Server=$SqlServerInstance;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;"

    $loginSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$escapedPrincipal')
BEGIN
    CREATE LOGIN [$principal] FROM WINDOWS;
END
"@
    Invoke-NonQuerySql -ConnectionStringToUse $masterConnectionString -Sql $loginSql

    $dbConnectionString = "Server=$SqlServerInstance;Database=$TargetDatabaseName;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;"
    $userSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$escapedPrincipal')
BEGIN
    CREATE USER [$principal] FOR LOGIN [$principal];
END

IF NOT EXISTS
(
    SELECT 1
    FROM sys.database_role_members drm
    INNER JOIN sys.database_principals role_principal ON drm.role_principal_id = role_principal.principal_id
    INNER JOIN sys.database_principals member_principal ON drm.member_principal_id = member_principal.principal_id
    WHERE role_principal.name = N'db_owner'
      AND member_principal.name = N'$escapedPrincipal'
)
BEGIN
    ALTER ROLE [db_owner] ADD MEMBER [$principal];
END
"@
    Invoke-NonQuerySql -ConnectionStringToUse $dbConnectionString -Sql $userSql
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $ConnectionString = "Server=$SqlInstance;Database=$DatabaseName;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=True;"
}

if ([string]::IsNullOrWhiteSpace($PublicBaseUrl)) {
    throw 'PublicBaseUrl is required. Set it in the settings file or pass it directly to the installer.'
}

if ([string]::IsNullOrWhiteSpace($JwtKey) -or $JwtKey.Length -lt 32) {
    throw 'JwtKey is required and must be at least 32 characters long.'
}

if (-not [string]::IsNullOrWhiteSpace($BootstrapAdminEmail) -and [string]::IsNullOrWhiteSpace($BootstrapAdminPassword)) {
    throw 'BootstrapAdminPassword is required when BootstrapAdminEmail is set.'
}

if ($CorsOrigins.Count -eq 0) {
    $CorsOrigins = @($PublicBaseUrl.TrimEnd('/'))
}

$effectiveHostName = Get-TargetHostName -ConfiguredHostName $HostName -BaseUrl $PublicBaseUrl

Write-Step 'Validating the immutable release artifact'
$artifact = Get-ReleaseArtifact -DeploymentRootPath $DeploymentRoot
$dotnetExe = Ensure-DotNetInstalled
Initialize-DotNetEnvironment -DotNetExe $dotnetExe
$appCmdExe = Ensure-IisModule
$deploymentState = Get-SiteDeploymentState -SiteNameToUse $SiteName -FallbackPoolName $AppPoolName
$deploymentPoolNames = @($deploymentState.ApplicationPool, $AppPoolName) |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -Unique
$otherSitesUsingDeploymentPools = @(Get-Website | Where-Object {
    -not [string]::Equals([string]$_.Name, $SiteName, [System.StringComparison]::OrdinalIgnoreCase) -and
    $deploymentPoolNames -contains [string]$_.applicationPool
})
if ($otherSitesUsingDeploymentPools.Count -gt 0) {
    $otherSiteNames = ($otherSitesUsingDeploymentPools | ForEach-Object { $_.Name }) -join ', '
    throw "The current or requested application pool is shared by other IIS site(s): $otherSiteNames. Assign a dedicated application pool and retry."
}
$releaseRoot = Join-Path (Join-Path $artifact.Root 'releases') "$($artifact.Commit)-$([Guid]::NewGuid().ToString('N'))"
$publishPath = Join-Path $releaseRoot 'publish'

Write-Step 'Staging the prebuilt application'
Copy-ArtifactPublish -ArtifactPublishPath $artifact.PublishPath -ReleasePublishPath $publishPath

$appDataPath = Join-Path $publishPath 'App_Data'
$uploadsPath = Join-Path $appDataPath 'uploads'
$keysPath = Join-Path $appDataPath 'keys'
$imageUploadPath = Join-Path $publishPath 'wwwroot\uploads\images'
Ensure-Directory -Path $appDataPath
Ensure-Directory -Path $uploadsPath
Ensure-Directory -Path $keysPath
Ensure-Directory -Path $imageUploadPath

Write-Step 'Writing production environment variables into the staged web.config'
$environmentVariables = @{
    'ASPNETCORE_ENVIRONMENT' = 'Production'
    'ConnectionStrings__DefaultConnection' = $ConnectionString
    'Jwt__Key' = $JwtKey
    'Jwt__Issuer' = $JwtIssuer
    'Jwt__Audience' = $JwtAudience
    'PublicApp__BaseUrl' = $PublicBaseUrl.TrimEnd('/')
    'Swagger__Enabled' = $(if ($EnableSwagger) { 'true' } else { 'false' })
    'HttpsRedirection__Enabled' = $(if ($EnableHttpsRedirection) { 'true' } else { 'false' })
    'RateLimiting__AuthAttemptsPerMinute' = [string]$AuthAttemptsPerMinute
    'RateLimiting__GuestQuizLoadsPerMinute' = [string]$GuestQuizLoadsPerMinute
    'RateLimiting__AuthenticatedQuizLoadsPerMinute' = [string]$AuthenticatedQuizLoadsPerMinute
}

if (-not [string]::IsNullOrWhiteSpace($BootstrapAdminEmail)) {
    $environmentVariables['BootstrapAdmin__Email'] = $BootstrapAdminEmail.Trim()
    $environmentVariables['BootstrapAdmin__Password'] = $BootstrapAdminPassword

    if (-not [string]::IsNullOrWhiteSpace($BootstrapAdminFirstName)) {
        $environmentVariables['BootstrapAdmin__FirstName'] = $BootstrapAdminFirstName.Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($BootstrapAdminLastName)) {
        $environmentVariables['BootstrapAdmin__LastName'] = $BootstrapAdminLastName.Trim()
    }
}

for ($i = 0; $i -lt $CorsOrigins.Count; $i++) {
    $environmentVariables["Cors__AllowedOrigins__$i"] = $CorsOrigins[$i].TrimEnd('/')
}

Set-WebConfigEnvironmentVariables -WebConfigPath (Join-Path $publishPath 'web.config') -EnvironmentVariables $environmentVariables

$effectiveCertificateThumbprint = Ensure-HttpsCertificate -RequestedThumbprint $CertificateThumbprint -DnsName $effectiveHostName -AllowCreateSelfSigned:$CreateSelfSignedCertificate
$siteWasCreated = $false
$migrationAttempted = $false
try {
    Write-Step 'Stopping IIS site and app pool if they already exist'
    Stop-SiteAndPoolIfPresent -SiteNameToUse $SiteName -PoolName $AppPoolName -PreviousPoolName $deploymentState.ApplicationPool

    Write-Step 'Preserving runtime state'
    Copy-RuntimeState -ExistingPublishPath $deploymentState.PhysicalPath -ReleasePublishPath $publishPath

    Write-Step 'Creating and configuring the IIS application pool'
    Ensure-AppPool -AppCmdExe $appCmdExe -PoolName $AppPoolName

    Write-Step 'Preparing runtime permissions'
    Grant-ApplicationAccess -Path $publishPath -AppPoolName $AppPoolName -Rights ([System.Security.AccessControl.FileSystemRights]::ReadAndExecute)
    Grant-ApplicationAccess -Path $appDataPath -AppPoolName $AppPoolName -Rights ([System.Security.AccessControl.FileSystemRights]::Modify)
    Grant-ApplicationAccess -Path $imageUploadPath -AppPoolName $AppPoolName -Rights ([System.Security.AccessControl.FileSystemRights]::Modify)

    Write-Step 'Running the bundled EF Core migration'
    $migrationAttempted = $true
    Invoke-MigrationBundle `
        -MigrationBundlePath $artifact.MigrationBundlePath `
        -PublishPath $publishPath `
        -ConnectionStringToUse $ConnectionString `
        -JwtKeyToUse $JwtKey `
        -JwtIssuerToUse $JwtIssuer `
        -JwtAudienceToUse $JwtAudience `
        -PublicBaseUrlToUse $PublicBaseUrl.TrimEnd('/')

    Write-Step 'Ensuring the bootstrap admin account exists'
    Invoke-BootstrapAdminTool `
        -DotNetExe $dotnetExe `
        -BootstrapperPath $artifact.BootstrapperPath `
        -ConnectionStringToUse $ConnectionString `
        -Email $BootstrapAdminEmail `
        -Password $BootstrapAdminPassword `
        -FirstName $BootstrapAdminFirstName `
        -LastName $BootstrapAdminLastName

    Write-Step 'Granting the IIS application pool identity access to the database'
    Ensure-AppPoolSqlAccess -SqlServerInstance $SqlInstance -TargetDatabaseName $DatabaseName -PoolName $AppPoolName

    Write-Step 'Pointing IIS at the staged release'
    $siteWasCreated = Ensure-SiteConfiguration -SiteNameToUse $SiteName -PublishPath $publishPath -PoolName $AppPoolName -HttpBindingPort $HttpPort

    Write-Step 'Ensuring IIS serves the application over both HTTP and HTTPS'
    Ensure-SiteBindings -SiteNameToUse $SiteName -HttpBindingPort $HttpPort -HttpsBindingPort $HttpsPort -DnsName $effectiveHostName -HttpsCertificateThumbprint $effectiveCertificateThumbprint

    Write-Step 'Starting the IIS site and application pool'
    Start-SiteAndPool -SiteNameToUse $SiteName -PoolName $AppPoolName
}
catch {
    if ($migrationAttempted) {
        try {
            Stop-SiteAndPoolIfPresent -SiteNameToUse $SiteName -PoolName $AppPoolName -PreviousPoolName $deploymentState.ApplicationPool
        }
        catch {
            Write-Warning "Could not stop IIS after the migration failure: $($_.Exception.Message)"
        }

        Write-Warning 'A database migration was attempted. Keep IIS stopped and verify database/application compatibility before restarting it.'
        Write-Warning 'Review the migration result, then complete this release or restore database/application compatibility manually.'
    }
    else {
        Write-Warning 'Installation failed. Restoring the previous IIS site path when possible.'
        try {
            Restore-SiteDeploymentState -State $deploymentState -SiteNameToUse $SiteName -SiteWasCreated:$siteWasCreated
        }
        catch {
            Write-Warning "Could not restore the previous IIS configuration: $($_.Exception.Message)"
        }
    }

    throw
}

Write-Step 'Installation summary'
Write-Host "Deployment root: $($artifact.Root)" -ForegroundColor Green
Write-Host "Release commit: $($artifact.Commit)" -ForegroundColor Green
Write-Host "Published site path: $publishPath" -ForegroundColor Green
Write-Host "IIS site: $SiteName" -ForegroundColor Green
Write-Host "Application pool: $AppPoolName" -ForegroundColor Green
Write-Host "Public base URL: $($PublicBaseUrl.TrimEnd('/'))" -ForegroundColor Green
Write-Host "HTTP binding: http://$effectiveHostName`:$HttpPort" -ForegroundColor Green
Write-Host "HTTPS binding: https://$effectiveHostName`:$HttpsPort" -ForegroundColor Green
Write-Host "HTTPS certificate thumbprint: $effectiveCertificateThumbprint" -ForegroundColor Green

Write-Host ''
Write-Host 'Production installation is complete. Run post-deploy smoke checks next.' -ForegroundColor Green
