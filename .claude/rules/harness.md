---
paths:
  - ".claude/**"
  - "scripts/agents/**"
  - ".mcp.json"
  - "AGENTS.md"
  - "CLAUDE.md"
---
# Manutenção do Agent Harness

Mapa e princípios: `docs/05-Engineering/Agent-Harness.md`.

- **Hook alterado ⇒ caso de teste** em `scripts/agents/verify-harness.sh` (`expect <exit> <hook> <payload> <rótulo>`): bloqueio novo ganha um `expect 2`; o comando legítimo vizinho ganha um `expect 0`, para pegar falso positivo. Regra que depende da branch usa o repositório temporário em `development` do próprio script.
- Hooks bash portáveis (Git Bash no Windows, bash no Linux/macOS): sem `sed` com flag `I`, sem `\n` na substituição do `sed`, caminhos via `relpath`/`to_posix` do `lib.sh`. Contrato de saída: `0` segue, `2` bloqueia (stderr volta ao agente); erro de leitura da entrada **bloqueia**, não libera.
- **Permissões simétricas**: toda regra `Bash(...)` em `.claude/settings.json` tem a equivalente `PowerShell(...)`.
- **MCP**: `.mcp.json` só tem servidor sem segredo, com versão fixada; servidor com chave vai para o escopo `local` de cada dev (documentado em `Agent-Harness.md`).
- Convenção mudou? Atualize **um** dono (rule ou skill) e, se for invariante, a linha do `AGENTS.md`. Não duplique texto entre arquivos. Donos: propriedade do recurso e legado do template → `security.md`; slice, validator e contrato de endpoint → `architecture.md`. Skills e agents apontam para o dono em vez de repetir a regra.
- **Caminhos citados** em `code` nas rules, skills e agents precisam existir: o `verify-harness.sh` checa. Arquivo a ser criado usa placeholder (`{Feature}`), não um nome concreto.
- **Idioma das skills**: as do template (`add-*`, `ca-review`, `bootstrap`) ficam em inglês, para continuarem reaproveitáveis fora deste repositório; as do harness/SDD (`write-spec`, `implement-spec`, `create-adr`…) e os agents ficam em pt-BR, como o vault. Skill nova segue o grupo a que pertence.
- **Subagente que não edita** registra `readonly-shell.sh` como hook `PreToolUse` de Bash no próprio frontmatter. Skill com efeito destrutivo ou amplo (ex.: `bootstrap`) leva `disable-model-invocation: true`: só roda por `/nome`.
- Antes de concluir: `bash scripts/agents/verify-harness.sh` com exit 0.
