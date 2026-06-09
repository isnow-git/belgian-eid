#Requires -Version 7
<#
.SYNOPSIS
    Build pipeline — bridge, Chrome extension, Windows installer.

.DESCRIPTION
    Publishes the .NET bridge, packages the extension as a .crx via Chrome,
    computes the stable extension ID, then compiles the Inno Setup installer.

    First run: a signing key is generated at installer/extension-key.pem.
    Keep this file — losing it changes the extension ID on already-installed machines.

.PARAMETER SkipBridge
    Reuses existing bridge artefacts (skips dotnet publish).

.PARAMETER SkipExtension
    Reuses the existing .crx (skips Chrome repackaging).

.PARAMETER SkipInstaller
    Does not compile the Inno Setup installer (useful for debugging artefacts).

.EXAMPLE
    .\build.ps1
    .\build.ps1 -SkipBridge -SkipExtension   # Recompile only the installer
#>
param(
    [switch]$SkipBridge,
    [switch]$SkipExtension,
    [switch]$SkipInstaller,
    # Explicit path to iscc.exe if automatic detection fails.
    # Example: .\build.ps1 -IsccPath "C:\MyTools\InnoSetup\iscc.exe"
    [string]$IsccPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root         = Split-Path $PSScriptRoot -Parent
$InstallerDir = $PSScriptRoot
$ArtifactsDir = Join-Path $InstallerDir 'artifacts'
$BridgeSrc    = Join-Path $Root 'bridge'
$ExtSrc       = Join-Path $Root 'extension'
$KeyFile      = Join-Path $InstallerDir 'extension-key.pem'
$ManifestJson = Join-Path $ExtSrc 'manifest.json'
$NmHostTemplate = Join-Path $BridgeSrc 'be.belgianeid.bridge.template.json'
$NmHostJson   = Join-Path $BridgeSrc 'be.belgianeid.bridge.json'

foreach ($dir in @($ArtifactsDir, "$ArtifactsDir\bridge", "$ArtifactsDir\extension")) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

# ─────────────────────────────────────────────────────────────────────────────
# Helpers
# ─────────────────────────────────────────────────────────────────────────────

function Write-Step([int]$n, [int]$total, [string]$label) {
    Write-Host ''
    Write-Host "[$n/$total] $label" -ForegroundColor Cyan
}

function Write-Ok([string]$msg) { Write-Host "    $msg" -ForegroundColor Green }
function Write-Warn([string]$msg) { Write-Host "    $msg" -ForegroundColor Yellow }

function Find-Chrome {
    $paths = @(
        "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
        "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
        "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe",
        "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"   # Edge also supports --pack-extension
    )
    $found = $paths | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $found) {
        throw 'Google Chrome (or Edge) not found. Install Chrome and rerun the script.'
    }
    return $found
}

function Find-Iscc {
    if ($IsccPath -and (Test-Path $IsccPath)) { return $IsccPath }

    $candidates = @(
        'C:\Program Files (x86)\Inno Setup 6\iscc.exe',
        'C:\Program Files\Inno Setup 6\iscc.exe',
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\iscc.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    $found = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $found) {
        $found = (Get-Command iscc.exe -ErrorAction SilentlyContinue)?.Source
    }
    if (-not $found) {
        throw "Inno Setup 6 not found.`nDownload: https://jrsoftware.org/isdownload.php"
    }
    return $found
}

# Computes the Chrome extension ID from the private key PEM file.
# Algorithm: SHA-256 of SubjectPublicKeyInfo (DER) → first 16 bytes → a-p alphabet.
function Get-ExtensionId([string]$PemPath) {
    $pem   = Get-Content $PemPath -Raw
    $b64   = $pem -replace '-----[^-]+-----', '' -replace '\s+', ''
    $bytes = [Convert]::FromBase64String($b64)

    $rsa = [System.Security.Cryptography.RSA]::Create()
    try {
        [int]$read = 0
        try   { $rsa.ImportPkcs8PrivateKey($bytes, [ref]$read) }   # PKCS#8 (modern Chrome)
        catch { $rsa.ImportRSAPrivateKey($bytes, [ref]$read) }       # PKCS#1 (older Chrome)
        $spki = $rsa.ExportSubjectPublicKeyInfo()
    }
    finally { $rsa.Dispose() }

    $hash = [System.Security.Cryptography.SHA256]::HashData($spki)
    $sb   = [System.Text.StringBuilder]::new(32)
    for ($i = 0; $i -lt 16; $i++) {
        $null = $sb.Append([char]([int][char]'a' + ($hash[$i] -shr 4)))
        $null = $sb.Append([char]([int][char]'a' + ($hash[$i] -band 0xF)))
    }
    return $sb.ToString()
}

