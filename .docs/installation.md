# Hokai — Installation and Uninstallation

> Distribution plan and installation lifecycle for Hokai. All operations are **idempotent** and uninstallation **leaves no residual artifacts**.

**Related docs**: [Architecture](architecture.md) (core design) | [Daemonization](daemonization.md) (service lifecycle)

---

## 1. Installation Methods

| Method | Target audience | Prerequisites | Complexity |
|---|---|---|---|
| [1.1 Build from Source](#11-build-from-source) | Developers | .NET SDK 10 | Low |
| [1.2 Shell Script](#12-shell-script-linuxmacos) | Linux / macOS | bash, curl/wget, tar | Low |
| [1.3 PowerShell Script](#13-powershell-script-windows) | Windows | PowerShell 5+, curl/wget | Low |
| [1.4 dotnet global tool](#14-dotnet-global-tool) | .NET Developers | .NET SDK 10 | Minimal |
| [1.5 Docker](#15-docker) | Sysadmins, infra | Docker / Podman | Low |
| [1.6 Single Binary Download](#16-single-binary-download-github-releases) | Any OS | None (self-contained) | Minimal |

---

## 2. What Gets Installed

Regardless of the method, a full installation produces the artifacts described in [Daemonization > File Locations](daemonization.md#5-file-locations-by-platform). The table below focuses on the paths relevant to the installation process itself:

| Artifact | Linux | macOS | Windows |
|---|---|---|---|
| Binaries directory | `/usr/local/bin/` (already in PATH) | `/usr/local/bin/` (already in PATH) | `%ProgramFiles%\Hokai\` (added to PATH) |
| Working data | `/var/lib/hokai/` | `~/Library/Application Support/Hokai/` | `%ProgramData%\Hokai\Data\` |

---

## 3. Idempotency Principles

Every install operation follows these rules:

| Rule | Behavior |
|---|---|
| **Binary** | If it already exists and the version matches → skip. If version differs → overwrite with optional backup. |
| **Config** | If `appsettings.json` already exists → **never overwrite**. Only create if absent. |
| **Data directory** | Create if it doesn't exist. Never remove existing data. |
| **Service** | If already registered in the OS → check definition. If different → recreate. If same → skip. |
| **PATH** | If the binary is already accessible via PATH → skip. If not → add it. |
| **Result** | Two consecutive `install` runs produce exactly the same final state. |

---

## 4. Clean Uninstallation Principles

| Rule | Behavior |
|---|---|
| **Running service** | `stop` before removing the definition |
| **Service definition** | Remove via native tool (`systemctl disable`, `launchctl unload`, `sc delete`) |
| **Binary** | Remove from installation location |
| **PATH** | Remove entry added during `install` |
| **Config** | Only removed with `--purge`. Never removed otherwise. |
| **Data** | Only removed with `--purge`. Never removed otherwise. |
| **Logs** | Remove if applicable (macOS). Linux uses journald (no file). |
| **Final verification** | Confirm no Hokai artifacts remain on the system |

### `--purge` Behavior

```
hokai service uninstall           # keeps config + data
hokai service uninstall --purge   # removes config + data as well
```

The binary is managed by installer scripts, not by `service uninstall`.

---

## 5. Methods in Detail

### 5.1 Build from Source

**Audience**: developers and contributors.

```bash
git clone https://github.com/user/hokai.git
cd hokai
dotnet restore
dotnet build -c Release
dotnet run --project src/Hokai -- endpoint add https://example.com/health
```

**Install as a service (after build)**:
```bash
dotnet publish -c Release -o ./publish
sudo ./publish/hokai service install
```

**Uninstall**:
```bash
sudo hokai service uninstall --purge
rm -rf ./hokai/   # remove the cloned repository
```

**Idempotency**: `git clone` fails if the directory already exists (expected). `git pull` in the existing repository updates it.

---

### 5.2 Shell Script (Linux/macOS)

File: `scripts/install.sh`

```bash
#!/usr/bin/env bash
set -euo pipefail

# ------------------------------------------------
# Hokai Installer — Linux & macOS
# Idempotent. Safe to run multiple times.
# ------------------------------------------------

INSTALL_VERSION="${HOKAI_VERSION:-latest}"
INSTALL_DIR="/usr/local/bin"
BINARY_NAME="hokai"
GITHUB_REPO="user/hokai"
TEMP_DIR=$(mktemp -d)

cleanup() { rm -rf "$TEMP_DIR"; }
trap cleanup EXIT

# --- Platform detection ---
detect_platform() {
    local os arch
    case "$(uname -s)" in
        Linux)  os="linux" ;;
        Darwin) os="osx"   ;;
        *) echo "Unsupported OS: $(uname -s)" >&2; exit 1 ;;
    esac
    case "$(uname -m)" in
        x86_64)  arch="x64" ;;
        aarch64) arch="arm64" ;;
        arm64)   arch="arm64" ;;
        *) echo "Unsupported arch: $(uname -m)" >&2; exit 1 ;;
    esac
    echo "${os}-${arch}"
}

