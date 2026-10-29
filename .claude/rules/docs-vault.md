---
paths:
  - "docs/**"
---
# Vault Obsidian (`docs/`)

O vault é a memória de longo prazo do projeto: abra `docs/` como vault no Obsidian. Entrada: `docs/Home.md`.

| Pasta | Conteúdo | ID |
| --- | --- | --- |
| `00-Inbox/` | Achados, dúvidas e rascunhos de sessões (`AAAA-MM-DD-assunto.md`) | — |
| `01-Product/` | Glossário (linguagem ubíqua), visão | — |
| `02-Specs/` | Specs SDD | `SPEC-NNNN` |
| `03-Architecture/` | Visão geral, observabilidade | — |
| `04-ADRs/` | Decisões arquiteturais | `ADR-NNNN` |
| `05-Engineering/` | Harness, diretrizes, práticas | — |
| `06-Runbooks/` | Procedimentos operacionais | `RB-NNN` |
| `99-Templates/` | Templates (não são notas) | — |

- Toda nota tem frontmatter YAML com `title` e `tags`; SPEC/ADR/RB também `id` e `status` (ADR: `date`). Validado pelo hook `validate-docs.sh`.
- Nome do arquivo = `ID-slug-em-kebab.md`. Próximo ID = maior existente + 1.
- Links internos em **wikilink** (`[[ADR-0002-clean-architecture]]`, nome do arquivo sem extensão); caminhos de código em `inline code`.
- **ADR `Accepted` é imutável**: para mudar, crie uma ADR nova com `supersedes` e, na antiga, altere **só** `status: Superseded` + `superseded_by` (o hook pede confirmação).
- Diagramas em Mermaid. Atualize os índices (`02-Specs/_Index.md`, `04-ADRs/_Index.md`) ao criar notas.
- Não copie código ou contratos para o vault — referencie o caminho.
- Não edite `docs/.obsidian/` sem pedido (configuração do usuário).
