# Hokai — NativeAOT

> Plano e linha de base de compatibilidade para compilação NativeAOT, com alvo em `v0.2.0-alpha.1`.

**Documentos relacionados**: [Arquitetura](architecture.md) | [Desempenho](performance.md) | [Release](release.md)

---

## 1. Escopo

O NativeAOT substitui binários JIT autossuficientes por executáveis nativos compilados ahead-of-time. Isso proporciona artefatos menores, inicialização mais rápida e menor uso de memória.

**Release alvo**: `v0.2.0-alpha.1` (pre-release). Releases estáveis herdarão o AOT após validação.

---

## 2. Requisitos de Ferramentas

| RID | SO Anfitrião | Pré-requisitos Nativos |
|---|---|---|
| linux-x64 | Linux x64 | `clang`, `zlib1g-dev` |
| linux-arm64 | Linux ARM64 | `clang`, `zlib1g-dev` |
| osx-x64 | macOS com Xcode | Command Line Tools |
| osx-arm64 | macOS com Xcode | Command Line Tools |
| win-x64 | Windows | Visual Studio C++ Desktop workload |
| win-arm64 | Windows | ARM64 C++ build tools + Windows SDK |

Os runners do CI fornecem as ferramentas apropriadas para builds na mesma arquitetura. Builds de arquitetura cruzada (ex.: macOS x64 em runner ARM64) exigem configuração explícita de toolchain.

---

## 3. Linha de Base de Compatibilidade Linux

Executáveis NativeAOT vinculam-se ao runtime C da plataforma. O binário de release deve suportar a versão mais antiga de glibc definida como alvo.

**Linha de base**: Ubuntu 24.04 (`glibc 2.39`). Compilar nos runners `ubuntu-24.04` ou `ubuntu-latest`.

**Verificação**: `ldd hokai` não deve reportar símbolos não resolvidos na distribuição de linha de base.

---

## 4. Estado de Prontidão para AOT

### Bloqueadores atuais (devem ser resolvidos antes de habilitar AOT)

| Bloqueador | Status |
|---|---|
| `HokaiJsonContext` gerado mas não utilizado; persistência usa `JsonSerializer` baseado em reflexão | Resolvido — Fase 9 |
| `PublishTrimmed=false`, `PublishAot` não configurado | Resolvido — Fase 10 |
| Lock file não possui assets do compilador NativeAOT | Resolvido — Fase 10 |
| Sem enforcement de warnings AOT no CI | Resolvido — Fase 10 |
| Docker usa stage de SDK forçado para AMD64; AOT ARM64 precisa de toolchain nativo | Resolvido — Fase 12 |

### Provavelmente compatível (requer verificação AOT)

- `System.CommandLine` (oficialmente compatível com trim/AOT)
- Binding de configuração com source generator
- `IHttpClientFactory` / `HttpClient`
- `System.Net.Mail` / `SmtpClient`
- Host builder e contêiner DI
- `ProcessRunner` (execução de processos nativos)
- `PeriodicTimer`
- Extensões de hosting systemd e Windows Service

### Otimizações adiadas

| Otimização | Quando |
|---|---|
| Host builder reduzido (`CreateApplicationBuilder`) | Após validação de correção |
| Referências condicionais de pacotes por RID | Após publicação AOT de seis RIDs |
| `IlcOptimizationPreference=Size` | Após linha de base de tamanho |
| Globalização invariante | Após testes de URI/formatação/endereço SMTP |
| Suporte a debugger, redução de stack traces | Após revisão de tratamento de erros |

---

## 5. Fases de Implementação

Consulte o milestone da release para detalhes das fases. Este documento rastreia as fases específicas do AOT:

| Fase | Branch | Escopo |
|---|---|---|
| 9 | `refactor/storage-aot-json` | Conectar `JsonTypeInfo` ao `AtomicJsonFile` |
| 10 | `build/native-aot-linux` | Habilitar AOT/trimming estrito, regenerar lock graph, CI Linux x64 |
| 11 | `build/native-aot-platforms` | Runners nativos para seis RIDs (ubuntu-24.04, ubuntu-24.04-arm, macos-15-intel, macos-15, windows-2025, windows-11-arm), smoke test funcional, verificação `ldd` |
| 12 | `build/native-aot-docker` | `FROM --platform=$BUILDPLATFORM`, publish AOT baseado em RID explícito, tag do compose corrigida (#82) |
| 13 | `build/aot-qualification` | Script de benchmark reproduzível, gates de tamanho e inicialização no CI, resultados medidos abaixo (#84) |

As fases anteriores (1–8) corrigem bugs e fortalecem o projeto antes do AOT ser habilitado.

---

## 6. Critérios de Aceitação do AOT

Antes de criar a tag `v0.2.0-alpha.1`:

- [x] Todos os seis RIDs de release publicam com `PublishAot=true` e zero warnings.
- [x] Artefatos na mesma arquitetura passam em smoke tests funcionais (endpoint add/list/status/remove).
- [x] `endpoints.json` e `checks.json` existentes permanecem legíveis (fixtures do rc.2, #81).
- [x] Builds JIT e AOT usam semântica de armazenamento idêntica (mesma suite de testes passa em ambos).
- [x] Redução de tamanho ≥30%, melhoria de inicialização ≥20% (vs linhas de base JIT do rc.2).
- [x] Imagens Docker AMD64 e ARM64 compiladas a partir de binários AOT (#82).
- [x] Sem regressão nos 228 testes existentes.

### Qualificação Medida (linux-x64, CI run 29388189153)

| Métrica | rc.2 (JIT) | Candidato (AOT) | Alvo | Resultado |
|---|---|---|---|---|
| Tamanho do binário | 75,600,670 B | 9,877,600 B | redução ≥30% | **87%** |
| Inicialização a frio | 174 ms | 20 ms | ≥20% mais rápido | **89%** |

---

## 7. Comandos de Verificação

### Restore AOT travado

```bash
dotnet restore src/Hokai/Hokai.csproj \
  -r linux-x64 --locked-mode \
  -p:PublishAot=true -p:PublishTrimmed=true
```

### Publicação AOT sem warnings

```bash
dotnet publish src/Hokai/Hokai.csproj \
  -c Release -r linux-x64 --self-contained true --no-restore \
  -p:PublishAot=true -p:PublishTrimmed=true \
  -p:TrimMode=full -p:TrimmerSingleWarn=false \
  -warnaserror -o artifacts/aot/linux-x64
```

### Smoke test funcional

```bash
bin=artifacts/aot/linux-x64/hokai
$bin --help
$bin --version
$bin endpoint add http://127.0.0.1:8080 --interval 1s --timeout 1s
$bin endpoint list
$bin status
$bin endpoint remove <id>
```

---

## 8. Melhorias Futuras

- [ ] Detecção automatizada de regressão de tamanho AOT no CI
- [ ] Feature switches por RID para excluir pacotes de plataforma não utilizados
- [ ] Benchmark de `IlcOptimizationPreference=Size`
- [ ] Avaliação de globalização invariante
- [ ] Investigação de macOS Universal Binary (fat binary)
