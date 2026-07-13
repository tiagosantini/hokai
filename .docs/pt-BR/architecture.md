# Hokai — CLI & Daemon de Monitoramento de Uptime

> Aplicação portável e multiplataforma para monitoramento de uptime via CLI, com notificação por email e persistência em JSON. Construída com .NET 10 com dependências mínimas.

**Documentos relacionados**: [Daemonização](daemonization.md) | [Instalação](installation.md)

---

## 1. Visão Geral

O Hokai é uma ferramenta de monitoramento de uptime que roda em background. Usuários configuram endpoints HTTP/HTTPS via CLI, e um daemon realiza health checks periódicos, calcula percentual de uptime (janela de 24h) e notifica por email quando detecta downtime ou recuperação.

### Princípios de Design

- **Mínimas dependências** — apenas NuGet da Microsoft (veja [Daemonização > Dependências](daemonization.md#1-design-decisions-settled) para a lista completa)
- **Portável** — single binary, sem IPC, sem serviços de SO específicos
- **Operação offline-first** — CLI e daemon se comunicam exclusivamente via sistema de arquivos

---

## 2. Stack Tecnológica

| Componente | Tecnologia | Origem |
|---|---|---|
| Runtime | .NET 10 | SDK |
| CLI Parser | `System.CommandLine` | NuGet (Microsoft) |
| Host / DI | `Microsoft.Extensions.Hosting` | SDK |
| HTTP Client | `System.Net.Http` + `IHttpClientFactory` | SDK + NuGet (Microsoft) |
| SMTP | `System.Net.Mail` (SmtpClient) | SDK |
| Serialização | `System.Text.Json` | SDK |
| Timer | `System.Threading.PeriodicTimer` | SDK |
| Config | `Microsoft.Extensions.Configuration` | SDK |
| Logging | `Microsoft.Extensions.Logging` | SDK |

**Total: 2 dependências externas para o aplicativo principal.** Para integração com serviços do SO, mais 2 pacotes Microsoft são necessários — veja [Daemonização > Dependências](daemonization.md#1-design-decisions-settled).

---

## 3. Estrutura do Projeto

```
hokai/
├── hokai.slnx
├── src/
│   └── Hokai/
│       ├── Hokai.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       ├── Commands/
│       │   ├── EndpointCommands.cs
│       │   └── ServiceCommands.cs       # service install/start/stop
│       ├── Models/
│       │   ├── EndpointConfig.cs
│       │   ├── CheckResult.cs
│       │   └── SmtpSettings.cs
│       └── Services/
│           ├── MonitorService.cs
│           ├── HealthCheckService.cs
│           ├── NotificationService.cs
│           ├── EndpointStore.cs
│           ├── CheckStore.cs
│           └── ServiceManager.cs        # abstração de serviço do SO
├── scripts/                              # scripts de instalação
├── .github/                               # template de PR, workflows CI
└── .docs/                                 # documentos de design
    ├── architecture.md
    ├── daemonization.md
    └── installation.md
```

*A árvore completa incluindo scripts de instalação, Docker e CI está em [Instalação > Estrutura do Projeto](installation.md#9-estrutura-do-projeto).*

---

## 4. Comandos CLI

### `hokai endpoint add <url>`
Adiciona um endpoint para monitoramento.

```
hokai endpoint add https://api.example.com/health \
    --interval 5m \
    --timeout 30s \
    --method GET \
    --expect 200
```

### `hokai endpoint list`
Lista todos os endpoints configurados e seu uptime % nas últimas 24h.

```
hokai endpoint list
```

### `hokai endpoint remove <id>`
Remove um endpoint pelo ID (obtido no `list`).

```
hokai endpoint remove abc123
```

### `hokai run`
Inicia o daemon de monitoramento em foreground. Para execução em background como serviço do SO, veja [Daemonização](daemonization.md).

```
hokai run
```

### `hokai status`
Exibe status atual de todos os endpoints: último check, tempo de resposta, e uptime % em 24h.

```
hokai status
```

---

## 5. Modelo de Dados

### EndpointConfig

Persistido em `Data/endpoints.json`.

```json
[
  {
    "id": "a1b2c3d4...",
    "url": "https://api.example.com/health",
    "interval": "00:05:00",
    "timeout": "00:00:30",
    "method": "GET",
    "expectedStatus": 200,
    "createdAt": "2026-07-10T12:00:00Z"
  }
]
```

### CheckResult

Persistido em `Data/checks.json`. Lista plana; registros antigos são removidos com base no `retentionDays`.

```json
[
  {
    "endpointId": "a1b2c3d4...",
    "timestamp": "2026-07-10T12:05:00Z",
    "isUp": true,
    "statusCode": 200,
    "responseTimeMs": 145,
    "error": null
  }
]
```

### State (transiente, em memória)

O `MonitorService` mantém em memória o último estado conhecido de cada endpoint (`UP`/`DOWN`) para detectar transições e evitar notificações duplicadas.

---

## 6. Arquitetura Interna

### 6.1 Program.cs — Roteador CLI vs Daemon

Modelo **single binary, dual mode**:

```
Program.Main(args)
 ├── "run"       → Host.CreateApplicationBuilder → AddHostedService<MonitorService> → host.Run()
 ├── "endpoint"  → EndpointCommands handler → EndpointStore → saída console
 ├── "status"    → EndpointStore + CheckStore → console
 └── outro       → System.CommandLine mostra help
```

### 6.2 MonitorService — Daemon Principal

`BackgroundService` que coordena toda a execução em background.

```
MonitorService.ExecuteAsync()
 │
 ├── 1. Carrega endpoints de EndpointStore
 ├── 2. Para cada endpoint: dispara task, verifica imediatamente e então usa PeriodicTimer
 │
 ├── Loop de recarga (a cada 30s):
 │   ├── Recarrega endpoints.json
 │   ├── Inicia tasks para novos endpoints
 │   ├── Cancela tasks de endpoints removidos
 │   └── Reinicia tasks cujas configurações de monitoramento mudaram
 │
 ├── Loop de limpeza (a cada 1h):
 │   └── CheckStore.RemoveOlderThan(retentionDays)
 │
 └── Cada task de endpoint:
     └── HealthCheckService.Check(endpoint)
         ├── CheckStore.Append(result)
         ├── Se transição de estado: NotificationService.Notify(endpoint, result)
         ├── Atualiza estado em memória
         └── await timer.WaitForNextTickAsync()
```

#### Sincronização entre CLI e Daemon

- CLI escreve em `Data/endpoints.json` e exit imediatamente
- Daemon relê o arquivo a cada 30s para detectar mudanças
- Sem lock de arquivo entre processos; o fluxo normal possui um processo escritor por arquivo
- Escritores são serializados no processo e publicam um arquivo temporário no mesmo diretório por rename atômico
- Sem IPC, sem comunicação entre processos

### 6.3 HealthCheckService

Responsabilidade: executar o health check HTTP e retornar um `CheckResult`.

```csharp
Task<CheckResult> CheckAsync(EndpointConfig endpoint, CancellationToken cancellationToken)
```

- Usa `IHttpClientFactory` para gerenciamento de conexões
- Usa token vinculado para timeout por endpoint; cancelamento do chamador é relançado
- Timeout e erros de transporte retornam DOWN com status nulo
- Timestamp é o horário UTC de conclusão e duração usa o relógio monotônico de `TimeProvider`
- Redirecionamentos não são seguidos, corpos não são lidos e métodos sem corpo configurado enviam requisição vazia
- Apenas URLs HTTP/HTTPS absolutas, timeouts positivos, métodos válidos e status entre 100 e 599 são aceitos

### 6.4 NotificationService

Responsabilidade: enviar email quando um endpoint muda de estado.

```csharp
Task NotifyDownAsync(EndpointConfig endpoint, CheckResult result, CancellationToken cancellationToken)
Task NotifyRecoveryAsync(EndpointConfig endpoint, CheckResult result, CancellationToken cancellationToken)
```

- Usa um novo `SmtpClient` do `System.Net.Mail` por envio
- Lê configuração SMTP de `appsettings.json`
- Assunto DOWN: `[HOKAI ALERT] {url} is DOWN`
- Assunto de recuperação: `[HOKAI RECOVERY] {url} is UP`
- Corpos em texto puro incluem endpoint, timestamp, status esperado/real, tempo de resposta e erro de transporte
- Lista de destinatários vazia ignora o envio. Falhas comuns de SMTP/configuração são registradas sem retry; cancelamento propaga

#### Política de falhas e recarga do Monitor

- Um resultado é persistido antes da notificação ou avanço do estado. Falha no append mantém o estado anterior.
- Falha de notificação é registrada e o estado avança, evitando alertas repetidos da mesma transição.
- O primeiro resultado persistido estabelece estado sem notificação.
- Remover ou alterar endpoint cancela seu worker e limpa o estado transiente.
- Recargas malformadas e IDs duplicados são rejeitados enquanto workers existentes continuam inalterados.
- Falhas de limpeza são registradas e repetidas no próximo tick horário.

### 6.5 EndpointStore

Responsabilidade: CRUD em `Data/endpoints.json`.

- `Task<IReadOnlyList<EndpointConfig>> GetAllAsync(CancellationToken cancellationToken)`
- `Task<EndpointConfig?> GetByIdAsync(string id, CancellationToken cancellationToken)`
- `Task AddAsync(EndpointConfig config, CancellationToken cancellationToken)`
- `Task<bool> RemoveAsync(string id, CancellationToken cancellationToken)`

### 6.6 CheckStore

Responsabilidade: append de resultados e cálculo de uptime em `Data/checks.json`.

- `Task AppendAsync(CheckResult result, CancellationToken cancellationToken)`
- `Task<double> GetUptimeAsync(string endpointId, TimeSpan window, CancellationToken cancellationToken)` — ex: últimas 24h
- `Task<CheckResult?> GetLastCheckAsync(string endpointId, CancellationToken cancellationToken)`
- `Task RemoveOlderThanAsync(TimeSpan retention, CancellationToken cancellationToken)`

### 6.7 Contratos de Persistência

- `IEndpointStore` e `ICheckStore` isolam o I/O de arquivos dos comandos e serviços.
- Arquivos ausentes representam coleções vazias. Mutações criam o diretório de dados quando necessário.
- JSON vazio, `null` ou malformado é um erro e nunca é substituído silenciosamente.
- Os arquivos são arrays JSON camelCase e indentados, codificados como UTF-8 sem BOM.
- Mutações usam um arquivo temporário único no diretório de destino e então o publicam atomicamente.
- Um lock por processo indexado pelo caminho canônico serializa mutações de todas as instâncias dos Stores.
- Conflitos de escrita entre processos estão fora do contrato inicial; a CLI escreve endpoints enquanto o daemon escreve checks.
- Append e limpeza reescrevem o array JSON completo; isso prioriza correção inicial e visibilidade atômica em vez de escalabilidade para históricos grandes.
- IDs de endpoint usam comparação ordinal e sensível a maiúsculas. Adicionar ID duplicado falha; remover ID desconhecido retorna `false` sem reescrever o arquivo.
- Remover um endpoint não remove seus checks históricos, que permanecem até a limpeza por retenção.
- Uptime usa checks no intervalo UTC inclusivo `[agora - janela, agora]`; checks futuros são excluídos e uma janela vazia retorna `0.0`.
- Janelas de uptime devem ser positivas. Retenção deve ser não negativa e remove checks estritamente anteriores ao limite.
- `GetLastCheckAsync` retorna o resultado correspondente com o maior timestamp, independentemente da ordem no arquivo.

---

## 7. Fluxo de Notificações

```
Estado anterior (memória)     Check Atual        Ação
─────────────────────────────────────────────────────────
null (primeiro check)         UP                 Nenhuma
null (primeiro check)         DOWN               Nenhuma
UP                            UP                 Nenhuma
UP                            DOWN               Email DOWN
DOWN                          DOWN               Nenhuma
DOWN                          UP                 Email RECOVERY
```

Regra: só notifica na **transição** entre estados, evitando spam.

---

## 8. Configuração

`appsettings.json`:

```json
{
  "Smtp": {
    "Host": "localhost",
    "Port": 25,
    "UseSsl": false,
    "Username": "",
    "Password": "",
    "FromAddress": "hokai@localhost",
    "ToAddresses": ["admin@example.com"]
  },
  "DataDirectory": "Data",
  "RetentionDays": 30
}
```

- `DataDirectory`: relativo ao working directory ou caminho absoluto
- `RetentionDays`: checks mais antigos que isso são removidos automaticamente
- Arquivo opcional: se não existir, defaults razoáveis são usados

---

## 9. Dependências Detalhadas

### NuGet (externa)

| Pacote | Versão | Motivo |
|---|---|---|
| `System.CommandLine` | 2.0.x | Parsing CLI com subcomandos, help automático, validação |
| `Microsoft.Extensions.Http` | 10.0.x | `IHttpClientFactory`, ciclo de handlers, pooling de conexões |

### SDK (built-in, sem NuGet)

| Namespace | Uso |
|---|---|
| `Microsoft.Extensions.Hosting` | Worker Service, DI, lifecycle |
| `Microsoft.Extensions.Configuration.Json` | Leitura de `appsettings.json` |
| `Microsoft.Extensions.Logging.Console` | Log do daemon no console |
| `System.Net.Mail` | `SmtpClient`, `MailMessage` |
| `System.Net.Http` | `HttpClient` para health checks |
| `System.Text.Json` | Serialização dos arquivos de dados |
| `System.Threading` | `PeriodicTimer` para scheduling |

---

## 10. Considerações de Cross-Platform

| Aspecto | Estratégia |
|---|---|
| Path do diretório de dados | `Path.Combine` + `Environment.SpecialFolder` ou configurável |
| Daemonização | Veja [Daemonização](daemonization.md) — `systemd` (Linux), `launchd` (macOS), Windows Service |
| Newline em arquivos | `Environment.NewLine` |
| Encoding | UTF-8 sem BOM (padrão `System.Text.Json`) |
| Single binary | `dotnet publish -r <rid> --self-contained true` ou `-p:PublishSingleFile=true` |

---

## 11. Melhorias Futuras

- [ ] Suporte a TCP health checks (conexão de socket)
- [ ] Suporte a ICMP ping
- [ ] Dashboard web embutido (minimal API embutida no daemon)
- [ ] Notificação via webhook (Slack, Discord, etc.)
- [ ] Histórico de uptime com gráfico (exportação CSV)
- [ ] Métricas Prometheus expostas via endpoint HTTP
- [ ] Criptografia de senha SMTP em repouso
- [ ] Suporte a multi-tenancy (múltiplos destinatários por endpoint)
- [ ] Watchdog / health check do próprio daemon
- [ ] Testes de integração com servidor SMTP mock

Para melhorias relacionadas à instalação (Homebrew, APT, winget, Docker, auto-update) veja [Instalação > Melhorias Futuras](installation.md#10-melhorias-futuras). Para melhorias do daemon/serviço (log tailing, múltiplas instâncias) veja [Daemonização > Decisões Pendentes](daemonization.md#7-decisões-pendentes).

---

## 12. Glossário

| Termo | Definição |
|---|---|
| **Endpoint** | URL HTTP/HTTPS sendo monitorada |
| **Check** | Requisição HTTP individual |
| **Uptime %** | (checks bem-sucedidos / total de checks) × 100 nos últimos 24h |
| **Daemon** | Processo `hokai run` que fica em execução contínua |
| **Transição** | Mudança de estado UP ↔ DOWN que dispara notificação |
| **IPC** | Inter-Process Communication (deliberadamente não usado) |
