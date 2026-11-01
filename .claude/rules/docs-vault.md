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
| `05-Engineering/` | Harness, diretrizes, práticas | — |
| `06-Runbooks/` | Procedimentos operacionais | `RB-NNN` |
| `99-Templates/` | Templates (não são notas) | — |

- Toda nota tem frontmatter YAML com `title` e `tags`; SPEC/RB também `id` e `status`. Validado pelo hook `validate-docs.sh`.
- Nome do arquivo = `ID-slug-em-kebab.md`. Próximo ID = maior existente + 1.
- Links internos em **wikilink** (`[[SPEC-0001-arquivamento-de-tarefas]]`, nome do arquivo sem extensão); caminhos de código em `inline code`.
- Diagramas em Mermaid. Atualize o índice (`02-Specs/_Index.md`) ao criar specs.
- Não copie código ou contratos para o vault — referencie o caminho.
- Não edite `docs/.obsidian/` sem pedido (configuração do usuário).
