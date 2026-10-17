# 00 · Núcleo (sempre carregada)

Regras de maior precedência. Em conflito com outra rule ou skill, **estas vencem**. Versão integral e comentada: `docs/05-Engineering/Agent-Guidelines.md`.

1. **Precedência**: pedido explícito do usuário › `AGENTS.md`/rules/skills › o que o build impõe (analisadores, `.editorconfig`, `ArchitectureTests`) › preferências do agente. Conflito? Siga a fonte mais alta e mencione no relatório.
2. **Sem LLM em runtime.** Nunca adicionar SDK/cliente de IA (`Anthropic`, `OpenAI`, `Azure.AI.*`, `Microsoft.SemanticKernel`, `Microsoft.Extensions.AI*`, `OllamaSharp`…) em `app/` ou `Directory.Packages.props`, nem chaves de IA em config (ADR-0003). O hook `guard-shell.sh` bloqueia `dotnet add package` desses pacotes.
3. **Investigue antes de escrever.** Ache 2–3 exemplos análogos (slice, endpoint, teste) e replique a forma. Procure utilitário, catálogo de erros, helper de teste ou chave de cache existente antes de criar. Confirme nomes de símbolos e comandos lendo o código.
4. **Não invente.** Regra de negócio ambígua → pergunte ou registre em `docs/00-Inbox/`. Termo de negócio → `docs/01-Product/Glossary.md`. Versão de pacote/API desconhecida → MCP `nuget`/`microsoft-learn`/`context7`.
5. **Siga o padrão predominante**, não o ideal teórico. Não "normalize" exceções nem legado fora do escopo.
6. **Menor mudança correta.** Sem refatoração, renomeação, reformatação ou reorganização oportunista. Viu algo errado fora do escopo? **Reporte, não corrija.**
7. **Prove.** Mudança de comportamento vem com teste que falharia sem ela. Antes de declarar pronto: `dotnet build CleanArchitecture.slnx` + testes relevantes verdes (o hook `stop-verify.sh` confere). Nunca enfraqueça, pule ou apague testes para passar.
8. **Decisão estrutural vira ADR** (dependência, padrão ou tecnologia nova) — skill `create-adr`.
9. **Destrutivo ou externo exige confirmação**: apagar dados/arquivos, reescrever histórico, push, publicar. Commit só quando pedido.
10. **Idioma**: código, identificadores, logs e mensagens de erro da API em **inglês**; vault (`docs/`), specs, ADRs e comentários de regra de negócio em **pt-BR**; commits em Conventional Commits com descrição em pt-BR (como no histórico).
11. **Relatório honesto**: o que foi feito, o que foi verificado (com resultado) e o que **não** foi verificado.
