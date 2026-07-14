# Hokai — Processo de Release

> Como o Hokai é compilado, empacotado e distribuído.

## 1. Versionamento

O Hokai segue Versionamento Semântico (MAJOR.MINOR.PATCH).

- Tags do Git devem corresponder a `v*.*.*` (ex: `v1.0.0`).
- A tag deve apontar para um commit no branch `main`.
- Metadados de versão são incorporados durante a compilação via `-p:Version=<tag>`.

## 2. Artefatos de Build

Cada release produz seis binários self-contained single-file:

| Asset | Plataforma |
|---|---|
| `hokai-linux-x64.tar.gz` | Linux x86_64 |
| `hokai-linux-arm64.tar.gz` | Linux ARM64 |
| `hokai-osx-x64.tar.gz` | macOS x86_64 |
| `hokai-osx-arm64.tar.gz` | macOS ARM64 |
| `hokai-win-x64.zip` | Windows x86_64 |
| `hokai-win-arm64.zip` | Windows ARM64 |

Todos os archives contêm um único executável (`hokai` no Unix, `hokai.exe` no Windows) compilado com `PublishSingleFile=true` e `PublishSelfContained=true`.

Os seis RIDs são declarados na propriedade `RuntimeIdentifiers` do projeto e rastreados em um único `packages.lock.json` commitado. A CI restaura em modo locked no Ubuntu, Windows e macOS.

Assets adicionais:

| Asset | Descrição |
|---|---|
| `install.sh` | Instalador Unix (Linux + macOS) |
| `uninstall.sh` | Desinstalador Unix |
| `install.ps1` | Instalador Windows |
| `uninstall.ps1` | Desinstalador Windows |
| `SHA256SUMS` | Checksums SHA-256 de todos os archives e scripts |

## 3. Fluxo de Release

1. O push de uma tag `vX.Y.Z` para `main` aciona o workflow de release.
2. O workflow valida SemVer e que o commit pertence a `main`.
3. Testes passam em Linux.
4. Os RIDs são publicados e o executável é validado (`--help` retorna com sucesso).
5. Um GitHub Release em draft é criado com todos os assets e checksums.
6. A release é publicada manualmente após revisão final.
7. Publicar a GitHub Release aciona o build da imagem GHCR.

## 4. Imagens Docker

Imagens são publicadas em `ghcr.io/tiagosantini/hokai` com as tags:

| Tag | Exemplo |
|---|---|
| Versão completa | `1.2.3` |
| Versão menor | `1.2` |
| Versão maior | `1` |
| Última estável | `latest` |

As imagens suportam `linux/amd64` e `linux/arm64`.

Pré-releases recebem apenas a tag de versão completa e não movem `latest`.

## 5. Assinatura e Proveniência

- Workflow de release gera attestations para proveniência da supply chain.
- Todos os assets incluem `SHA256SUMS` para verificação de integridade.
- Code signing (Authenticode no Windows, notarização no macOS) é planejado como melhoria futura.

## 6. Melhorias Futuras

- [ ] Code signing (Authenticode para Windows, notarização Apple para macOS)
- [ ] Publicação como .NET Global Tool no NuGet.org
- [ ] Fórmula Homebrew (`brew install hokai`)
- [ ] Repositório APT (`apt install hokai`)
- [ ] Pacote winget (`winget install hokai`)
