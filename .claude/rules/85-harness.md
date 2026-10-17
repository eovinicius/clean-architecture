---
paths:
  - ".claude/**"
  - "scripts/agents/**"
  - ".mcp.json"
  - "AGENTS.md"
  - "CLAUDE.md"
---
# 85 · Manutenção do Agent Harness

Mapa e princípios: `docs/05-Engineering/Agent-Harness.md`.

- **Hook alterado ⇒ caso de teste** em `scripts/agents/verify-harness.sh` (`expect <exit> <hook> <payload> <rótulo>`): bloqueio novo ganha um `expect 2`; o comando legítimo vizinho ganha um `expect 0`, para pegar falso positivo. Regra que depende da branch usa o repositório temporário em `development` do próprio script.
- Hooks bash portáveis (Git Bash no Windows, bash no Linux/macOS): sem `sed` com flag `I`, sem `\n` na substituição do `sed`, caminhos via `relpath`/`to_posix` do `lib.sh`. Contrato de saída: `0` segue, `2` bloqueia (stderr volta ao agente); erro de leitura da entrada **bloqueia**, não libera.
- **Permissões simétricas**: toda regra `Bash(...)` em `.claude/settings.json` tem a equivalente `PowerShell(...)`.
- **MCP**: `.mcp.json` só tem servidor sem segredo, com versão fixada; servidor com chave vai para o escopo `local` de cada dev (documentado em `Agent-Harness.md`).
- Convenção mudou? Atualize **um** dono (rule ou skill) e, se for invariante, a linha do `AGENTS.md`. Não duplique texto entre arquivos.
- Antes de concluir: `bash scripts/agents/verify-harness.sh` com exit 0.
