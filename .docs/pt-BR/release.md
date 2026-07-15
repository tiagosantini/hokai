# Hokai — Processo de Release

> Como o Hokai é compilado, empacotado e distribuído.

## 1. Versionamento

O Hokai segue Versionamento Semântico estrito (MAJOR.MINOR.PATCH).

- Tags do Git devem corresponder a `v*.*.*` (ex: `v1.0.0`, `v0.2.0-alpha.1`).
- A tag deve apontar para um commit no branch `main`.
- Metadados de versão são incorporados durante a compilação via `-p:Version=<tag>`.
- Tags de pré-release (alpha, beta, rc) recebem apenas a tag completa no Docker e não movem `latest`.

## 2. Artefatos de Build

Cada release produz seis binários NativeAOT autocontidos:

| Asset | Plataforma | Build |
|---|---|---|
| `hokai-linux-x64.tar.gz` | Linux x86_64 | ubuntu-24.04 |
| `hokai-linux-arm64.tar.gz` | Linux ARM64 | ubuntu-24.04-arm |
| `hokai-osx-x64.tar.gz` | macOS x86_64 | macos-15-intel |
| `hokai-osx-arm64.tar.gz` | macOS ARM64 | macos-15 |
| `hokai-win-x64.zip` | Windows x86_64 | windows-2025 |
| `hokai-win-arm64.zip` | Windows ARM64 | windows-11-arm |

Todos os arquivos contêm um único executável NativeAOT (`hokai` no Unix, `hokai.exe` no Windows) compilado com `PublishAot=true`, `PublishTrimmed=true`, `TrimMode=full` e `-warnaserror`. Cada plataforma compila e testa em seu runner nativo — sem cross-compilação ou emulação QEMU no pipeline de release.

Assets adicionais:

| Asset | Descrição |
|---|---|
| `install.sh` | Instalador Unix (Linux + macOS) |
| `uninstall.sh` | Desinstalador Unix |
| `install.ps1` | Instalador Windows |
| `uninstall.ps1` | Desinstalador Windows |
| `SHA256SUMS` | Checksums SHA-256 de todos os archives e scripts |
| `SOURCE_SHA` | SHA do commit Git usado na release |

## 3. Fluxo de Release

1. O push de uma tag anotada `vX.Y.Z` para `main` aciona o workflow de release.
2. O workflow valida que a tag é alcançável a partir de `origin/main` e tem o formato esperado.
3. Os seis RIDs são publicados em seus runners nativos com `PublishAot=true` e `-warnaserror`.
4. Cada artefato é testado em seu runner nativo: `--help`, `--version`, endpoint add/list/status/remove e `ldd` (apenas Linux).
5. `SOURCE_SHA` é registrado, depois `SHA256SUMS` é gerado (incluindo `SOURCE_SHA`) e auto-validado.
6. Um GitHub Release em draft é criado com todos os assets e attestation de proveniência.
7. A release é publicada manualmente após revisão final.
8. Publicar a GitHub Release aciona o build multi-plataforma da imagem GHCR.

## 4. Imagens Docker

Fonte: `Dockerfile` usando build multi-estágio com `--platform=$BUILDPLATFORM`.

Imagens são publicadas em `ghcr.io/tiagosantini/hokai` com estas tags:

| Tipo de release | Tags |
|---|---|
| Estável (ex: `1.2.3`) | `1.2.3`, `1.2`, `1`, `latest` |
| Pré-release (ex: `0.2.0-alpha.1`) | apenas `0.2.0-alpha.1` |

Imagens suportam `linux/amd64` e `linux/arm64`, compiladas via Buildx com emulação QEMU no estágio SDK e linking nativo para a arquitetura alvo. O estágio de runtime usa `runtime-deps:10.0-noble-chiseled` com usuário não-root (UID 1000).

## 5. Assinatura e Proveniência

- `SHA256SUMS` inclui todos os archives, scripts e `SOURCE_SHA` para integridade completa dos artefatos.
- `SOURCE_SHA` registra o commit Git exato do qual a release foi compilada.
- Atestação de proveniência do GitHub Actions cobre todos os assets da release e `SOURCE_SHA`.
- Code signing (Authenticode no Windows, notarização no macOS) é planejado como melhoria futura.

## 6. Integração de Release Multi-Fase

