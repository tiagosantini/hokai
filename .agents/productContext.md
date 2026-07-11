# Product Context

## Why this project exists
- CLI-first uptime monitoring tool that runs as a background service
- Lightweight alternative to hosted monitoring services (Pingdom, UptimeRobot) for local/private infrastructure
- Portable across Linux, macOS, and Windows with a single binary

## Problems it solves
- Monitor internal HTTP/HTTPS endpoints not exposed to the internet
- Track uptime percentage over a rolling 24-hour window
- Get email notifications on downtime and recovery without third-party services
- Minimal footprint — no database, no external dependencies, no IPC between CLI and daemon

## User personas
- **Developer**: wants to monitor staging/development endpoints, gets alerts on broken deploys
- **Sysadmin**: monitors production endpoints, manages via CLI scripts, integrates with systemd/launchd
- **DevOps engineer**: runs in Docker, monitors internal APIs, uses file-based config for automation

## Key differentiators
- Zero third-party NuGet dependencies — only Microsoft packages
- File-based persistence (JSON) — no database setup
- No IPC — CLI and daemon communicate through the file system
- OS-native service integration (systemd, launchd, Windows Service)