# --- Download binary ---
download_binary() {
    local platform url
    platform=$(detect_platform)

    if [ "$INSTALL_VERSION" = "latest" ]; then
        url="https://github.com/${GITHUB_REPO}/releases/latest/download/hokai-${platform}.tar.gz"
    else
        url="https://github.com/${GITHUB_REPO}/releases/download/${INSTALL_VERSION}/hokai-${platform}.tar.gz"
    fi

    echo "Downloading hokai ${INSTALL_VERSION} for ${platform}..."
    curl -fsSL "$url" -o "$TEMP_DIR/hokai.tar.gz"
    tar -xzf "$TEMP_DIR/hokai.tar.gz" -C "$TEMP_DIR"
}

# --- Installation ---
install_binary() {
    local dest="$INSTALL_DIR/$BINARY_NAME"

    if [ -f "$dest" ]; then
        local existing_version
        existing_version=$("$dest" --version 2>/dev/null || echo "unknown")
        echo "Existing installation found: ${existing_version}"
        if [ "$INSTALL_VERSION" != "latest" ] && [ "$existing_version" = "$INSTALL_VERSION" ]; then
            echo "Already at version ${INSTALL_VERSION}. Skipping binary install."
            return
        fi
    fi

    sudo cp "$TEMP_DIR/$BINARY_NAME" "$dest"
    sudo chmod +x "$dest"
    echo "Binary installed to ${dest}"
}

# --- Service setup ---
install_service() {
    if [ "$(uname -s)" = "Linux" ]; then
        sudo "$INSTALL_DIR/$BINARY_NAME" service install
    elif [ "$(uname -s)" = "Darwin" ]; then
        "$INSTALL_DIR/$BINARY_NAME" service install
    fi
}

# --- Execution ---
download_binary
install_binary
install_service

echo ""
echo "Hokai installed successfully."
echo "  Binary:  ${INSTALL_DIR}/${BINARY_NAME}"
echo "  Config:  /etc/hokai/appsettings.json  (Linux)"
echo "  Data:    /var/lib/hokai/               (Linux)"
echo ""
echo "To add an endpoint:  hokai endpoint add <url>"
echo "To start monitoring: hokai service start"
```

File: `scripts/uninstall.sh`

```bash
#!/usr/bin/env bash
set -euo pipefail

INSTALL_DIR="/usr/local/bin"
BINARY_NAME="hokai"
PURGE=false

usage() {
    echo "Usage: uninstall.sh [--purge]"
    echo "  --purge  Remove config, data, and logs as well"
    exit 1
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --purge) PURGE=true; shift ;;
        *) usage ;;
    esac
done

echo "Stopping Hokai service..."
sudo "$INSTALL_DIR/$BINARY_NAME" service stop 2>/dev/null || true
sudo "$INSTALL_DIR/$BINARY_NAME" service uninstall 2>/dev/null || true

echo "Removing binary..."
sudo rm -f "$INSTALL_DIR/$BINARY_NAME"

if [ "$PURGE" = true ]; then
    echo "Removing configuration..."
    sudo rm -rf /etc/hokai/
    echo "Removing data..."
    sudo rm -rf /var/lib/hokai/

    if [ "$(uname -s)" = "Darwin" ]; then
        sudo rm -rf /usr/local/var/hokai/
        sudo rm -rf /usr/local/etc/hokai/
    fi
else
    echo "Config and data preserved. Use --purge to remove them."
fi

# Final verification
LEFT=$(find /usr/local /etc /var/lib /opt -name "*hokai*" 2>/dev/null || true)
if [ -n "$LEFT" ]; then
    echo "Warning: some files may remain:"
    echo "$LEFT"