O planejamento de fases segue o fluxo issue-first definido em [AGENTS.md](../AGENTS.md) § Release Phase Issues: cada fase planejada da release tem uma issue no GitHub com escopo e critérios de aceitação, atribuída ao milestone da release. Os PRs de fase vinculam-se à sua issue com as palavras-chave `Closes` ou `Refs`. Todos os itens de fase do milestone devem estar fechados antes da criação do PR de agregação.

O passo de integração `dev → main` agrega muitos PRs anteriores. Ele está documentado como uma exceção justificada ao limite de 400 linhas por PR (veja AGENTS.md: scaffold/initial/bulk).

Ambos `dev` e `main` são protegidos por rulesets que exigem pull requests, histórico linear e `CI / required`. Push direto e fast-forward não são possíveis. O fluxo de release deve usar um PR de agregação `dev → main` em draft que é aprovado e mergeado via squash por um revisor.

### Checklist de pré-condições

- [ ] `gh run list --branch dev --workflow ci.yml --limit 1` — CI do dev verde
- [ ] `gh pr list --state open --base dev` — nenhum PR aberto para dev
- [ ] `gh pr list --state open --base main` — nenhum PR aberto para main
- [ ] `gh release list` — tag alvo ainda não existe
- [ ] Todos os itens de fase do milestone fechados; `.docs/` EN e PT-BR consistentes
- [ ] `dotnet build hokai.slnx -c Release -warnaserror && dotnet test hokai.slnx -c Release --no-build` — build e testes locais passam no SHA exato

### Dry-run (antes da integração)

Dispare o workflow de release manualmente contra o SHA exato do `dev` com `dry_run: true`. Isso compila, testa, empacota e gera checksums para as seis plataformas sem criar um draft release ou tag.

```bash
gh workflow run release.yml --ref dev -f version=0.2.0-alpha.1 -f dry_run=true
gh run watch <run_id> --exit-status
gh run download <run_id> -D dist
```

Validar a saída do dry-run:

```bash
# Verificar seis archives + quatro scripts + SHA256SUMS + SOURCE_SHA
ls dist/hokai-{linux-x64,linux-arm64,osx-x64,osx-arm64,win-x64,win-arm64}.{tar.gz,zip}
ls dist/{install,uninstall}.{sh,ps1} dist/SHA256SUMS dist/SOURCE_SHA

# Auto-validar checksums
cd dist && sha256sum -c SHA256SUMS

# Confirmar que SOURCE_SHA corresponde ao commit do dev
cat dist/SOURCE_SHA

# Verificar conteúdo dos archives (um executável cada, versão correta)
tar -xzf dist/hokai-linux-x64.tar.gz -O ./hokai --version
```

### Integração (após dry-run bem-sucedido)

1. Criar um PR de agregação `dev → main` em draft usando o template de PR.
2. Documentar a exceção de LOC, resultados do dry-run e escopo na descrição.
3. Anexar o milestone e labels obrigatórios.
4. CI deve ficar verde; marcar o PR como pronto para revisão.
5. Um revisor aprova e faz squash-merge do PR.

### Pós-merge (após merge do PR)

```bash
git fetch origin
git checkout main && git pull --ff-only origin main

# Verificar que main corresponde a dev após squash
git log main...dev --oneline

# Criar tag anotada no commit mergeado em main
git tag -a v0.2.0-alpha.1 -m "v0.2.0-alpha.1: Preview NativeAOT"

# Push apenas da tag (main já foi atualizado pelo squash merge)
git push origin v0.2.0-alpha.1
```

### Verificação pós-push

1. O workflow de release dispara com o push da tag.
2. As seis plataformas compilam, testam, empacotam e geram checksums.
3. Um draft release é criado com assets + attestation de proveniência.
4. Revisar o draft: verificar auto-validação do `SHA256SUMS`, `SOURCE_SHA`, lista de artefatos.
5. Publicar o draft — dispara o build da imagem Docker.
6. Verificar que `ghcr.io/tiagosantini/hokai:0.2.0-alpha.1` está disponível.
7. Executar `scripts/install.sh --version v0.2.0-alpha.1` como teste final.

## 7. Melhorias Futuras

- [ ] Code signing (Authenticode para Windows, notarização Apple para macOS)
- [ ] Publicação como .NET Global Tool no NuGet.org
- [ ] Fórmula Homebrew (`brew install hokai`)
- [ ] Repositório APT (`apt install hokai`)
- [ ] Pacote winget (`winget install hokai`)
