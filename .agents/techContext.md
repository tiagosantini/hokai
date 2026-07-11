# Tech Context

## Runtime
- **.NET 10** (SDK 10.0.301, Runtime 10.0.9) — installed on dev machine
- Cross-platform: Linux (x64, arm64), macOS (x64, arm64), Windows (x64, arm64)

## Dependencies

### NuGet packages (3, all Microsoft)
| Package | Version | Purpose |
|---|---|---|
| `System.CommandLine` | 2.0.x | CLI parsing, subcommands, auto-help |
| `Microsoft.Extensions.Hosting.Systemd` | latest | systemd lifecycle, sd_notify, SIGTERM |
| `Microsoft.Extensions.Hosting.WindowsServices` | latest | Windows Service Control Manager |

### Built-in SDK namespaces (no NuGet)
| Namespace | Usage |
|---|---|
| `Microsoft.Extensions.Hosting` | Worker Service, DI, lifecycle |
| `Microsoft.Extensions.Http` | IHttpClientFactory, connection pooling |
| `Microsoft.Extensions.Configuration.Json` | appsettings.json reading |
| `Microsoft.Extensions.Logging.Console` | Console logging for daemon |
| `System.Net.Mail` | SmtpClient, MailMessage |
| `System.Net.Http` | HttpClient for health checks |
| `System.Text.Json` | JSON file persistence |
| `System.Threading` | PeriodicTimer for scheduling |
| `System.Diagnostics` | Stopwatch for response time measurement |

## Development setup
- `dotnet build` — compiles the solution
- `dotnet test` — runs xUnit tests (project planned)
- `dotnet run --project src/Hokai` — runs the app during development
- `dotnet publish -c Release -r <rid> --self-contained -p:PublishSingleFile=true` — produces single-file binary

## Test project
- `tests/Hokai.Tests/` with xUnit + `Microsoft.NET.Test.Sdk` + `coverlet.collector`
- No mocking framework — prefers fakes/stubs via DI container

## Technical constraints
- No third-party NuGet packages (only Microsoft)
- No database — file-based JSON persistence only
- No IPC — file system is the communication channel
- No GUI — CLI-only interface
- Single-binary publish target for distribution
