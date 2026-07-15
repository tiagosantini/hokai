# Hokai

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com)
[![Build](https://img.shields.io/github/actions/workflow/status/tiagosantini/hokai/ci.yml?branch=dev)](https://github.com/tiagosantini/hokai/actions)

**Uptime monitoring for your endpoints — from the terminal, as a background service.**

Hokai lets you monitor HTTP/HTTPS endpoints, track uptime percentage, and get
email alerts on downtime. Built with .NET 10 with minimal dependencies.

> **Status**: Pre-release (`v0.2.0-alpha.1`, NativeAOT). Published as a draft release on the milestone. Self-contained native executables for six platforms, Docker images on GHCR, and installer scripts are available. Suitable for testing and feedback. Not yet recommended for production.

---

## Quick Start

### Linux (systemd)

```bash
curl -fsSL https://github.com/tiagosantini/hokai/releases/download/v0.2.0-alpha.1/install.sh | sudo bash
newgrp hokai
hokai endpoint add https://example.com/health --interval 30s --timeout 10s
hokai status
sudo hokai service start
journalctl -u hokai -f
```

### macOS (launchd)

```bash
curl -fsSL https://github.com/tiagosantini/hokai/releases/download/v0.2.0-alpha.1/install.sh | bash
hokai endpoint add https://example.com/health --interval 30s --timeout 10s
hokai status
hokai service start
tail -f ~/Library/Logs/Hokai/daemon.log
```

### Windows (PowerShell as Administrator)

```powershell
irm https://github.com/tiagosantini/hokai/releases/download/v0.2.0-alpha.1/install.ps1 | iex
hokai endpoint add https://example.com/health --interval 30s --timeout 10s
```

### Docker

```bash
docker run -d \
  --name hokai \
  --restart unless-stopped \
  -v hokai-data:/var/lib/hokai \
  -v ./docker/appsettings.json:/etc/hokai/appsettings.json:ro \
  ghcr.io/tiagosantini/hokai:0.2.0-alpha.1

docker exec hokai hokai endpoint add https://example.com/health --interval 30s
docker exec hokai hokai status
```

### Build from Source

```bash
git clone https://github.com/tiagosantini/hokai.git
cd hokai
dotnet restore hokai.slnx --locked-mode
dotnet build -c Release
dotnet run --project src/Hokai -- run
```

---

## Features

- **HTTP/HTTPS health checks** — configurable interval, timeout, method, and expected status code
- **Uptime % tracking** — 24-hour rolling window with O(C) single-pass grouping
- **Email notifications** — alerts on state transitions (UP → DOWN, DOWN → UP) via configurable SMTP
- **NativeAOT compilation** — ~9.4 MiB binaries with 20 ms cold startup (87% smaller, 89% faster than JIT)
- **Runs as a native OS service** — systemd (Linux), launchd (macOS), Windows Service
- **Single portable binary** — self-contained native executable, no runtime required
- **File-based storage** — JSON persistence with rc.2 backward compatibility, zero external databases
- **Minimal dependencies** — only 4 NuGet packages, all from Microsoft

---

## Usage

### Managing endpoints

```bash
$ hokai endpoint add https://example.com/health --interval 5m --timeout 30s
Endpoint a1b2c3d4 added.

$ hokai endpoint list
ID        URL                                               INTERVAL  TIMEOUT  METHOD  EXPECT  UPTIME
a1b2c3d4  https://example.com/health                        00:05:00  00:00:30 GET     200     0.0%

$ hokai endpoint remove a1b2c3d4
Endpoint a1b2c3d4 removed.
```

### Checking endpoint status

```bash
$ hokai status
ID        URL                                               LAST CHECK           STATUS  CODE  RT(ms)  UPTIME
a1b2c3d4  https://example.com/health                        2026-07-13 15:00:00  UP      200   145     99.9%
```

### Service lifecycle

```bash
$ sudo hokai service install            # Register as native OS service
Service installed successfully.

$ hokai service start                   # Start the service
Service started successfully.

$ hokai service stop                    # Stop the service
Service stopped successfully.

$ hokai service status                  # Check service state
active (active)

$ sudo hokai service uninstall          # Remove service (keeps config + data)
$ sudo hokai service uninstall --purge  # Remove service, config, and data
```

---

## Configuration

Configuration is read from `appsettings.json`. When no config file is found, Hokai uses the defaults shown below.

```json
{
  "Smtp": {
    "Host": "localhost",
    "Port": 25,
    "UseSsl": false,
    "Username": "",
    "Password": "",
    "FromAddress": "hokai@localhost",
    "ToAddresses": []
  },
  "DataDirectory": "Data",
  "RetentionDays": 30
}
```

| Key | Default | Description |
|---|---|---|
| `Smtp.Host` | `localhost` | SMTP server hostname |
| `Smtp.Port` | `25` | SMTP server port |
| `Smtp.UseSsl` | `false` | Enable SSL/TLS |
| `Smtp.Username` | `""` | SMTP authentication username |
| `Smtp.Password` | `""` | SMTP authentication password |
| `Smtp.FromAddress` | `hokai@localhost` | Sender email address |
| `Smtp.ToAddresses` | `[]` | Recipient email addresses |
| `DataDirectory` | `Data` | Relative to config file; stores `endpoints.json` and `checks.json` |
| `RetentionDays` | `30` | Days to keep individual check records |

**Config resolution order:**

1. `--config /path` (or `-c /path`) CLI argument
2. `HOKAI_CONFIG_PATH` environment variable
3. Canonical OS config (e.g. `/etc/hokai/appsettings.json` on Linux)
4. `appsettings.json` next to the executable

Any `appsettings.json` value can be overridden via environment variables
with the `HOKAI_` prefix, e.g. `HOKAI_RETENTIONDAYS=45` or
`HOKAI_SMTP__HOST=mail.example.com`. Environment overrides work even when
no config file is present.

**File locations by platform:**

| Platform | Config path | Data path |
|---|---|---|
| Linux | `/etc/hokai/appsettings.json` | `/var/lib/hokai/` |
| macOS | `~/Library/Application Support/Hokai/appsettings.json` | `~/Library/Application Support/Hokai/Data/` |
| Windows | `%ProgramData%\Hokai\appsettings.json` | `%ProgramData%\Hokai\Data\` |

For the full configuration reference, see [Configuration](.docs/configuration.md).

---

## Documentation

| Document | EN | PT-BR |
|---|---|---|
| Architecture | [EN](.docs/architecture.md) | [PT-BR](.docs/pt-BR/architecture.md) |
| Daemonization | [EN](.docs/daemonization.md) | [PT-BR](.docs/pt-BR/daemonization.md) |
| Installation | [EN](.docs/installation.md) | [PT-BR](.docs/pt-BR/installation.md) |
| Configuration | [EN](.docs/configuration.md) | [PT-BR](.docs/pt-BR/configuration.md) |
| NativeAOT | [EN](.docs/native-aot.md) | [PT-BR](.docs/pt-BR/native-aot.md) |
| Performance | [EN](.docs/performance.md) | [PT-BR](.docs/pt-BR/performance.md) |
| Release | [EN](.docs/release.md) | [PT-BR](.docs/pt-BR/release.md) |

---

## Contributing

```bash
git clone https://github.com/tiagosantini/hokai.git
cd hokai
dotnet restore hokai.slnx --locked-mode
dotnet build -c Release
dotnet test
```

See [Architecture](.docs/architecture.md) for an overview of the codebase and [AGENTS.md](AGENTS.md) for contribution conventions.

---

## License

[MIT](LICENSE)
