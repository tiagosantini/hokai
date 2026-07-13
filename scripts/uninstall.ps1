<#
.SYNOPSIS
    Hokai Uninstaller — Windows
.DESCRIPTION
    Stops the service, removes the binary, and cleans up PATH.
    Idempotent — safe to run multiple times.
.PARAMETER Purge
    Also remove configuration and data from ProgramData.
.PARAMETER Help
    Show help message.
#>

param(
    [switch]$Purge,
    [switch]$Help
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$BinaryName = "hokai.exe"
$DataDir = "$env:ProgramData\Hokai"

if ($Help) {
    Write-Host "Usage: uninstall.ps1 [-Purge] [-Help]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -Purge   Remove configuration and data as well"
    Write-Host "  -Help    Show this help message"
    exit 0
}

# --- Locate installation ---
$elevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")

if ($elevated) {
    $InstallDir = "$env:ProgramFiles\Hokai"
    $PathTarget = "Machine"
} else {
    $InstallDir = "$env:LocalAppData\Programs\Hokai"
    $PathTarget = "User"
}

$BinaryPath = if ($InstallDir) { Join-Path $InstallDir $BinaryName } else { $null }

# --- Stop and unregister service ---
if ($BinaryPath -and (Test-Path $BinaryPath)) {
    Write-Host "Stopping Hokai service..."
    & $BinaryPath service stop 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Service stop exited with code $LASTEXITCODE (may be expected if not running)"
    }

    Write-Host "Unregistering Hokai service..."
    if ($Purge) {
        & $BinaryPath service uninstall --purge 2>$null
    } else {
        & $BinaryPath service uninstall 2>$null
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Service uninstall exited with code $LASTEXITCODE"
    }
} else {
    Write-Host "Hokai binary not found at expected path. Attempting native cleanup..."
    sc.exe stop hokai 2>$null
    sc.exe delete hokai 2>$null
}

# --- Remove binary ---
if ($InstallDir -and (Test-Path $InstallDir)) {
    Remove-Item -Recurse -Force $InstallDir -ErrorAction SilentlyContinue
    Write-Host "Binary removed from $InstallDir"
} else {
    Write-Host "Install directory not found."
}

# --- Remove from PATH (exact segment match) ---
$scope = [EnvironmentVariableTarget]::$PathTarget
$currentPath = [Environment]::GetEnvironmentVariable("Path", $scope)
if ($currentPath -and $InstallDir) {
    $segments = $currentPath -split [IO.Path]::PathSeparator |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_.Length -gt 0 -and $_ -ne $InstallDir }
    $newPath = $segments -join [IO.Path]::PathSeparator
    if ($newPath -ne $currentPath) {
        [Environment]::SetEnvironmentVariable("Path", $newPath, $scope)
        Write-Host "Removed $InstallDir from $PathTarget PATH"
    } else {
        Write-Host "$InstallDir was not found in PATH"
    }
}

# --- Purge ---
if ($Purge) {
    if (Test-Path $DataDir) {
        Remove-Item -Recurse -Force $DataDir -ErrorAction SilentlyContinue
        Write-Host "Configuration and data removed from $DataDir"
    }
} else {
    Write-Host "Configuration and data preserved. Use -Purge to remove them."
}

Write-Host "Hokai uninstalled."
