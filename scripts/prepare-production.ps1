[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path ([System.IO.Path]::GetTempPath()) 'TheCertMaster-local-publish')
)

$ErrorActionPreference = 'Stop'

$sourceRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $sourceRoot 'TheCertMaster.csproj'

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw 'This local development helper requires a source checkout. Deploy the immutable CI archive instead of compiling on a target server.'
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK is not installed or not available on PATH.'
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $resolvedOutput | Out-Null

Write-Warning 'This historical helper is for local development troubleshooting only. It is not a production deployment package.'
& dotnet publish $projectPath --configuration Release -p:UseAppHost=false --output $resolvedOutput
if ($LASTEXITCODE -ne 0) {
    throw 'The local development publish failed.'
}

Write-Host "Local development publish: $resolvedOutput" -ForegroundColor Green
Write-Host 'For production, deploy the immutable CI archive and run its packaged installer.' -ForegroundColor Green
