# Hokai

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com)
[![Build](https://img.shields.io/github/actions/workflow/status/tiagosantini/hokai/ci.yml?branch=dev)](https://github.com/tiagosantini/hokai/actions)

**Uptime monitoring for your endpoints — from the terminal, as a background service.**

Hokai lets you monitor HTTP/HTTPS endpoints, track uptime percentage, and get
email alerts on downtime. Built with .NET 10 with minimal dependencies.

> **Status**: Pre-release. Source builds are available now. Release binaries, Docker images, and installer scripts will be available with the first GitHub Release.

---

## Features

- **HTTP/HTTPS health checks** — configurable interval, timeout, method, and expected status code
- **Uptime % tracking** — 24-hour rolling window with historical data retention
- **Email notifications** — alerts on downtime and recovery via configurable SMTP
- **Runs as a native OS service** — systemd (Linux), launchd (macOS), Windows Service
- **Single portable binary** — self-contained publish, no runtime required
- **File-based storage** — JSON persistence, zero external databases
- **Minimal dependencies** — only 4 NuGet packages, all from Microsoft

---

## Quick Start (Build from Source)

```bash
git clone https://github.com/tiagosantini/hokai.git
cd hokai
dotnet restore hokai.slnx --locked-mode
dotnet build -c Release

# Run in foreground
dotnet run --project src/Hokai -- run

# Or publish and run directly
dotnet publish src/Hokai/Hokai.csproj -c Release -o ./hokai-bin
./hokai-bin/hokai run
```

### Docker (after first release)

```yaml
# compose.yml
services:
  hokai:
    image: ghcr.io/tiagosantini/hokai:latest
    container_name: hokai
    restart: unless-stopped
    volumes:
      - hokai_data:/var/lib/hokai
      - ./docker/appsettings.json:/etc/hokai/appsettings.json:ro
volumes:
  hokai_data:
```

```bash
docker compose up -d
docker exec hokai /app/hokai endpoint add https://example.com/health
```

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
$ sudo hokai service install         # Install as native OS service
Service installed successfully.

$ hokai service start                # Start the service
Service started successfully.

$ hokai service stop                 # Stop the service
Service stopped successfully.

$ hokai service status               # Check service state
active (active)

$ sudo hokai service uninstall       # Remove service (keeps config + data)
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
| Release | [EN](.docs/release.md) | [PT-BR](.docs/pt-BR/release.md) |

---

## Contributing

Hokai is built with .NET 10 and requires the [.NET SDK](https://dotnet.microsoft.com/download).

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
