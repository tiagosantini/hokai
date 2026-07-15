# Hokai — Desempenho

> Tamanho, inicialização, uso de memória e estratégia de otimização de armazenamento.

**Documentos relacionados**: [Arquitetura](architecture.md) | [Release](release.md)

---

## 1. Tamanho dos Binários

Todas as medições são para binários independentes (`self-contained`) publicados com `PublishSingleFile=true` e `PublishSelfContained=true`.

### 1.1 Tamanho dos Binários AOT (v0.2.0-alpha.1, linux-x64)

| Métrica | rc.2 (JIT, PublishTrimmed=false) | Candidato (AOT, TrimMode=full) | Redução |
|---|---|---|---|
| Binário não comprimido | 75,600,670 B (~72 MiB) | 9,877,600 B (~9.4 MiB) | 87% |
| `.tar.gz` comprimido | ~29 MiB | ~TBD | — |

*Medido a partir do CI run 29388189153. Tabela completa de tamanhos AOT para seis RIDs será preenchida após o dry-run da release. Imagens Docker devem ser ~82% menores que as equivalentes do rc.2 na base `runtime-deps` chiseled.*

### 1.2 Linha de Base JIT do RC.2 (histórico)

Tamanhos de executáveis rc.2 por RID (não comprimidos, PublishTrimmed=false):

| RID | Tamanho Aproximado |
|---|---|
| linux-x64 | ~71 MiB |
| linux-arm64 | ~71 MiB |
| osx-x64 | ~76 MiB |
| osx-arm64 | ~76 MiB |
| win-x64 | ~80 MiB |
| win-arm64 | ~82 MiB |

*Medido a partir da saída de publicação `v0.1.0-rc.1`. Arredondado para o MiB mais próximo.*

---

## 2. Tempo de Inicialização

### 2.1 Inicialização Fria (CLI `--version`)

Medido no ubuntu-24.04 x64 via `scripts/bench-aot.sh` (7 execuções frias, mediana):

| Versão | Compilação | Inicialização (mediana) | Origem |
|---|---|---|---|
| rc.2 | JIT | 174 ms | Download da release |
| Candidato | NativeAOT | 20 ms | Saída do CI publish |

**Melhoria**: 89% mais rápido na inicialização fria em relação à linha de base JIT do rc.2.

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

### 4.2 Otimização de Resumo em Lote (v0.1.0-rc.2 → v0.2.0-alpha.1)

`GetBatchSummariesAsync` lê o `checks.json` uma única vez e calcula os resumos de uptime e última verificação para todos os endpoints em uma única passagem. Otimizado para agrupamento O(C) em passagem única no v0.2.0-alpha.1 (#80).

| Complexidade | Antes (rc.1) | Após (#80) |
|---|---|---|
| `endpoint list` | E leituras de `checks.json` | 1 leitura, 1 passagem |
| `status` | 2E leituras de `checks.json` | 1 leitura, 1 passagem |
| Algorítmica | O(E × C) | O(C) |

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

- [x] Publicação NativeAOT (redução de tamanho 87%, melhoria de inicialização 89%, #84)
- [ ] Tabela completa de qualificação de tamanho/inicialização para seis RIDs
- [ ] Detecção automatizada de regressão de desempenho no CI além de linux-x64
- [ ] Formato de armazenamento orientado a append
- [ ] Arquivo de verificações mapeado em memória para monitoramento de alta frequência
- [ ] Recarga de configuração a quente sem polling de arquivo
- [ ] Verificações paralelas (atualmente sequenciais por endpoint)

### Benchmarks Reproduzíveis

Qualificado via CI com `scripts/bench-aot.sh`:
- Baixa a linha de base rc.2 das releases do GitHub
- Mede inicialização fria (mediana de 7 execuções cronometradas após 3 de aquecimento)
- Mede tamanho do binário não comprimido
- Impõe redução de tamanho ≥30% e melhoria de inicialização ≥20%
