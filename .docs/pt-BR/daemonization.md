# Hokai — Daemonização como Serviço do SO

> Plano de implementação para executar o Hokai como serviço nativo do sistema operacional (systemd, launchd, Windows Service), com comandos CLI para gerenciamento completo do ciclo de vida.

**Documentos relacionados**: [Arquitetura](architecture.md) (design principal) | [Instalação](installation.md) (instalar/desinstalar)

---

## 1. Decisões de Design (definidas)

| Decisão | Escolha |
|---|---|
| Integração com lifecycle do SO | **Pacotes Microsoft** — `Hosting.Systemd` + `Hosting.WindowsServices` |
| Escopo de comandos | **Completo** — `install`, `uninstall`, `start`, `stop`, `status` |
| Instalação do binário | **Externa** — scripts instaladores posicionam o executável; `service install` apenas registra o serviço |

### Dependências atualizadas

| Pacote | Origem | Necessidade |
|---|---|---|
| `System.CommandLine` | NuGet | Parsing CLI |
| `Microsoft.Extensions.Http` | NuGet | `IHttpClientFactory`, pooling de conexões |
| `Microsoft.Extensions.Hosting.Systemd` | NuGet | `sd_notify`, suporte a `Type=notify`, SIGTERM tratado automaticamente |
| `Microsoft.Extensions.Hosting.WindowsServices` | NuGet | Windows Service Control, eventos `Start`, `Stop`, `Shutdown` |

**Total: 4 pacotes NuGet (todos Microsoft).** Nenhuma dependência de terceiros.

---

## 2. Comandos de Serviço

```
hokai service install
hokai service uninstall [--purge]
hokai service start
hokai service stop
hokai service status
```

`hokai run` permanece inalterado para execução em foreground (dev/debug/manual).

`service uninstall --purge` remove o registro do serviço, a configuração e o diretório de dados. Sem `--purge`, apenas o registro do serviço é removido; config e dados são preservados para reinstalação futura.

---

## 3. Comportamento por Plataforma

### 3.1 Linux — systemd

| Etapa | O que acontece |
|---|---|
| `install` | 1. Solicita sudo se necessário. 2. Cria grupo de sistema `hokai` e usuário de sistema `hokai` de forma idempotente. 3. Adiciona o usuário sudo que invocou ao grupo `hokai` quando aplicável. 4. Cria diretório de dados `/var/lib/hokai/` com posse do grupo e `g+rw`. 5. Cria diretório de config `/etc/hokai/` com posse do grupo e `g+rw`. 6. Escreve config padrão apenas se ausente. 7. Gera unit file em `/etc/systemd/system/hokai.service`. 8. Executa `systemctl daemon-reload && systemctl enable hokai`. |
| `uninstall` | 1. `systemctl stop hokai && systemctl disable hokai`. 2. Remove `/etc/systemd/system/hokai.service`. 3. Com `--purge`: remove `/etc/hokai/` e `/var/lib/hokai/`. |
| `start` | `systemctl start hokai` |
| `stop` | `systemctl stop hokai` |
| `status` | `systemctl is-active hokai` → mapeia para rótulo |

**Template do unit file** (`/etc/systemd/system/hokai.service`):

```ini
[Unit]
Description=Hokai Uptime Monitor
Documentation=https://github.com/tiagosantini/hokai
After=network-online.target
Wants=network-online.target

[Service]
Type=notify
ExecStart=/usr/local/bin/hokai --config /etc/hokai/appsettings.json run
WorkingDirectory=/etc/hokai
User=hokai
Group=hokai
UMask=0002
Restart=on-failure
RestartSec=10s
LimitNOFILE=4096

# Segurança
NoNewPrivileges=yes
ProtectSystem=strict
ProtectHome=yes
ReadWritePaths=/var/lib/hokai
ReadOnlyPaths=/etc/hokai/appsettings.json

[Install]
WantedBy=multi-user.target
```
O usuário e grupo de sistema `hokai`, junto com as permissões de arquivo, são criados durante o `service install`.

### 3.2 macOS — launchd

| Etapa | O que acontece |
|---|---|
| `install` | 1. Cria diretório de config `~/Library/Application Support/Hokai/`. 2. Cria diretório de dados `~/Library/Application Support/Hokai/Data/`. 3. Escreve config padrão apenas se ausente. 4. Gera plist em `~/Library/LaunchAgents/com.hokai.daemon.plist`. Não inicia. |
| `uninstall` | 1. `launchctl bootout gui/$UID/com.hokai.daemon` se carregado. 2. Remove plist. 3. Com `--purge`: remove diretórios de config e dados. |
| `start` | `launchctl bootstrap gui/$UID` se não carregado, então `launchctl kickstart gui/$UID/com.hokai.daemon` |
| `stop` | `launchctl bootout gui/$UID/com.hokai.daemon` |
| `status` | `launchctl print gui/$UID/com.hokai.daemon` → mapeia para rótulo |