# Adds/updates the "key" field in manifest.json so that the unpacked (dev)
# extension ID matches the .crx ID.
function Set-ManifestKey([string]$PemPath, [string]$ManifestPath) {
    $pem   = Get-Content $PemPath -Raw
    $b64   = $pem -replace '-----[^-]+-----', '' -replace '\s+', ''
    $bytes = [Convert]::FromBase64String($b64)

    $rsa = [System.Security.Cryptography.RSA]::Create()
    try {
        [int]$read = 0
        try   { $rsa.ImportPkcs8PrivateKey($bytes, [ref]$read) }
        catch { $rsa.ImportRSAPrivateKey($bytes, [ref]$read) }
        $publicKeyB64 = [Convert]::ToBase64String($rsa.ExportSubjectPublicKeyInfo())
    }
    finally { $rsa.Dispose() }

    $manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json -AsHashtable
    $manifest['key'] = $publicKeyB64
    $manifest | ConvertTo-Json -Depth 10 | Set-Content $ManifestPath -Encoding UTF8NoBOM
}

# Generates the native messaging host JSON from template
function Write-NativeMessagingManifest([string]$TemplatePath, [string]$OutputPath, [string]$ExtensionId) {
    # Create default template if it doesn't exist
    if (-not (Test-Path $TemplatePath)) {
        Write-Warn "Template not found at: $TemplatePath"
        Write-Warn "Creating default template..."

        $defaultTemplate = @'
{
  "name": "be.belgianeid.bridge",
  "description": "Belgian eID Bridge — read Belgian eID cards via PC/SC",
  "path": "{{BRIDGE_PATH}}",
  "type": "stdio",
  "allowed_origins": [
    "chrome-extension://{{EXTENSION_ID}}/"
  ]
}
'@
        # Ensure directory exists
        $templateDir = Split-Path $TemplatePath -Parent
        if (-not (Test-Path $templateDir)) {
            New-Item -ItemType Directory -Force -Path $templateDir | Out-Null
        }
        Set-Content -Path $TemplatePath -Value $defaultTemplate -Encoding UTF8NoBOM
        Write-Ok "Default template created at: $TemplatePath"
    }

    # Read and process template
    $template = Get-Content $TemplatePath -Raw -Encoding UTF8
    $template = $template.Replace('{{EXTENSION_ID}}', $ExtensionId)
    # BRIDGE_PATH will be replaced at install time by the Inno Setup installer
    # So we keep {{BRIDGE_PATH}} as a placeholder

    # Ensure output directory exists
    $outputDir = Split-Path $OutputPath -Parent
    if (-not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
    }

    $template | Set-Content -Path $OutputPath -Encoding UTF8NoBOM
    Write-Ok "Native messaging manifest generated at: $OutputPath"
}

# ─────────────────────────────────────────────────────────────────────────────
# Step 1 — Bridge publication
# ─────────────────────────────────────────────────────────────────────────────

$totalSteps = 3 + (-not $SkipInstaller)

if (-not $SkipBridge) {
    Write-Step 1 $totalSteps 'Publishing the bridge (self-contained, win-x64)...'

    # Stop the bridge if it is running (otherwise DLLs are locked)
    $running = Get-Process -Name 'BelgianEidBridge' -ErrorAction SilentlyContinue
    if ($running) {
        $running | Stop-Process -Force
        Write-Warn 'BelgianEidBridge process stopped.'
        Start-Sleep -Seconds 2
    }

    # Delete the output directory to avoid any residual lock
    $bridgeOut = "$ArtifactsDir\bridge"
    if (Test-Path $bridgeOut) {
        Remove-Item $bridgeOut -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $bridgeOut | Out-Null

    # Clean before publish to force a full recompilation
    & dotnet clean "$BridgeSrc\BelgianEidBridge.csproj" --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) { Write-Warn 'dotnet clean failed (non-blocking).' }

    $publishArgs = @(
        'publish',
        "$BridgeSrc\BelgianEidBridge.csproj",
        '--configuration', 'Release',
        '--runtime',       'win-x64',
        '--self-contained', 'true',
        '--output',        $bridgeOut,
        '--nologo'
    )
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
    Write-Ok 'Bridge published.'

    # Sync the publish\ directory used by the HKCU key (local dev).
    # Chrome prefers HKCU over HKLM — without this copy, the HKCU bridge stays at the old version.
    $devPublish = Join-Path $BridgeSrc 'publish'
    if (Test-Path $devPublish) {
        Copy-Item -Path "$bridgeOut\*" -Destination $devPublish -Recurse -Force
        Write-Ok "Bridge synchronised to publish\ (HKCU dev path)."
    }
}
else {
    Write-Step 1 $totalSteps 'Bridge — step skipped (-SkipBridge).'
}

