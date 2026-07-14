# Hokai — Configuration Reference

> Complete reference for Hokai configuration, resolution order, and platform paths.

**Related docs**: [Architecture](architecture.md) | [Installation](installation.md) | [Daemonization](daemonization.md)

---

## 1. Schema

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

| Key | Type | Default | Description |
|---|---|---|---|
| `Smtp.Host` | string | `localhost` | SMTP server hostname |
| `Smtp.Port` | int | `25` | SMTP server port |
| `Smtp.UseSsl` | bool | `false` | Enable SSL/TLS |
| `Smtp.Username` | string | `""` | SMTP authentication username (blank = no auth) |
| `Smtp.Password` | string | `""` | SMTP authentication password |
| `Smtp.FromAddress` | string | `hokai@localhost` | Sender email address |
| `Smtp.ToAddresses` | string[] | `[]` | Recipient email addresses |
| `DataDirectory` | string | `Data` | Where endpoint and check data is stored |
| `RetentionDays` | int | `30` | Days to keep individual check records |

## 2. Config File Resolution

Hokai resolves the configuration file using the following priority:

| Priority | Source | Description |
|---|---|---|
| 1 | `--config /path` (or `-c /path`) | Explicit CLI argument. Error if file does not exist. |
| 2 | `HOKAI_CONFIG_PATH` | Environment variable. Error if file does not exist. |
| 3 | Canonical OS config | Platform-specific config location if present on disk. |
| 4 | Executable-adjacent | `appsettings.json` in the same directory as the executable. |

If no config file is found through any tier, Hokai uses in-memory defaults with `DataDirectory = "Data"` (relative to the working directory).

### Canonical Paths by Platform

| Platform | Config path | Data directory |
|---|---|---|
| Linux | `/etc/hokai/appsettings.json` | `/var/lib/hokai/` |
| macOS | `~/Library/Application Support/Hokai/appsettings.json` | `~/Library/Application Support/Hokai/Data/` |
| Windows | `%ProgramData%\Hokai\appsettings.json` | `%ProgramData%\Hokai\Data\` |

## 3. DataDirectory Semantics

- **Relative path**: resolved relative to the config file's directory (not the working directory).
- **Absolute path**: used as-is.
- **Default**: when no config file exists, `Data` resolves relative to the process working directory.
- The directory stores two JSON files: `endpoints.json` (endpoint configuration) and `checks.json` (check history).
- The directory is created automatically on first write.
- Retention cleanup runs every hour in daemon mode.

## 4. SMTP Configuration

- Notifications are sent only on state transitions (UP → DOWN, DOWN → UP). The first check for each endpoint does not trigger a notification.
- If `ToAddresses` is empty, no emails are sent.
- If `Username` is empty, SMTP authentication is skipped.
- One `SmtpClient` is created per message; failures are logged without retry.
- **Security**: Store credentials in environment variables or .NET User Secrets, not in the config file. The `appsettings.json` should contain only placeholder values in committed repositories.

## 5. Configuration Reloading

- The config file is read once at startup. Changes to SMTP settings or `RetentionDays` require a process restart.
- Endpoint configuration (`endpoints.json`) is reloaded every 30 seconds by the daemon.
- Adding or removing endpoints via CLI takes effect automatically within 30 seconds.

## 6. Docker Configuration

When running in Docker, provide configuration via a bind-mount:

```bash
docker run -v ./my-config.json:/etc/hokai/appsettings.json:ro \
           -v hokai-data:/var/lib/hokai \
           ghcr.io/tiagosantini/hokai:latest
```

Set `DataDirectory` to an absolute path within the volume:

```json
{
  "DataDirectory": "/var/lib/hokai",
  "Smtp": { "Host": "smtp.example.com", "...": "..." },
  "RetentionDays": 30
}
```

The default `docker/appsettings.json` template is pre-configured for this setup.

## 7. Missing or Invalid Config

| Scenario | Behavior |
|---|---|
| Config file is valid JSON | Settings are bound and validated at point of use. |
| Config file is missing | In-memory defaults are used. No error. |
| Config file is malformed JSON | Exception is thrown on load. Application exits with error. |
| Explicit `--config` points to missing file | Application exits with error. |
| `HOKAI_CONFIG_PATH` points to missing file | Application exits with error. |
| Unknown JSON keys | Ignored by configuration binding. |

---

## 8. Future Improvements

- [ ] Environment variable binding for individual keys (e.g. `HOKAI_SMTP__HOST`)
- [ ] Configuration hot-reload for SMTP and retention settings
- [ ] Encrypted SMTP password storage
- [ ] Configuration validation on startup
