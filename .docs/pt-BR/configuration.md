# Hokai — Referência de Configuração

> Referência completa da configuração do Hokai, ordem de resolução e caminhos por plataforma.

**Documentos relacionados**: [Arquitetura](architecture.md) | [Instalação](installation.md) | [Daemonização](daemonization.md)

---

## 1. Schema

```json
{
  "Smtp": {
    "Host": "localhost",
    "Port": 25,
    "UseSsl": false,
    "Username": "",
    "Password": "",
    "FromAddress": "hokai@localhost",
    "ToAddresses": []
  },
  "DataDirectory": "Data",
  "RetentionDays": 30
}
```

| Chave | Tipo | Padrão | Descrição |
|---|---|---|---|
| `Smtp.Host` | string | `localhost` | Hostname do servidor SMTP |
| `Smtp.Port` | int | `25` | Porta do servidor SMTP |
| `Smtp.UseSsl` | bool | `false` | Habilitar SSL/TLS |
| `Smtp.Username` | string | `""` | Usuário para autenticação SMTP |
| `Smtp.Password` | string | `""` | Senha para autenticação SMTP |
| `Smtp.FromAddress` | string | `hokai@localhost` | Endereço de email do remetente |
| `Smtp.ToAddresses` | string[] | `[]` | Endereços de email dos destinatários |
| `DataDirectory` | string | `Data` | Onde os dados de endpoints e checks são armazenados |
| `RetentionDays` | int | `30` | Dias para manter registros individuais de check |

## 2. Resolução do Arquivo de Config

O Hokai resolve o arquivo de configuração usando a seguinte prioridade:

| Prioridade | Fonte | Descrição |
|---|---|---|
| 1 | `--config /caminho` (ou `-c /caminho`) | Argumento explícito da CLI. Erro se o arquivo não existir. |
| 2 | `HOKAI_CONFIG_PATH` | Variável de ambiente. Erro se o arquivo não existir. |
| 3 | Config canônico do SO | Local de config específico da plataforma, se presente no disco. |
| 4 | Adjacente ao executável | `appsettings.json` no mesmo diretório do executável. |

Se nenhum arquivo de config for encontrado, o Hokai usa os padrões em memória com `DataDirectory = "Data"` (relativo ao diretório de trabalho).

### Caminhos Canônicos por Plataforma

| Plataforma | Caminho de config | Diretório de dados |
|---|---|---|
| Linux | `/etc/hokai/appsettings.json` | `/var/lib/hokai/` |
| macOS | `~/Library/Application Support/Hokai/appsettings.json` | `~/Library/Application Support/Hokai/Data/` |
| Windows | `%ProgramData%\Hokai\appsettings.json` | `%ProgramData%\Hokai\Data\` |

## 3. Semântica do DataDirectory

- **Caminho relativo**: resolvido em relação ao diretório do arquivo de config (não ao diretório de trabalho).
- **Caminho absoluto**: usado como está.
- **Padrão**: quando não há arquivo de config, `Data` é resolvido em relação ao diretório de trabalho do processo.
- O diretório armazena dois arquivos JSON: `endpoints.json` (configuração dos endpoints) e `checks.json` (histórico de checks).
- O diretório é criado automaticamente na primeira escrita.
- A limpeza por retenção é executada a cada hora no modo daemon.

## 4. Configuração SMTP

- Notificações são enviadas apenas em transições de estado (UP → DOWN, DOWN → UP). O primeiro check de cada endpoint não dispara notificação.
- Se `ToAddresses` estiver vazio, nenhum email é enviado.
- Se `Username` estiver vazio, a autenticação SMTP é ignorada.
- Um `SmtpClient` é criado por mensagem; falhas são registradas em log sem retry.
- **Segurança**: Armazene credenciais em variáveis de ambiente ou .NET User Secrets, não no arquivo de config. O `appsettings.json` deve conter apenas valores placeholder em repositórios commitados.

## 5. Recarregamento de Configuração

- O arquivo de config é lido uma vez na inicialização. Alterações nas configurações SMTP ou `RetentionDays` exigem reinicialização do processo.
- A configuração de endpoints (`endpoints.json`) é recarregada a cada 30 segundos pelo daemon.
- Adicionar ou remover endpoints via CLI tem efeito automaticamente em até 30 segundos.

## 6. Configuração no Docker

Ao executar no Docker, forneça a configuração via bind-mount:

```bash
docker run -v ./minha-config.json:/etc/hokai/appsettings.json:ro \
           -v hokai-data:/var/lib/hokai \
           ghcr.io/tiagosantini/hokai:latest
```

Defina `DataDirectory` como um caminho absoluto dentro do volume:

```json
{
  "DataDirectory": "/var/lib/hokai",
  "Smtp": { "Host": "smtp.exemplo.com", "...": "..." },
  "RetentionDays": 30
}
```

O template padrão `docker/appsettings.json` já está pré-configurado para este setup.

## 7. Config Ausente ou Inválida

| Cenário | Comportamento |
|---|---|
| Arquivo de config é JSON válido | Configurações são vinculadas e validadas no ponto de uso. |
| Arquivo de config ausente | Padrões em memória são usados. Sem erro. |
| Arquivo de config é JSON malformado | Exceção é lançada no carregamento. Aplicação encerra com erro. |
| `--config` explícito aponta para arquivo ausente | Aplicação encerra com erro. |
| `HOKAI_CONFIG_PATH` aponta para arquivo ausente | Aplicação encerra com erro. |
| Chaves JSON desconhecidas | Ignoradas pelo binding de configuração. |

---

## 8. Melhorias Futuras

- [ ] Binding de variáveis de ambiente para chaves individuais (ex: `HOKAI_SMTP__HOST`)
- [ ] Hot-reload de configuração para SMTP e retenção
- [ ] Armazenamento criptografado da senha SMTP
- [ ] Validação de configuração na inicialização