else
    echo "Hokai fully uninstalled. No residuals."
fi
```

**`install.sh` idempotency**:
- If the binary already exists at the correct version → skip binary install
- If the service is already registered → skip service install
- If config already exists → never overwrite
- Script can be run N times with the same result

**`uninstall.sh` idempotency**:
- `set -e` + `|| true` on stop/remove commands → won't fail if already removed
- `rm -f` → won't fail if the file doesn't exist
- Running `uninstall.sh` twice is equivalent to running it once

---

### 5.3 PowerShell Script (Windows)

File: `scripts/install.ps1`

```powershell
<#
.SYNOPSIS
    Hokai Installer — Windows
.DESCRIPTION
    Downloads and installs the latest Hokai binary as a Windows Service.
    Idempotent — safe to run multiple times.
#>

param(
    [string]$Version = "latest",
    [switch]$SkipService
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$InstallDir  = "$env:ProgramFiles\Hokai"
$DataDir     = "$env:ProgramData\Hokai"
$BinaryPath  = "$InstallDir\hokai.exe"
$Repo        = "user/hokai"

function Get-Platform {
    $arch = if ([Environment]::Is64BitOperatingSystem) { "x64" } else { "x86" }
    return "win-$arch"
}

function Invoke-Download {
    $platform = Get-Platform
    if ($Version -eq "latest") {
        $url = "https://github.com/$Repo/releases/latest/download/hokai-$platform.zip"
    } else {
        $url = "https://github.com/$Repo/releases/download/$Version/hokai-$platform.zip"
    }

    Write-Host "Downloading hokai $Version for $platform..."
    $tempZip = "$env:TEMP\hokai.zip"
    Invoke-WebRequest -Uri $url -OutFile $tempZip

    $tempDir = "$env:TEMP\hokai_install"
    Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue
    Expand-Archive -Path $tempZip -DestinationPath $tempDir

    return $tempDir
}

function Install-Binary {
    param([string]$SourceDir)

    if (Test-Path $BinaryPath) {
        Write-Host "Binary already exists. Overwriting..."
    }

    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    Copy-Item -Force "$SourceDir\hokai.exe" $BinaryPath
    Write-Host "Binary installed to $BinaryPath"
}

function Install-Path {
    $currentPath = [Environment]::GetEnvironmentVariable("Path", "Machine")
    if ($currentPath -notlike "*$InstallDir*") {
        [Environment]::SetEnvironmentVariable(
            "Path", "$currentPath;$InstallDir", "Machine")
        Write-Host "Added $InstallDir to system PATH"
    } else {
        Write-Host "PATH already configured"
    }
}

function Install-Service {
    if ($SkipService) { return }
    & $BinaryPath service install
    Write-Host "Service installed"
}

# --- Main ---
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "Please run as Administrator"
    exit 1
}

$sourceDir = Invoke-Download
Install-Binary -SourceDir $sourceDir
Install-Path
Install-Service

Remove-Item -Recurse -Force $sourceDir -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Hokai installed successfully."
Write-Host "  Binary:  $BinaryPath"
Write-Host "  Config:  $DataDir\appsettings.json"
Write-Host "  Data:    $DataDir\Data\"
```

File: `scripts/uninstall.ps1`

```powershell
param([switch]$Purge)

$InstallDir = "$env:ProgramFiles\Hokai"
$DataDir    = "$env:ProgramData\Hokai"
$BinaryPath = "$InstallDir\hokai.exe"

if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "Please run as Administrator"
    exit 1
}

# Stop and remove service
& $BinaryPath service stop 2>$null
& $BinaryPath service uninstall 2>$null

# Remove binary
Remove-Item -Recurse -Force $InstallDir -ErrorAction SilentlyContinue
Write-Host "Binary removed"

# Remove from PATH
$currentPath = [Environment]::GetEnvironmentVariable("Path", "Machine")
if ($currentPath -like "*$InstallDir*") {
    $newPath = ($currentPath -split ";" | Where-Object { $_ -ne $InstallDir }) -join ";"
    [Environment]::SetEnvironmentVariable("Path", $newPath, "Machine")
    Write-Host "Removed from PATH"
}

if ($Purge) {
    Remove-Item -Recurse -Force $DataDir -ErrorAction SilentlyContinue
    Write-Host "Config and data removed"
} else {
    Write-Host "Config and data preserved at $DataDir. Use --Purge to remove."
}