# ─────────────────────────────────────────────────────────────────────────────
# Step 2 — Chrome extension packaging
# ─────────────────────────────────────────────────────────────────────────────

if (-not $SkipExtension) {
    Write-Step 2 $totalSteps "Packaging the Chrome extension..."

    $chrome  = Find-Chrome
    $tempCrx = "$ExtSrc.crx"
    $tempPem = "$ExtSrc.pem"

    Remove-Item $tempCrx, $tempPem -ErrorAction SilentlyContinue

    $packArgs = @(
        "--pack-extension=$ExtSrc",
        '--no-first-run',
        '--disable-extensions',
        '--no-default-browser-check'
    )
    if (Test-Path $KeyFile) {
        $packArgs += "--pack-extension-key=$KeyFile"
        Write-Ok "Existing key used: $KeyFile"
    }
    else {
        Write-Warn 'No key found — Chrome will generate a new one.'
        Write-Warn "It will be saved at: $KeyFile"
    }

    Write-Warn 'Close all Chrome/Edge instances before continuing.'
    Write-Host '    Press Enter to start packaging...' -NoNewline
    $null = Read-Host

    & $chrome @packArgs 2>$null

    # Wait for the file to be created (Chrome is asynchronous on some versions)
    $timeout = 15
    $elapsed = 0
    while (-not (Test-Path $tempCrx) -and $elapsed -lt $timeout) {
        Start-Sleep -Seconds 1
        $elapsed++
    }

    if (-not (Test-Path $tempCrx)) {
        throw @"
Chrome did not create the .crx file.
Try manually:
  & "$chrome" --pack-extension="$ExtSrc"
then rerun with -SkipExtension.
"@
    }

    Move-Item $tempCrx "$ArtifactsDir\extension\BelgianEid.crx" -Force

    if (Test-Path $tempPem) {
        Move-Item $tempPem $KeyFile -Force
    }

    if (-not (Test-Path $KeyFile)) {
        throw "Key file not found after packaging. Check permissions."
    }

    $extId = Get-ExtensionId $KeyFile
    Write-Ok "Extension packaged. ID: $extId"

    # Update manifest.json with the key
    Set-ManifestKey $KeyFile $ManifestJson
    Write-Ok "manifest.json updated with extension key."

    # Generate native messaging manifest from template (for development use)
    Write-NativeMessagingManifest -TemplatePath $NmHostTemplate -OutputPath $NmHostJson -ExtensionId $extId

    $extId | Set-Content "$ArtifactsDir\extension-id.txt" -NoNewline
}
else {
    Write-Step 2 $totalSteps "Extension — step skipped (-SkipExtension)."
}

# ─────────────────────────────────────────────────────────────────────────────
# Step 3 — Installer configuration generation
# ─────────────────────────────────────────────────────────────────────────────

Write-Step 3 $totalSteps "Generating installer configuration..."

$extIdFile = "$ArtifactsDir\extension-id.txt"
if (-not (Test-Path $extIdFile)) {
    throw "extension-id.txt not found. Run without -SkipExtension first."
}
$extId     = (Get-Content $extIdFile -Raw).Trim()
$appVersion = ((Get-Content $ManifestJson -Raw | ConvertFrom-Json).version)

@"
; Auto-generated by build.ps1 — do not edit manually.
#define AppVersion  "$appVersion"
#define ExtensionId "$extId"
"@ | Set-Content "$ArtifactsDir\installer-config.iss" -Encoding UTF8NoBOM

Write-Ok "Version: $appVersion — Extension ID: $extId"

# ─────────────────────────────────────────────────────────────────────────────
# Step 4 — Inno Setup installer compilation
# ─────────────────────────────────────────────────────────────────────────────

if (-not $SkipInstaller) {
    Write-Step 4 $totalSteps 'Compiling the Inno Setup installer...'

    $iscc   = Find-Iscc
    $issFile = Join-Path $InstallerDir 'setup.iss'

    & $iscc $issFile
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }

    $output = Join-Path $InstallerDir "output\BelgianEidSetup-$appVersion.exe"
    Write-Host ''
    Write-Host "Installer ready: $output" -ForegroundColor Green
}