Não requer sudo (LaunchAgent do usuário). Config, dados e definições ficam no diretório Library do usuário.

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
        <string>--config</string>
        <string>/Users/[USER]/Library/Application Support/Hokai/appsettings.json</string>
        <string>run</string>
    </array>
    <key>WorkingDirectory</key>
    <string>/Users/[USER]/Library/Application Support/Hokai</string>
    <key>RunAtLoad</key>
    <false/>
    <key>KeepAlive</key>
    <dict>
        <key>SuccessfulExit</key>
        <false/>
    </dict>
    <key>ThrottleInterval</key>
    <integer>10</integer>
    <key>StandardOutPath</key>
    <string>/Users/[USER]/Library/Logs/Hokai/stdout.log</string>
    <key>StandardErrorPath</key>
    <string>/Users/[USER]/Library/Logs/Hokai/stderr.log</string>
    <key>EnvironmentVariables</key>
    <dict>
        <key>DOTNET_ENVIRONMENT</key>
        <string>Production</string>
    </dict>
</dict>
</plist>
```

`[USER]` é substituído pelo nome de usuário real durante o `install`. O plist usa `RunAtLoad=false`; o serviço é iniciado explicitamente via `launchctl kickstart`.

### 3.3 Windows — Windows Service

| Etapa | O que acontece |
|---|---|
| `install` | 1. Solicita elevação de privilégio (admin). 2. Cria diretório de config `%ProgramData%\Hokai\`. 3. Cria diretório de dados `%ProgramData%\Hokai\Data\`. 4. Escreve config padrão apenas se ausente. 5. Concede a `NT AUTHORITY\LocalService` acesso ao diretório de dados via `icacls`. 6. `sc.exe create Hokai binPath= "..." start= auto obj= "NT AUTHORITY\LocalService"`. |
| `uninstall` | 1. `sc.exe stop Hokai`. 2. `sc.exe delete Hokai`. 3. Com `--purge`: remove diretórios de config e dados. |
| `start` | `sc.exe start Hokai` |
| `stop` | `sc.exe stop Hokai` |
| `status` | `sc.exe query Hokai` → mapeia para rótulo |

O caminho do executável é fornecido pelo instalador externo; `service install` não copia o binário.

**Comando de instalação** (caminhos resolvidos em tempo de execução):

```powershell
sc.exe create Hokai `
    binPath= "\"C:\Program Files\Hokai\hokai.exe\" --config \"C:\ProgramData\Hokai\appsettings.json\" run" `
    start= auto `
    obj= "NT AUTHORITY\LocalService" `
    DisplayName= "Hokai Uptime Monitor"
```

- `Hosting.WindowsServices` garante que o processo responda corretamente aos comandos `Start`, `Stop` e `Shutdown` do Service Control Manager
- O binário deve ser publicado como self-contained para evitar dependência de runtime instalado
- `icacls` concede ao SID LocalService `(OI)(CI)(M)` no diretório de dados para que o serviço possa escrever resultados dos checks

---

## 4. Arquitetura Interna

### 4.1 Arquivos

```text
src/Hokai/
├── Commands/
│   └── ServiceCommands.cs
├── Services/
│   ├── ServiceManager.cs           # fachada, seleciona backend
│   ├── IServiceManagerBackend.cs   # contrato do backend
│   ├── ServiceManager.Linux.cs     # implementação systemd
│   ├── ServiceManager.MacOS.cs     # implementação launchd
│   └── ServiceManager.Windows.cs   # implementação Windows
└── Hosting/
    ├── ApplicationPaths.cs
    ├── ConfigurationPathResolver.cs
    ├── AppSettingsLoader.cs
    ├── HokaiApplication.cs         # roteador CLI/daemon
    └── ServiceCollectionExtensions.cs
```

### 4.2 Program.cs (planejado)

Usa `Host.CreateDefaultBuilder` para habilitar tanto `UseSystemd()` quanto `UseWindowsService()`:

```csharp
return await HokaiApplication.RunAsync(args);
```

### 4.3 Integração com Host

Ambos `UseSystemd()` e `UseWindowsService()` são sensíveis ao contexto e viram no-op quando não estão no ambiente correspondente:

```csharp
Host.CreateDefaultBuilder(args)
    .UseSystemd()
    .UseWindowsService(options =>
    {
        options.ServiceName = "Hokai";
    })
