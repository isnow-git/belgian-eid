#Requires -Version 5.1
<#
.SYNOPSIS
    Registers BelgianEidBridge as a Chrome Native Messaging host.

.DESCRIPTION
    Generates the Native Messaging manifest JSON, writes it next to this script,
    and creates (or updates) the required registry key under HKCU so that Chrome
    can locate and launch the host when the extension calls connectNative().

.PARAMETER ExtensionId
    The Chrome extension ID (32 lowercase letters, visible in chrome://extensions).
    Example: abcdefghijklmnopabcdefghijklmnop

.PARAMETER ExePath
    Absolute path to BelgianEidBridge.exe.
    Defaults to "publish\BelgianEidBridge.exe" in the same folder as this script.

.EXAMPLE
    .\register.ps1 -ExtensionId abcdefghijklmnopabcdefghijklmnop

.EXAMPLE
    .\register.ps1 -ExtensionId abcdefghijklmnopabcdefghijklmnop -ExePath "C:\Tools\BelgianEidBridge.exe"

.NOTES
    Run once after each fresh publish. Restart Chrome if it was already open.
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-z]{32}$')]
    [string] $ExtensionId,

    [Parameter(Mandatory = $false)]
    [string] $ExePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Resolve executable path ───────────────────────────────────────────────────

if (-not $ExePath)
{
    $ExePath = Join-Path $PSScriptRoot 'publish\BelgianEidBridge.exe'
}

$ExePath = [System.IO.Path]::GetFullPath($ExePath)

if (-not (Test-Path $ExePath -PathType Leaf))
{
    Write-Error @"
BelgianEidBridge.exe not found at '$ExePath'.
Publish the project first:
    dotnet publish -c Release -r win-x64 --self-contained -o publish
"@
    exit 1
}

# ── Generate the Native Messaging manifest ────────────────────────────────────

$NativeHostName = 'be.belgianeid.bridge'

$Manifest = [ordered]@{
    name            = $NativeHostName
    description     = 'Belgian eID Bridge — read Belgian eID cards via PC/SC'
    path            = $ExePath
    type            = 'stdio'
    allowed_origins = @("chrome-extension://$ExtensionId/")
}

$ManifestJson = $Manifest | ConvertTo-Json -Depth 3
$ManifestPath = Join-Path $PSScriptRoot "$NativeHostName.json"

[System.IO.File]::WriteAllText($ManifestPath, $ManifestJson, [System.Text.Encoding]::UTF8)

# ── Register in the Chrome native messaging registry key ──────────────────────

$RegPath = "HKCU:\Software\Google\Chrome\NativeMessagingHosts\$NativeHostName"

if (-not (Test-Path $RegPath))
{
    New-Item -Path $RegPath -Force | Out-Null
}
Set-ItemProperty -Path $RegPath -Name '(default)' -Value $ManifestPath -Type String

# ── Summary ───────────────────────────────────────────────────────────────────

Write-Host ''
Write-Host '  Native Messaging host registered successfully.' -ForegroundColor Green
Write-Host ''
Write-Host "  Executable : $ExePath"
Write-Host "  Manifest   : $ManifestPath"
Write-Host "  Registry   : $RegPath"
Write-Host "  Extension  : chrome-extension://$ExtensionId/"
Write-Host ''
Write-Host '  Restart Chrome if it was already running.' -ForegroundColor Yellow
Write-Host ''
