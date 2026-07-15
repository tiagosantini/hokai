<#
.SYNOPSIS
    Hokai Installer — Windows
.DESCRIPTION
    Downloads and installs the latest Hokai binary as a Windows Service.
    Idempotent — safe to run multiple times.
.PARAMETER Version
    Version to install (default: latest).
.PARAMETER SkipService
    Skip service registration.
.PARAMETER Help
    Show help message.
#>

param(
    [string]$Version = "latest",
    [switch]$SkipService,
    [switch]$Help
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$Repo = "tiagosantini/hokai"
$BinaryName = "hokai.exe"

if ($Help) {
    Write-Host "Usage: install.ps1 [-Version <version>] [-SkipService] [-Help]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -Version       Version to install (default: latest)"
    Write-Host "  -SkipService   Skip service registration"
    Write-Host "  -Help          Show this help message"
    exit 0
}

# --- Platform detection ---
if (-not [Environment]::Is64BitOperatingSystem) {
    Write-Error "x86 (32-bit) is not supported. Please run on a 64-bit system."
    exit 1
}

$arch = switch ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture) {
    'Arm64'   { 'arm64' }
    'X64'     { 'x64' }
    default   { 'x64' }
}
$platform = "win-$arch"

# --- Elevation & install targets ---
$elevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")

if ($elevated) {
    $InstallDir = "$env:ProgramFiles\Hokai"
    $PathTarget = "Machine"
} else {
    Write-Host "Not running as Administrator. Installing per-user to LocalAppData."
    $InstallDir = "$env:LocalAppData\Programs\Hokai"
    $PathTarget = "User"
    if (-not $SkipService) {
        Write-Host "Skipping service registration (requires Administrator). Use --SkipService to suppress."
        $SkipService = $true
    }
}

$BinaryPath = Join-Path $InstallDir $BinaryName

# --- Download ---
$TempDir = Join-Path $env:TEMP "hokai_install_$(Get-Random)"
New-Item -ItemType Directory -Force -Path $TempDir | Out-Null

try {
    if ($Version -eq "latest") {
        Write-Host "Resolving latest release..."
        $apiUrl = "https://api.github.com/repos/$Repo/releases?per_page=1"
        try {
            $releases = Invoke-RestMethod -Uri $apiUrl -Headers @{ Accept = "application/vnd.github+json" }
            $Version = $releases[0].tag_name
            Write-Host "Latest release: $Version"
        } catch {
            Write-Error "Could not resolve latest release. Specify --version explicitly."
            exit 1
        }
    }
    $zipUrl = "https://github.com/$Repo/releases/download/$Version/hokai-$platform.zip"
    $checksumUrl = "https://github.com/$Repo/releases/download/$Version/SHA256SUMS"

    Write-Host "Downloading hokai $Version for $platform..."
    $zipPath = Join-Path $TempDir "hokai.zip"
    Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath

    Write-Host "Downloading checksums..."
    $checksumPath = Join-Path $TempDir "SHA256SUMS"
    try {
        Invoke-WebRequest -Uri $checksumUrl -OutFile $checksumPath
    } catch {
        Write-Error "Could not download checksums. Aborting."
        exit 1
    }

    Write-Host "Verifying checksum..."
    $sums = Get-Content $checksumPath
    $expected = $null
    $archiveName = "hokai-$platform.zip"
    foreach ($line in $sums) {
        if ($line -match "^\s*([0-9a-fA-F]{64})\s+[ \*]?(.*)") {
            if ($Matches[2] -eq $archiveName) {
                $expected = $Matches[1]
                break
            }
        }
    }
    if (-not $expected) {
        Write-Error "Archive $archiveName not found in SHA256SUMS. Aborting."
        exit 1
    }
    $actual = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($expected.ToLowerInvariant() -ne $actual) {
        Write-Error "Checksum mismatch! Aborting."
        exit 1
    }
    Write-Host "Checksum verified."

    Write-Host "Extracting..."
    $extractDir = Join-Path $TempDir "extract"
    Expand-Archive -Path $zipPath -DestinationPath $extractDir

    # Locate hokai.exe inside the extracted tree
    $exePath = Get-ChildItem -Path $extractDir -Recurse -Filter $BinaryName | Select-Object -First 1
    if (-not $exePath) {
        Write-Error "hokai.exe not found in extracted archive."
        exit 1
    }

    # --- Install binary ---
    Write-Host "Installing to $InstallDir..."
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    Copy-Item -Force $exePath.FullName $BinaryPath

    # --- Configure PATH ---
    $scope = [EnvironmentVariableTarget]::$PathTarget
    $currentPath = [Environment]::GetEnvironmentVariable("Path", $scope)
    $segments = $currentPath -split [IO.Path]::PathSeparator | ForEach-Object { $_.Trim() }
    if ($segments -contains $InstallDir) {
        Write-Host "PATH already contains $InstallDir"
    } else {
        $newPath = if ($currentPath) { "$currentPath$([IO.Path]::PathSeparator)$InstallDir" } else { $InstallDir }
        [Environment]::SetEnvironmentVariable("Path", $newPath, $scope)
        Write-Host "Added $InstallDir to $PathTarget PATH"
    }

    # --- Install service ---
    if (-not $SkipService) {
        Write-Host "Installing service..."
        & $BinaryPath service install
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Service installation failed with exit code $LASTEXITCODE"
            exit $LASTEXITCODE
        }
        Write-Host "Service installed."
    }

    Write-Host ""
    Write-Host "Hokai installed successfully."
    Write-Host "  Binary:  $BinaryPath"
    if (-not $SkipService) {
        Write-Host "  Service: installed"
    }
    Write-Host ""
    Write-Host "To add an endpoint:  hokai endpoint add <url>"
    Write-Host "To check status:      hokai status"
    Write-Host "To start daemon:      hokai run"

} finally {
    Remove-Item -Recurse -Force $TempDir -ErrorAction SilentlyContinue
}