```

No Linux, `UseSystemd()` habilita `sd_notify` com `Type=notify` e trata `SIGTERM`.
No Windows, `UseWindowsService()` conecta ao Service Control Manager.
No macOS, o `ConsoleLifetime` padrão lida com os sinais do launchd.

### 4.4 ServiceManager

Abstração sobre as ferramentas nativas de cada plataforma. Responsabilidades:

```
ServiceManager
├── InstallAsync(cancellationToken)
│   ├── 1. DetectPlatform()
│   ├── 2. EnsureDirectoriesAsync()              # cria diretórios config + dados
│   ├── 3. WriteDefaultConfigAsync()             # apenas se ausente — nunca sobrescreve
│   ├── 4. ApplyPermissionsAsync()               # usuário, grupo, ACLs
│   ├── 5. GenerateDefinitionFileAsync()          # template específico da plataforma
│   ├── 6. WriteDefinitionFileAsync(path)         # escreve no local correto
│   └── 7. EnableServiceAsync()                  # systemctl enable / sc create
│
├── UninstallAsync(purge, cancellationToken)
│   ├── 1. StopServiceAsync()                    # systemctl stop / launchctl bootout / sc stop
│   ├── 2. DisableServiceAsync()                 # systemctl disable / sc delete
│   ├── 3. RemoveDefinitionFileAsync()           # unit / plist
│   └── 4. Se purge: RemoveConfigAndDataAsync()   # diretórios config + dados
│
├── StartAsync(cancellationToken)
├── StopAsync(cancellationToken)
└── GetStatusAsync(cancellationToken)
```

A cópia do binário é feita por scripts instaladores externos, não pelo `ServiceManager`.

### 4.5 ServiceCommands

Já implementado. O comando `service` delega para `IServiceManager` via subcomandos do `System.CommandLine`. O comando `uninstall` aceita `--purge` para remover configuração e dados.

### 4.6 Permissões e Elevação

| Plataforma | Comando | Requer Elevação? | Tratamento |
|---|---|---|---|
| Linux | `install/uninstall` | Sim (sudo) | Detecta se não é root → exibe mensagem com instruções de sudo |
| Linux | `start/stop/status` | Depende da policy do systemd | Executa direto |
| macOS | todos | Não (LaunchAgent) | Executa direto |
| Windows | `install/uninstall` | Sim (admin) | Exibe mensagem com instruções de admin |
| Windows | `start/stop/status` | Pode exigir admin | Executa direto; a policy do SO pode solicitar |

Estratégia de elevação:
1. Tenta executar comando diretamente
2. Se falhar com `PermissionDenied` / `AccessDenied`:
   - **Linux**: informa "Este comando requer privilégios de root. Execute com sudo."
   - **Windows**: informa "Este comando requer privilégios de administrador. Execute como Administrador."
3. Sem re-execução automática com `sudo` / `runas` na versão inicial.

---

## 5. Localização de Arquivos por Plataforma

As localizações de arquivos por plataforma estão documentadas em [Instalação > O Que é Instalado](installation.md#2-o-que-é-instalado). A tabela abaixo adiciona detalhes específicos do daemon:

| Arquivo | Linux | macOS | Windows |
|---|---|---|---|
| Logs | journald (integrado ao systemd) | `~/Library/Logs/Hokai/stdout.log` e `~/Library/Logs/Hokai/stderr.log` | Event Log (integrado ao Windows Service) |
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

| Questão | Impacto | Decisão |
|---|---|---|
| Criar usuário `hokai` no Linux automaticamente no `install`? | Segurança vs conveniência | Sim — install cria usuário e grupo de forma idempotente. |
| `uninstall` deve remover config e dados? | Perda de dados acidental | Apenas com `--purge`. Padrão preserva ambos. |
| Suportar `hokai service logs` para tail de logs? | Conveniência | Melhoria futura. Por ora `journalctl -u hokai` no Linux. |
| Suportar múltiplas instâncias do serviço? | Escopo grande | Fora do escopo inicial. Apenas single-instance. |
| O daemon deve expor um health check HTTP próprio? | Monitoramento do monitor | Fora do escopo inicial. |

---

## 8. Implementação — Estado Atual

1. **Contrato IServiceManager** — definido
2. **ServiceCommands** — implementado
3. **Backends ServiceManager** — implementado (systemd, launchd, Windows)
4. **Bootstrap do Host e DI** — implementado
5. **Roteador Program.cs** — implementado
6. **Templates** — incorporados como constantes string nas implementações
