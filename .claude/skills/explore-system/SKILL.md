---
name: explore-system
description: Investiga e explica como o sistema funciona de fato, sem alterar nada — rastreia um fluxo ou requisição ponta a ponta entre os repositórios da pasta workspace (Gateway, BFF do Gateway, Gateway da API, API, Documentação) ou dentro de um único repositório, mapeando rotas, contratos e DTOs, autenticação, transformações, tratamento de erros, regras de negócio e testes, com cada conclusão apoiada em arquivo:linha e as lacunas declaradas. Use antes de propor ou planejar uma mudança e sempre que o usuário perguntar como algo funciona, por onde uma requisição passa, quem chama quem, onde fica uma regra, qual o impacto de alterar um contrato, ou quiser diagnosticar pelo código um problema que atravessa serviços — mesmo sem dizer "explorar". Para reproduzir um erro em runtime com logs e traces, use investigate-issue.
argument-hint: '<fluxo, funcionalidade ou problema, ex.: "consulta de previdência do Gateway até a API">'
---

# explore-system

Objetivo: **explicar o comportamento real com evidência**, para que a decisão seguinte (planejar, corrigir, especificar) parta de fatos. A skill não implementa nada.

**Somente leitura.** Use `Read`, `Grep`, `Glob` e consultas git (`log`, `show`, `blame`, `grep`, `status`, `diff`). Não edite nem crie arquivos e não mude o estado dos repositórios (`checkout`, `switch`, `pull`, `fetch`, `stash`, `restore`): cada repositório pode estar numa branch com trabalho em andamento de outra pessoa. Build, testes ou subir a aplicação só se o usuário pedir — aqui a análise é estática.

## 1. Delimite a pergunta

Fluxo, funcionalidade ou sintoma; ponto de entrada conhecido (rota, tela, mensagem de erro, termo de negócio); e o que a resposta precisa permitir decidir. Se a ambiguidade mudar por onde começar, pergunte uma vez; senão, declare sua interpretação e siga.

## 2. Inventarie os repositórios (antes de abrir código)

- Existe a pasta `workspace` na raiz? Cada subpasta com `.git` é um repositório. Não existe? O repositório atual é o único escopo.
- Para cada candidato, de forma barata: papel (pelo README e pelo manifesto — `*.csproj`, `package.json`, `pom.xml`, `go.mod`… —, não só pelo nome da pasta), `git -C <repo> log -1 --format='%h %cs %s'` e `git -C <repo> status --short` (alterações locais mudam o que você está lendo).
- Papel não confirmado ⇒ "não identificado". Repositório esperado ausente ⇒ lacuna no relatório, nunca suposição.
- Se o repositório tiver `CLAUDE.md`/`AGENTS.md` próprio, use-o como mapa das convenções e pastas dele.

## 3. Rastreie a partir de uma âncora, um salto por vez

Comece pelo ponto de entrada mais próximo da pergunta e avance só pelo que as evidências apontam. Ler repositórios inteiros gasta contexto e afoga o que importa.

- **Âncora**: busca dirigida pelo literal mais específico (rota, nome de endpoint, chave de configuração, código de erro, termo de negócio e sinônimos), com `Grep` restrito ao repositório da vez e por tipo de arquivo.
- **Em cada componente**, responda: como a requisição entra (rota/handler, middlewares, filtros, autenticação e autorização); o que é validado; como os dados mudam (DTO recebido → DTO enviado, mapeamentos, headers propagados ou descartados); para onde sai (cliente HTTP, rota de proxy/gateway, fila); e como os erros voltam (status mapeados, timeout, retry, fallback).
- **Salto entre repositórios**: a URL, rota ou nome de serviço de saída. Resolva-o na configuração (appsettings, variáveis, rotas do gateway, service discovery) e procure a rota correspondente no repositório seguinte. Valor que só existe por ambiente (variável não versionada, secret) ⇒ "não confirmável" e diga onde ele seria definido.
- A ordem Gateway → BFF do Gateway → Gateway da API → API é hipótese de partida; vale o que as chamadas mostram (camada pulada, chamadas extras, assíncronas).
- **Na API**: onde mora a regra de negócio (entidade, handler, query/SQL) e quais persistências ou integrações externas o fluxo toca.
- **Testes**: procure pelos nomes de rota, handler e DTO nos projetos de teste e por contratos (OpenAPI, Pact, coleções), se existirem. Não encontrou ⇒ diga onde procurou.
- **Documentação**: leia depois do código e compare; divergência entre doc e código é achado, e o código vence como descrição do comportamento atual.
- Varredura ampla (convenção de nomes desconhecida, dezenas de candidatos) ⇒ delegue ao subagente `Explore` pedindo só a conclusão com caminhos, para preservar o contexto.

Pare quando a pergunta estiver respondida; não mapeie o que não foi perguntado.

## 4. Classifique cada afirmação

- **Confirmado**: lido no código ou na configuração, com `arquivo:linha` e símbolo.
- **Inferido**: deduzido de nome, padrão ou convenção — diga de quê.
- **Não verificado**: sem acesso ou sem evidência — diga o que faltou e como confirmar.

Comentário, nome de classe ou documentação sozinhos não confirmam comportamento. Use caminhos relativos à raiz do harness (`workspace/<repo>/...`), para que sejam clicáveis.

## 5. Relatório

Omita a seção que não se aplica à pergunta. Em investigação de um único repositório, o fluxo é a cadeia interna (endpoint → handler → domínio → persistência).

```markdown
## <pergunta em uma linha>
**Escopo**: <repo> (<branch> @ <commit>, alterações locais: sim/não) · ausentes ou não lidos: …

### Fluxo em ordem de execução
1. **<componente>** — `<rota>` → `<arquivo>:<linha>` `<Símbolo>`: o que faz, o que valida, para onde encaminha. *(Confirmado)*
2. …

### Responsabilidades
| Componente | Papel neste fluxo | Evidência |

### Contratos e transformações
<DTO por salto: campos renomeados, descartados ou enriquecidos>

### Autenticação, validação e erros
### Regras de negócio
### Testes que cobrem o fluxo
### Lacunas, divergências e riscos
- Não verificado: <o quê> — <como confirmar>
- Doc × código: …
```

Feche com, no máximo, um próximo passo sugerido (spec, correção, pergunta a alguém). A skill não grava o relatório: se o usuário quiser guardá-lo, ele pede.