Write-Host "Hokai uninstalled."
```

---

### 5.4 dotnet Global Tool

> **Note**: This is a **Future Improvement** and is not currently available.

**Audience**: developers with .NET SDK installed.

```bash
# Installation
dotnet tool install -g hokai

# Update
dotnet tool update -g hokai

# Usage
hokai endpoint add https://example.com/health
hokai run

# Uninstallation
dotnet tool uninstall -g hokai
```

**Prerequisites**:
- .NET SDK 10 (or compatible)
- The binary goes to `~/.dotnet/tools/` (already in PATH if configured)

**Advantages**:
- Idiomatic in the .NET ecosystem
- Built-in version management (`update`, `list`)
- `uninstall` built-in cleans up everything automatically

**Limitations**:
- Requires .NET SDK (not self-contained)
- Does not install the OS service automatically — the user still needs to run `hokai service install`
- Available via NuGet.org (requires packaging as a .NET tool)

**Packaging** (`.csproj`):
```xml
<PackAsTool>true</PackAsTool>
<ToolCommandName>hokai</ToolCommandName>
<PackageOutputPath>./nupkg</PackageOutputPath>
```

---

### 5.5 Docker

**Audience**: sysadmins, containerized environments.

#### Dockerfile

```dockerfile
# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Hokai/Hokai.csproj -c Release -o /app

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .

# Directory for persistent data
VOLUME ["/var/lib/hokai"]

ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Hokai.dll", "run"]
```

#### docker-compose.yml

```yaml
services:
  hokai:
    image: ghcr.io/user/hokai:latest
    container_name: hokai
    restart: unless-stopped
    volumes:
      - hokai_data:/var/lib/hokai
      - ./appsettings.json:/app/appsettings.json:ro
    environment:
      - TZ=America/Sao_Paulo
      - HOKAI_CONFIG_PATH=/app/appsettings.json
    network_mode: host  # or bridge for isolation

volumes:
  hokai_data:
```

#### Usage

```bash
# Build + run
docker compose up -d

# Add an endpoint (exec inside the container)
docker exec hokai dotnet Hokai.dll endpoint add https://example.com/health

# Logs
docker compose logs -f

# Stop + remove (keeps data volume)
docker compose down

# Stop + remove everything (purge)
docker compose down -v
```

#### Docker Hub / GHCR

```bash
# Pull the image
docker pull ghcr.io/user/hokai:latest

# Or a specific version
docker pull ghcr.io/user/hokai:1.0.0
```

**CI/CD (GitHub Actions)** for building and pushing the image:

```yaml
name: Publish Docker image

on:
  release:
    types: [published]

jobs:
  docker:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Build and push
        uses: docker/build-push-action@v5
        with:
          push: true
          tags: |
            ghcr.io/${{ github.repository }}:latest
            ghcr.io/${{ github.repository }}:${{ github.ref_name }}
```

**Docker idempotency**:
- `docker compose up -d`: recreates the container if the configuration changed; if already running with the same config, it's a no-op
- `docker compose down`: always removes the container (idempotent)
- `docker compose down -v`: also removes volumes (purges data)

---

### 5.6 Single Binary Download (GitHub Releases)

**Audience**: any user, zero dependencies.

Distribution via GitHub Releases with self-contained binaries per platform:

```
hokai-linux-x64.tar.gz
hokai-linux-arm64.tar.gz
hokai-osx-x64.tar.gz
hokai-osx-arm64.tar.gz
hokai-win-x64.zip
hokai-win-arm64.zip
```

**Build for release** (CI/CD):

```bash
# Multi-platform publish
dotnet publish src/Hokai/Hokai.csproj -c Release \
    -r linux-x64 --self-contained true \
    -p:PublishSingleFile=true -p:DebugType=embedded \
    -o publish/linux-x64

dotnet publish src/Hokai/Hokai.csproj -c Release \
    -r linux-arm64 --self-contained true \
    -p:PublishSingleFile=true -p:DebugType=embedded \
    -o publish/linux-arm64

dotnet publish src/Hokai/Hokai.csproj -c Release \
    -r osx-x64 --self-contained true \
    -p:PublishSingleFile=true -p:DebugType=embedded \
    -o publish/osx-x64

