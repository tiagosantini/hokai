# Hokai

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com)
[![Build](https://img.shields.io/github/actions/workflow/status/username/hokai/ci.yml?branch=main)](https://github.com/username/hokai/actions)

**Uptime monitoring for your endpoints — from the terminal, as a background service.**

Hokai lets you monitor HTTP/HTTPS endpoints, track uptime percentage, and get
email alerts on downtime. Built with .NET 10 with minimal dependencies.

---

## Features

- **HTTP/HTTPS health checks** — configurable interval, timeout, method, and expected status code
- **Uptime % tracking** — 24-hour rolling window with historical data retention
- **Email notifications** — alerts on downtime and recovery via configurable SMTP
- **Runs as a native OS service** — systemd (Linux), launchd (macOS), Windows Service
- **Single portable binary** — self-contained publish, no runtime required
- **File-based storage** — JSON persistence, zero external databases
- **Minimal dependencies** — only 3 first-party NuGet packages from Microsoft

---

## Quick Start

### Linux / macOS

```bash
curl -fsSL https://raw.githubusercontent.com/username/hokai/main/scripts/install.sh | bash
```

### Windows (PowerShell as Administrator)

```powershell
Invoke-WebRequest -Uri "https://github.com/username/hokai/releases/latest/download/install.ps1" -OutFile "$env:TEMP\install.ps1"
& "$env:TEMP\install.ps1"
```

### Docker

```yaml
# docker-compose.yml
services:
  hokai:
    image: ghcr.io/username/hokai:latest
    container_name: hokai
    restart: unless-stopped
    volumes:
      - hokai_data:/var/lib/hokai
      - ./appsettings.json:/app/appsettings.json:ro
volumes:
  hokai_data:
```

```bash
docker compose up -d
```

### .NET Global Tool

```bash
dotnet tool install -g hokai
```

---

## Usage

### Managing endpoints

```bash
$ hokai endpoint add https://api.example.com/health --interval 5m --timeout 30s
Endpoint added: a1b2c3d4

$ hokai endpoint add https://app.example.com/ping --interval 1m
Endpoint added: e5f6g7h8

$ hokai endpoint list
┌────────────┬────────────────────────────────────┬──────────┬────────────┬───────────┐
│ ID         │ URL                                │ Interval │ Uptime 24h │ Status    │
├────────────┼────────────────────────────────────┼──────────┼────────────┼───────────┤
│ a1b2c3d4   │ https://api.example.com/health     │ 5m       │ 99.97%     │ UP (145ms)│
│ e5f6g7h8   │ https://app.example.com/ping       │ 1m       │ 100.00%    │ UP (89ms) │
└────────────┴────────────────────────────────────┴──────────┴────────────┴───────────┘

$ hokai endpoint remove a1b2c3d4
Endpoint removed: a1b2c3d4
```

### Running the daemon

```bash
# Foreground mode (useful for testing)
$ hokai run
[12:05:00 INF] MonitorService started — watching 2 endpoints
[12:05:00 INF] https://api.example.com/health — UP (145ms)
[12:05:00 INF] https://app.example.com/ping — UP (89ms)

# As a background service
$ hokai service start
Service started

$ hokai service status
Service: active (running)
Uptime (24h):
  https://api.example.com/health = 99.97%
  https://app.example.com/ping   = 100.00%
Data directory: /var/lib/hokai
```

### Service lifecycle

```bash
$ sudo hokai service install    # Install as systemd service
Service installed and enabled.

$ hokai service start            # Start the service
Service started.

$ hokai service stop             # Stop the service
Service stopped.

$ sudo hokai service uninstall   # Remove the service
Service uninstalled.
```

---

## Configuration

Configuration is read from an optional `appsettings.json`. When the file is absent,
Hokai uses the defaults shown below:

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
| `Smtp.FromAddress` | `hokai@localhost` | Sender email address |
| `Smtp.ToAddresses` | `[]` | Recipient email addresses |
| `DataDirectory` | `Data` | Where endpoint and check data is stored |
| `RetentionDays` | `30` | How long to keep individual check records |

**File locations by platform:**

| Platform | Config path | Data path |
|---|---|---|
| Linux | `/etc/hokai/appsettings.json` | `/var/lib/hokai/` |
| macOS | `/usr/local/etc/hokai/appsettings.json` | `/usr/local/var/hokai/` |
| Windows | `%ProgramData%\Hokai\appsettings.json` | `%ProgramData%\Hokai\Data\` |

---

## Documentation

| Document | Description |
|---|---|
| [Architecture](.docs/architecture.md) | Application design, data model, service architecture |
| [Daemonization](.docs/daemonization.md) | OS service integration (systemd, launchd, Windows Service) |
| [Installation](.docs/installation.md) | All install methods, idempotency, uninstall procedures |

---

## Contributing

Hokai is built with .NET 10 and requires the [.NET SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/username/hokai.git
cd hokai
dotnet restore
dotnet build -c Release
dotnet test
```

See [Architecture](.docs/architecture.md) for an overview of the codebase.

---

## License

[MIT](LICENSE)
