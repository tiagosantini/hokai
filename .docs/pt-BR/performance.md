# Hokai — Desempenho

> Tamanho, inicialização, uso de memória e estratégia de otimização de armazenamento.

**Documentos relacionados**: [Arquitetura](architecture.md) | [Release](release.md)

---

## 1. Tamanho dos Binários

Todas as medições são para binários independentes (`self-contained`) publicados com `PublishSingleFile=true`, `PublishSelfContained=true` e `PublishTrimmed=false`.

### 1.1 Tamanho dos Executáveis (não comprimidos)

| RID | Tamanho Aproximado |
|---|---|
| linux-x64 | ~71 MiB |
| linux-arm64 | ~71 MiB |
| osx-x64 | ~76 MiB |
| osx-arm64 | ~76 MiB |
| win-x64 | ~80 MiB |
| win-arm64 | ~82 MiB |

*Medido a partir da saída de publicação `v0.1.0-rc.1`. Arredondado para o MiB mais próximo.*

### 1.2 Tamanho dos Arquivos (comprimidos)

| RID | Arquivo | Tamanho Aproximado |
|---|---|---|
| linux-x64 | `.tar.gz` | ~29 MiB |
| linux-arm64 | `.tar.gz` | ~29 MiB |
| osx-x64 | `.tar.gz` | ~31 MiB |
| osx-arm64 | `.tar.gz` | ~32 MiB |
| win-x64 | `.zip` | ~32 MiB |
| win-arm64 | `.zip` | ~33 MiB |

*Tamanho total da distribuição: ~182.7 MiB em seis arquivos.*

### 1.3 Tamanho da Imagem Docker

Imagem multi-arquitetura publicada em `ghcr.io/tiagosantini/hokai`.

| Plataforma | Tamanho Aproximado |
|---|---|
| linux/amd64 | ~38 MiB (comprimido) |
| linux/arm64 | ~37 MiB (comprimido) |

*Baseado em `runtime-deps:10.0-noble-chiseled` com o binário self-contained recortado.*

---

## 2. Tempo de Inicialização

Os comandos CLI do Hokai realizam o seguinte trabalho na inicialização:
- Resolução do caminho de configuração (cadeia de fallback em 4 etapas)
- Carregamento e binding da configuração JSON (com source generator a partir da `v0.1.0-rc.2`)
- Inicialização dos armazenamentos (sem conexão com banco de dados, sem rede)

O daemon (`hokai run`) adicionalmente:
- Verificação de registro do serviço do SO
- Criação da fábrica de clientes HTTP
- Carregamento da configuração dos endpoints

**Linha de base estimada para comandos CLI**: < 200 ms em hardware moderno.
*Benchmarks precisos serão adicionados em uma versão futura.*

### 2.1 Meta de Inicialização com AOT

A compilação NativeAOT (planejada para `v0.2.0-alpha.1`) tem como meta:
- Inicialização de comando CLI: < 100 ms
- Inicialização do daemon: < 150 ms
- Melhoria ≥20% em relação à inicialização JIT atual

---

## 3. Uso de Memória

### 3.1 Perfil de Memória

| Componente | Memória Residente Estimada |
|---|---|
| Comando CLI (curta duração) | ~15–20 MiB |
| Daemon (`hokai run`) | ~25–35 MiB (ocioso, sobrecarga por endpoint mínima) |

*A memória do daemon cresce com o histórico de verificações retido na janela de 24h. A memória é limitada pela janela de retenção e pelo número de endpoints.*

### 3.2 Meta de Redução de Memória com AOT

A compilação NativeAOT tem como meta redução ≥15% no conjunto de trabalho devido a:
- Sem sobrecarga de compilação JIT
- Sem bookkeeping de compilação em camadas
- Layout de tipo estático definido em tempo de compilação

---

## 4. Otimização de Armazenamento

### 4.1 Estado Atual

| Arquivo | Padrão de Acesso | Complexidade |
|---|---|---|
| `endpoints.json` | Leitura na inicialização, escrita ao adicionar/remover | O(E) |
| `checks.json` | Leitura por consulta, escrita ao adicionar/purgar | O(C) por leitura |

Cada consulta de status de endpoint lê o arquivo `checks.json` inteiro. Com E endpoints e C verificações, verificar todos os endpoints requer O(E × C) de trabalho total.

### 4.2 Otimização de Resumo em Lote (v0.1.0-rc.2)

O método `CheckStore.GetBatchSummariesAsync` lê o `checks.json` uma única vez e calcula os resumos de uptime e última verificação para todos os endpoints em uma única passagem.

| Comando | Antes | Depois |
|---|---|---|
| `endpoint list` | E leituras de `checks.json` | 1 leitura de `checks.json` |
| `status` | 2E leituras de `checks.json` | 1 leitura de `checks.json` |

**Resultado**: Os comandos de status e lista agora escalam O(E + C) em vez de O(E × C).

### 4.3 Formato Orientado a Append Futuro (planejado)

Atualmente, cada append reescreve todo o array `checks.json`. Versões futuras explorarão:
- JSON orientado a append com compactação periódica
- Segmentos de log rotativos com rotação baseada em tempo
- Acesso a arquivo mapeado em memória para verificações de alta frequência

Essas mudanças requerem migração do formato de armazenamento e serão adiadas para uma versão principal futura.

---

## 5. Desempenho de Configuração

| `v0.1.0-rc.1` | `v0.1.0-rc.2` |
|---|---|
| `ConfigurationBuilder.Bind()` baseado em reflexão | Binding com source generator (`EnableConfigurationBindingGenerator`) |
| Inspeção de tipos em tempo de execução | Acesso a propriedades em tempo de compilação |
| Inicialização fria mais lenta | Carregamento de configuração sem reflexão |

---

## 6. Benchmarks

*Planejado para uma versão futura. Medições alvo:*

- Latência fim-a-fim de comando CLI (inicialização fria + execução)
- Memória em estado estacionário do daemon (1, 10, 50 endpoints)
- Vazão de verificações (verificações/segundo em endpoint HTTP ocioso)
- Vazão de I/O de arquivo (appends/minuto, leituras/minuto)
- Tempo de inicialização (tempo até primeira verificação)

---

## 7. Melhorias Futuras

- [ ] Benchmarks de desempenho automatizados no CI
- [ ] Publicação NativeAOT (redução de tamanho ≥30%, melhoria de inicialização ≥20%)
- [ ] Formato de armazenamento orientado a append
- [ ] Arquivo de verificações mapeado em memória para monitoramento de alta frequência
- [ ] Recarga de configuração a quente sem polling de arquivo
- [ ] Verificações paralelas (atualmente sequenciais por endpoint)
