# Hokai — Daemonização como Serviço do SO

> Plano de implementação para executar o Hokai como serviço nativo do sistema operacional (systemd, launchd, Windows Service), com comandos CLI para gerenciamento completo do ciclo de vida.

**Documentos relacionados**: [Arquitetura](architecture.md) (design principal) | [Instalação](installation.md) (instalar/desinstalar)

---

## 1. Decisões de Design (definidas)

| Decisão | Escolha |
|---|---|
| Integração com lifecycle do SO | **Pacotes Microsoft** — `Hosting.Systemd` + `Hosting.WindowsServices` |
| Escopo de comandos | **Completo** — `install`, `uninstall`, `start`, `stop`, `status` |
| Instalação do binário | **Cópia automática** — `install` copia o binário para local padrão do SO |

### Dependências atualizadas

| Pacote | Origem | Necessidade |
|---|---|---|
| `System.CommandLine` | NuGet | CLI |
| `Microsoft.Extensions.Hosting.Systemd` | NuGet | `sd_notify`, suporte a `Type=notify`, SIGTERM tratado automaticamente |
| `Microsoft.Extensions.Hosting.WindowsServices` | NuGet | Windows Service Control, `Start`, `Stop`, `Shutdown` events |

**Total: 3 NuGet (todos Microsoft).** Nenhuma dependência de terceiros. Para a lista completa de dependências do aplicativo principal, veja [Arquitetura > Dependências Detalhadas](architecture.md#9-dependências-detalhadas).

---

## 2. Comandos de Serviço

```
hokai service install   [--config <path>] [--data-dir <path>]
hokai service uninstall
hokai service start
hokai service stop
hokai service status
```

`hokai run` permanece inalterado para execução em foreground (dev/debug/manual).

---

## 3. Comportamento por Plataforma

### 3.1 Linux — systemd

| Etapa | O que acontece |
|---|---|
| `install` | 1. Solicita sudo se necessário. 2. Copia binário para `/usr/local/bin/hokai`. 3. Cria diretório de dados `/var/lib/hokai/`. 4. Gera unit file em `/etc/systemd/system/hokai.service`. 5. Executa `systemctl daemon-reload && systemctl enable hokai`. |
| `uninstall` | 1. `systemctl stop hokai && systemctl disable hokai`. 2. Remove `/etc/systemd/system/hokai.service`. 3. Remove `/usr/local/bin/hokai`. 4. Pergunta se quer remover diretório de dados. |
| `start` | `systemctl start hokai` |
| `stop` | `systemctl stop hokai` |
| `status` | `systemctl status hokai` + exibe uptime % dos endpoints |

**Template do unit file** (`/etc/systemd/system/hokai.service`):

```ini
[Unit]
Description=Hokai Uptime Monitor
Documentation=https://github.com/user/hokai
After=network-online.target
Wants=network-online.target

[Service]
Type=notify
ExecStart=/usr/local/bin/hokai run
WorkingDirectory=/etc/hokai
Restart=on-failure
RestartSec=10s
User=hokai
Group=hokai
LimitNOFILE=4096

# Segurança
NoNewPrivileges=yes
ProtectSystem=strict
ProtectHome=yes
ReadWritePaths=/var/lib/hokai /etc/hokai
ReadOnlyPaths=/usr/local/bin/hokai

[Install]
WantedBy=multi-user.target
```

- `Type=notify` — usa `sd_notify` via `Hosting.Systemd` para sinalizar quando o serviço está pronto
- `WorkingDirectory=/etc/hokai` — aponta para onde o `appsettings.json` reside
- `ProtectSystem=strict` + `ReadWritePaths` — hardening de segurança
- Criação do usuário `hokai` é responsabilidade do admin (ou do script `install` com flag `--create-user`)

### 3.2 macOS — launchd

| Etapa | O que acontece |
|---|---|
| `install` | 1. Copia binário para `/usr/local/bin/hokai`. 2. Cria diretório de dados `~/Library/Application Support/hokai/`. 3. Gera plist em `~/Library/LaunchAgents/com.hokai.daemon.plist`. 4. `launchctl load` + `launchctl start`. |
| `uninstall` | 1. `launchctl unload`. 2. Remove plist. 3. Remove binário. |
| `start` | `launchctl start com.hokai.daemon` |
| `stop` | `launchctl stop com.hokai.daemon` |
| `status` | `launchctl list com.hokai.daemon` |

Não requer sudo (LaunchAgent do usuário, não Daemon do sistema).

**Template plist** (`~/Library/LaunchAgents/com.hokai.daemon.plist`):

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.hokai.daemon</string>
    <key>ProgramArguments</key>
    <array>
        <string>/usr/local/bin/hokai</string>
        <string>run</string>
    </array>
    <key>WorkingDirectory</key>
    <string>/usr/local/var/hokai</string>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <dict>
        <key>SuccessfulExit</key>
        <false/>
    </dict>
    <key>ThrottleInterval</key>
    <integer>10</integer>
    <key>StandardOutPath</key>
    <string>/usr/local/var/log/hokai.log</string>
    <key>StandardErrorPath</key>
    <string>/usr/local/var/log/hokai.err</string>
    <key>EnvironmentVariables</key>
    <dict>
        <key>DOTNET_ENVIRONMENT</key>
        <string>Production</string>
    </dict>
</dict>
</plist>
```

### 3.3 Windows — Windows Service

| Etapa | O que acontece |
|---|---|
| `install` | 1. Solicita elevação de privilégio (admin). 2. Copia binário para `%ProgramFiles%\Hokai\hokai.exe`. 3. Cria diretório de dados `%ProgramData%\Hokai\`. 4. `sc.exe create Hokai binPath= "..." start= auto`. |
| `uninstall` | 1. `sc.exe stop Hokai`. 2. `sc.exe delete Hokai`. 3. Remove binário. |
| `start` | `sc.exe start Hokai` |
| `stop` | `sc.exe stop Hokai` |
| `status` | `sc.exe query Hokai` |

**Comando de instalação**:

```powershell
sc.exe create Hokai `
    binPath= "\"C:\Program Files\Hokai\hokai.exe\" run" `
    start= auto `
    DisplayName= "Hokai Uptime Monitor"
```

- `Hosting.WindowsServices` garante que o processo responda corretamente aos comandos `Start`, `Stop`, `Shutdown` do Service Control Manager
- O binário deve ser publicado como self-contained para evitar dependência de runtime instalado

---

## 4. Arquitetura Interna

### 4.1 Novos arquivos

Dois arquivos são adicionados à [estrutura canônica do projeto](architecture.md#3-estrutura-do-projeto):

```
src/Hokai/
├── Commands/
│   └── ServiceCommands.cs          # NOVO
└── Services/
    └── ServiceManager.cs           # NOVO
```

### 4.2 Program.cs (atualizado)

A detecção de ambiente de serviço acontece durante o bootstrap do host:

```csharp
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// --- Serviços de monitoramento ---
builder.Services.AddHostedService<MonitorService>();
builder.Services.AddSingleton<HealthCheckService>();
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddSingleton<EndpointStore>();
builder.Services.AddSingleton<CheckStore>();

// --- Integração com serviços do SO ---
if (OperatingSystem.IsLinux())
    builder.Services.AddHostedService<SystemdHostedService>();  // ou similar

IHost host = builder.Build();
await host.RunAsync();
```

> **Nota**: A API exata do `UseSystemd()` / `UseWindowsService()` depende do overload disponível no .NET 10. Pode ser via `Host.CreateDefaultBuilder(args).UseSystemd().UseWindowsService()` ao invés de `Host.CreateApplicationBuilder`. Detalhe de implementação a ser resolvido durante o build.

### 4.3 ServiceManager

Abstração sobre as ferramentas nativas de cada plataforma. Responsabilidades:

```
ServiceManager
├── InstallAsync(config)
│   ├── 1. DetectPlatform()
│   ├── 2. CopyBinaryAsync(targetPath)        # cópia automática
│   ├── 3. EnsureDataDirectoryAsync(dataDir)   # cria diretório de dados
│   ├── 4. GenerateDefinitionFileAsync()        # template específico da plataforma
│   ├── 5. WriteDefinitionFileAsync(path)       # escreve no local correto
│   └── 6. EnableServiceAsync()                 # systemctl enable / launchctl load / sc create
│
├── UninstallAsync()
│   ├── 1. DisableServiceAsync()                # systemctl disable / launchctl unload / sc delete
│   ├── 2. RemoveDefinitionFileAsync()
│   └── 3. RemoveBinaryAsync() + prompt remove data?
│
├── StartAsync()
├── StopAsync()
└── GetStatusAsync()
```

### 4.4 ServiceCommands

Integração com `System.CommandLine`:

```csharp
var serviceCommand = new Command("service", "Manage the Hokai background service");

var installCommand = new Command("install", "Install as an OS service");
installCommand.SetHandler(async (configPath, dataDir) => {
    var manager = new ServiceManager();
    await manager.InstallAsync(new ServiceConfig { ... });
});

serviceCommand.AddCommand(installCommand);
// ... uninstall, start, stop, status
```

### 4.5 Permissões e Elevação

| Plataforma | Comando | Requer Elevação? | Tratamento |
|---|---|---|---|
| Linux | `install` | Sim (sudo) | Detecta se não é root → reexecuta com `sudo` ou exibe mensagem de erro |
| Linux | `uninstall` | Sim | Mesmo tratamento |
| Linux | `start/stop/status` | Depende da policy do systemd (geralmente não) | Executa direto |
| macOS | `install/uninstall` | Não (LaunchAgent) | Executa direto |
| Windows | `install/uninstall` | Sim (admin) | Detecta → `RunAs` ou mensagem de erro |
| Windows | `start/stop/status` | Sim | Mesmo tratamento |

Estratégia de elevação:
1. Tenta executar comando diretamente
2. Se falhar com `PermissionDenied` / `AccessDenied`:
   - **Linux/macOS**: informa "Este comando requer privilégios administrativos. Execute com sudo."
   - **Windows**: informa "Este comando requer privilégios de administrador. Execute como administrador."

Alternativa mais conveniente (futura): detectar ambiente não-elevado e reexecutar automaticamente com `sudo` / `runas`.

---

## 5. Localização de Arquivos por Plataforma

As localizações de arquivos por plataforma estão documentadas em [Instalação > O Que é Instalado](installation.md#2-o-que-é-instalado). A tabela abaixo adiciona detalhes específicos do daemon:

| Arquivo | Linux | macOS | Windows |
|---|---|---|---|
| Logs | journald (integrado ao systemd) | `/usr/local/var/log/hokai.log` | Event Log (integrado ao Windows Service) |
| Definição do serviço | `/etc/systemd/system/hokai.service` | `~/Library/LaunchAgents/com.hokai.daemon.plist` | Registry (`sc.exe create`) |

---

## 6. Fluxo Completo do Usuário

### Linux

```bash
# Publicar o binário self-contained
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true

# Instalar como serviço
sudo hokai service install

# Verificar status
hokai service status
# Output:
#   Service: active (running)
#   Uptime (24h): https://api.example.com = 99.97% | https://app.example.com = 100%
#   Data directory: /var/lib/hokai

# Adicionar endpoint (funciona com ou sem serviço rodando)
hokai endpoint add https://new-api.example.com/health
# O daemon detecta o novo endpoint em até 30s automaticamente

# Parar / iniciar
hokai service stop
hokai service start

# Desinstalar
sudo hokai service uninstall
```

### macOS

```bash
# Mesmo fluxo, sem sudo (LaunchAgent)
hokai service install
hokai service start
```

### Windows (PowerShell como Admin)

```powershell
hokai service install
hokai service start
hokai service status
```

---

## 7. Decisões Pendentes

| Questão | Impacto | Sugestão |
|---|---|---|
| Criar usuário `hokai` no Linux automaticamente no `install`? | Segurança vs conveniência | Flag `--create-user`. Sem flag, usar `User=nobody` como fallback. |
| `uninstall` deve perguntar antes de remover diretório de dados? | Perda de dados acidental | Sim, prompt interativo com `--force` para skipar. |
| Suportar `hokai service logs` para tail de logs? | Conveniência | Sim, futuramente. Por ora `journalctl -u hokai` no Linux. |
| Suportar múltiplas instâncias do serviço (ex: `hokai@dev`, `hokai@prod`)? | Escopo grande | Fora do escopo inicial. Single-instance apenas. |
| O daemon deve expor um health check HTTP próprio (ex: `http://localhost:9090/health`)? | Monitoramento do monitor | Fora do escopo inicial. Listado como melhoria futura. |

---

## 8. Implementação — Ordem Sugerida

1. **ServiceManager** — abstração de plataforma com os 3 backends (Linux, macOS, Windows)
2. **ServiceCommands** — integração com System.CommandLine
3. **Program.cs update** — adicionar `builder.UseSystemd()` / `builder.UseWindowsService()`
4. **Templates** — unit files / plist / sc.exe scripts como embedded resources ou strings
5. **Testes manuais** — validar `install → start → status → stop → uninstall` em cada plataforma
6. **Elevação** — tratamento de permissões e mensagens de erro
