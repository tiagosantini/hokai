# Hokai — Instalação e Desinstalação

> Plano de distribuição e ciclo de vida de instalação do Hokai. Todas as operações são **idempotentes** e a desinstalação **não deixa artefatos residuais**.

**Documentos relacionados**: [Arquitetura](architecture.md) (design principal) | [Daemonização](daemonization.md) (ciclo de vida do serviço)

---

## 1. Métodos de Instalação

| Método | Público-alvo | Dependência prévia | Complexidade |
|---|---|---|---|
| [5.1 Build from Source](#51-build-from-source) | Desenvolvedores | .NET SDK 10 | Baixa |
| [5.2 Shell Script](#52-shell-script-linuxmacos) | Linux / macOS | bash, curl/wget, tar | Baixa |
| [5.3 PowerShell Script](#53-powershell-script-windows) | Windows | PowerShell 5+ | Baixa |
| [5.4 Single Binary Download](#54-single-binary-download-github-releases) | Qualquer SO | Nenhuma (self-contained) | Mínima |
| [5.5 Docker](#55-docker) | Sysadmins, infra | Docker / Podman | Baixa |

---

## 2. O Que é Instalado

Independentemente do método, uma instalação completa produz os artefatos descritos em [Daemonização > Localização de Arquivos](daemonization.md#5-localização-de-arquivos-por-plataforma). A tabela abaixo foca nos caminhos relevantes para o processo de instalação:

| Artefato | Linux | macOS | Windows |
|---|---|---|---|
| Diretório de binários | `/usr/local/bin/` (já no PATH) | `/usr/local/bin/` (já no PATH) | `%ProgramFiles%\Hokai\` (adicionado ao PATH) |
| Dados de trabalho | `/var/lib/hokai/` | `~/Library/Application Support/Hokai/` | `%ProgramData%\Hokai\Data\` |

> **Nota**: O binário é posicionado pelos scripts de instalação, não pelo comando `service install`. `service install` gerencia apenas o registro do serviço no SO, diretórios de config/dados e arquivos de definição. Veja [Daemonização > Decisões de Design](daemonization.md#1-decisões-de-design-definidas).

---

## 3. Princípios de Idempotência

Toda operação de instalação segue estas regras:

| Regra | Comportamento |
|---|---|
| **Binário** | Se já existe e a versão é igual → skip. Se versão diferente → sobrescreve com backup opcional. |
| **Config** | Se `appsettings.json` já existe → **nunca sobrescreve**. Apenas cria se ausente. |
| **Diretório de dados** | Cria se não existe. Nunca remove dados existentes. |
| **Serviço** | Se já está registrado no SO → verifica definição. Se diferente → recria. Se igual → skip. |
| **PATH** | Se binário já está acessível via PATH → skip. Se não → adiciona. |
| **Resultado** | Duas execuções consecutivas de `install` produzem exatamente o mesmo estado final. |

---

## 4. Princípios de Desinstalação Limpa

| Regra | Comportamento |
|---|---|
| **Serviço rodando** | `stop` antes de remover a definição |
| **Definição do serviço** | Remove via ferramenta nativa (`systemctl disable`, `launchctl unload`, `sc delete`) |
| **Binário** | Remove do local de instalação |
| **PATH** | Remove entrada adicionada durante `install` |
| **Config** | Removida apenas com `--purge`. Nunca removida caso contrário. |
| **Dados**  | Removidos apenas com `--purge`. Nunca removidos caso contrário. |
| **Logs** | Remove se aplicável (macOS). Linux usa journald (não há arquivo). |
| **Verificação final** | Confirma que nenhum artefato do Hokai permanece no sistema |

### Comportamento de `--purge`

```
hokai service uninstall           # mantém config + dados
hokai service uninstall --purge   # remove config + dados
```

Sem `--purge`, apenas o registro do serviço é removido. O binário é gerenciado pelos scripts de instalação; `service uninstall --purge` remove apenas config e dados.

---

## 5. Métodos em Detalhe

### 5.1 Build from Source

**Público**: desenvolvedores e contribuidores.

```bash
git clone https://github.com/user/hokai.git
cd hokai
dotnet restore
dotnet build -c Release
dotnet run --project src/Hokai -- endpoint add https://example.com/health
```

**Instalação como serviço (após build)**:
```bash
dotnet publish -c Release -o ./publish
sudo ./publish/hokai service install
```

**Desinstalação**:
```bash
sudo hokai service uninstall --purge
rm -rf ./hokai/   # remove o repositório clonado
```

**Idempotência**: `git clone` falha se diretório já existe (esperado). `git pull` no repositório existente atualiza.

---

### 5.2 Shell Script (Linux/macOS)

Arquivo: `scripts/install.sh`

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

# --- Detecção de plataforma ---
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

# --- Download do binário ---
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

# --- Instalação ---
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

# --- Configuração do serviço ---
install_service() {
    if [ "$(uname -s)" = "Linux" ]; then
        sudo "$INSTALL_DIR/$BINARY_NAME" service install
    elif [ "$(uname -s)" = "Darwin" ]; then
        "$INSTALL_DIR/$BINARY_NAME" service install
    fi
}

# --- Execução ---
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

Arquivo: `scripts/uninstall.sh`

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

# Verificação final
LEFT=$(find /usr/local /etc /var/lib /opt -name "*hokai*" 2>/dev/null || true)
if [ -n "$LEFT" ]; then
    echo "Warning: some files may remain:"
    echo "$LEFT"
else
    echo "Hokai fully uninstalled. No residuals."
fi
```

**Idempotência do `install.sh`**:
- Se binário já existe na versão correta → skip binary install
- Se serviço já está registrado → skip service install
- Se config já existe → nunca sobrescreve
- Script pode ser executado N vezes com o mesmo resultado

**Idempotência do `uninstall.sh`**:
- `set -e` + `|| true` nos comandos de parada/remoção → não falha se já removido
- `rm -f` → não falha se arquivo não existe
- Executar 2x `uninstall.sh` é equivalente a executar 1x

---

### 5.3 PowerShell Script (Windows)

Arquivo: `scripts/install.ps1`

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

Arquivo: `scripts/uninstall.ps1`

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
& $BinaryPath service stop -ErrorAction SilentlyContinue
& $BinaryPath service uninstall -ErrorAction SilentlyContinue

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

> **(Melhoria Futura)** — Esta funcionalidade ainda não está disponível.

**Público**: desenvolvedores com .NET SDK instalado.

```bash
# Instalação
dotnet tool install -g hokai

# Atualização
dotnet tool update -g hokai

# Uso
hokai endpoint add https://example.com/health
hokai run

# Desinstalação
dotnet tool uninstall -g hokai
```

**Pré-requisitos**:
- .NET SDK 10 (ou compatível)
- O binário vai para `~/.dotnet/tools/` (já no PATH se configurado)

**Vantagens**:
- Idiomático no ecossistema .NET
- Gerenciamento de versão built-in (`update`, `list`)
- `uninstall` built-in limpa tudo automaticamente

**Limitações**:
- Requer .NET SDK (não self-contained)
- Não instala o serviço do OS automaticamente — usuário ainda precisa rodar `hokai service install`
- Disponível via NuGet.org (requer empacotamento como ferramenta .NET)

**Packaging** (`.csproj`):
```xml
<PackAsTool>true</PackAsTool>
<ToolCommandName>hokai</ToolCommandName>
<PackageOutputPath>./nupkg</PackageOutputPath>
```

---

### 5.5 Docker

**Público**: sysadmins, ambientes containerizados.

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

# Diretório para dados persistentes
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
    network_mode: host  # ou bridge para isolamento

volumes:
  hokai_data:
```

#### Uso

```bash
# Build + run
docker compose up -d

# Adicionar endpoint (exec dentro do container)
docker exec hokai dotnet Hokai.dll endpoint add https://example.com/health

# Logs
docker compose logs -f

# Stop + remove (mantém volume de dados)
docker compose down

# Stop + remove tudo (purge)
docker compose down -v
```

#### Docker Hub / GHCR

```bash
# Pull da imagem
docker pull ghcr.io/user/hokai:latest

# Ou versão específica
docker pull ghcr.io/user/hokai:1.0.0
```

**CI/CD (GitHub Actions)** para build e push da imagem:

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

**Idempotência Docker**:
- `docker compose up -d`: recria container se configuração mudou; se já está rodando com a mesma config, é no-op
- `docker compose down`: sempre remove o container (idempotente)
- `docker compose down -v`: remove também volumes (purga dados)

---

### 5.6 Single Binary Download (GitHub Releases)

**Público**: qualquer usuário, zero dependências.

Distribuição via GitHub Releases com binários self-contained por plataforma:

```
hokai-linux-x64.tar.gz
hokai-linux-arm64.tar.gz
hokai-osx-x64.tar.gz
hokai-osx-arm64.tar.gz
hokai-win-x64.zip
hokai-win-arm64.zip
```

**Build para release** (CI/CD):

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

**Instalação manual** (qualquer SO):

```bash
# Exemplo Linux
curl -L -o hokai.tar.gz https://github.com/user/hokai/releases/latest/download/hokai-linux-x64.tar.gz
tar -xzf hokai.tar.gz
sudo mv hokai /usr/local/bin/
sudo chmod +x /usr/local/bin/hokai
hokai service install
```

**Desinstalação manual**:

```bash
sudo hokai service stop
sudo hokai service uninstall
sudo rm /usr/local/bin/hokai
# Opcional:
sudo rm -rf /etc/hokai /var/lib/hokai
```

---

## 6. Matriz de Decisão por Perfil de Usuário

| Perfil | SO | Método recomendado |
|---|---|---|
| Desenvolvedor .NET | Qualquer | `dotnet tool install -g hokai` |
| Desenvolvedor contribuidor | Qualquer | Build from source (`git clone` + `dotnet build`) |
| Sysadmin Linux | Linux | `curl ... \| bash` (install.sh) ou Docker |
| Sysadmin Windows | Windows | PowerShell (install.ps1) |
| Usuário macOS | macOS | Shell script (install.sh) ou Homebrew (futuro) |
| Infra / K8s | Qualquer | Docker + Helm chart (futuro) |
| Usuário final | Qualquer | Single binary download (GitHub Releases) |

---

## 7. Verificação de Instalação

Após qualquer método de instalação, o usuário pode verificar:

```bash
# 1. Binário está acessível?
hokai --version

# 2. Serviço está registrado?
hokai service status

# 3. Endpoint funcional?
hokai endpoint add https://httpbin.org/status/200
hokai status
```

Um smoke test automatizado no script de instalação:

```bash
# install.sh inclui ao final:
echo "Running smoke test..."
"$INSTALL_DIR/$BINARY_NAME" --version || { echo "Smoke test failed!"; exit 1; }
echo "Smoke test passed."
```

---

## 8. Resumo de Idempotência por Método

| Método | `install` idempotente? | `uninstall` limpo? |
|---|---|---|
| Build from source | Sim (`dotnet build` já é idempotente) | Sim (`rm -rf` do repo) |
| install.sh | Sim (verifica versão existente, não sobrescreve config) | Sim (verificação final de artefatos) |
| install.ps1 | Sim (verifica existência, não sobrescreve config) | Sim (limpeza de PATH + registry) |
| dotnet tool | Sim (`dotnet tool update` para reinstalar) | Sim (`dotnet tool uninstall` built-in) |
| Docker | Sim (`docker compose up -d` reaplica estado) | Sim (`docker compose down -v` limpa tudo) |
| Download manual | Sim (sobrescreve binário, não toca em config) | Sim (remoção manual) |

---

## 9. Estrutura do Projeto

A estrutura canônica do projeto está em [Arquitetura > Estrutura do Projeto](architecture.md#3-estrutura-do-projeto). Os arquivos específicos de instalação adicionados a essa estrutura são:

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

## 10. Melhorias Futuras

- [ ] **Homebrew formula** — `brew install hokai` para macOS
- [ ] **APT repository** — `apt install hokai` para Debian/Ubuntu
- [ ] **winget package** — `winget install hokai` para Windows
- [ ] **Helm chart** — deployment em Kubernetes
- [ ] **AUR package** — `yay -S hokai` para Arch Linux
- [ ] **Snap / Flatpak** — distribuição sandboxed para Linux
- [ ] **Nix flake** — para usuários Nix/NixOS
- [ ] **Instalador gráfico** — wizard de instalação (futuro distante, baixa prioridade)
- [ ] **Auto-update** — `hokai update` para self-update do binário