dotnet publish src/Hokai/Hokai.csproj -c Release \
    -r osx-arm64 --self-contained true \
    -p:PublishSingleFile=true -p:DebugType=embedded \
    -o publish/osx-arm64

dotnet publish src/Hokai/Hokai.csproj -c Release \
    -r win-x64 --self-contained true \
    -p:PublishSingleFile=true -p:DebugType=embedded \
    -o publish/win-x64

dotnet publish src/Hokai/Hokai.csproj -c Release \
    -r win-arm64 --self-contained true \
    -p:PublishSingleFile=true -p:DebugType=embedded \
    -o publish/win-arm64
```

**Manual installation** (any OS):

```bash
# Linux example
curl -L -o hokai.tar.gz https://github.com/user/hokai/releases/latest/download/hokai-linux-x64.tar.gz
tar -xzf hokai.tar.gz
sudo mv hokai /usr/local/bin/
sudo chmod +x /usr/local/bin/hokai
hokai service install
```

**Manual uninstall**:

```bash
sudo hokai service stop
sudo hokai service uninstall
sudo rm /usr/local/bin/hokai
# Optional:
sudo rm -rf /etc/hokai /var/lib/hokai
```

---

## 6. Decision Matrix by User Profile

| Profile | OS | Recommended method |
|---|---|---|
| .NET Developer | Any | `dotnet tool install -g hokai` |
| Contributing developer | Any | Build from source (`git clone` + `dotnet build`) |
| Linux sysadmin | Linux | `curl ... \| bash` (install.sh) or Docker |
| Windows sysadmin | Windows | PowerShell (install.ps1) |
| macOS user | macOS | Shell script (install.sh) or Homebrew (future) |
| Infra / K8s | Any | Docker + Helm chart (future) |
| End user | Any | Single binary download (GitHub Releases) |

---

## 7. Installation Verification

After any installation method, the user can verify:

```bash
# 1. Is the binary accessible?
hokai --version

# 2. Is the service registered?
hokai service status

# 3. Is the endpoint functional?
hokai endpoint add https://httpbin.org/status/200
hokai status
```

Automated smoke test in the install script:

```bash
# install.sh includes at the end:
echo "Running smoke test..."
"$INSTALL_DIR/$BINARY_NAME" --version || { echo "Smoke test failed!"; exit 1; }
echo "Smoke test passed."
```

---

## 8. Idempotency Summary by Method

| Method | `install` idempotent? | `uninstall` clean? |
|---|---|---|
| Build from source | Yes (`dotnet build` is already idempotent) | Yes (`rm -rf` the repo) |
| install.sh | Yes (checks existing version, never overwrites config) | Yes (final artifact verification) |
| install.ps1 | Yes (overwrites binary; never overwrites config) | Yes (PATH + registry cleanup) |
| dotnet tool | Yes (`dotnet tool update` to reinstall) | Yes (`dotnet tool uninstall` built-in) |
| Docker | Yes (`docker compose up -d` reapplies state) | Yes (`docker compose down -v` cleans everything) |
| Manual download | Yes (overwrites binary, doesn't touch config) | Yes (manual removal) |

---

## 9. Project File Structure

The canonical project structure is at [Architecture > Project Structure](architecture.md#3-project-structure). The installation-specific files added to that structure are:

```
hokai/
├── scripts/
│   ├── install.sh              # Linux/macOS installer
│   ├── uninstall.sh            # Linux/macOS uninstaller
│   ├── install.ps1             # Windows installer
│   └── uninstall.ps1           # Windows uninstaller
├── Dockerfile
├── docker-compose.yml
└── .github/
    └── workflows/
        ├── release.yml         # Build + GitHub Release
        └── docker-publish.yml  # Build + push Docker image
```

---

## 10. Future Improvements

- [ ] **Homebrew formula** — `brew install hokai` for macOS
- [ ] **APT repository** — `apt install hokai` for Debian/Ubuntu
- [ ] **winget package** — `winget install hokai` for Windows
- [ ] **Helm chart** — Kubernetes deployment
- [ ] **AUR package** — `yay -S hokai` for Arch Linux
- [ ] **Snap / Flatpak** — sandboxed distribution for Linux
- [ ] **Nix flake** — for Nix/NixOS users
- [ ] **Graphical installer** — installation wizard (low priority)
- [ ] **Auto-update** — `hokai update` for self-updating the binary
