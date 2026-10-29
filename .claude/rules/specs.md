---
paths:
  - "docs/02-Specs/**"
  - "docs/01-Product/**"
---
# Specs (SDD leve no vault)

Decisão: ADR-0003. Skills: `write-spec` (criar/evoluir), `implement-spec` (implementar). Revisão: subagente `spec-reviewer`.

## Quando a spec é obrigatória

- **Sim**: feature nova, mudança de regra de negócio, endpoint novo ou mudança de contrato HTTP existente.
- **Não**: bugfix sem mudança de comportamento esperado, refatoração interna, ajuste de infraestrutura/ferramentas.

## Formato

- Arquivo `docs/02-Specs/SPEC-NNNN-slug-em-kebab.md` a partir de `docs/99-Templates/TPL-Spec.md`. Próximo ID = maior existente + 1.
- Frontmatter: `id`, `title`, `status`, `tags`, `adrs`, `created`, `updated` (validado pelo hook `validate-docs.sh`).
- Status: `Draft → Review → Approved → In Progress → Implemented` (ou `Deprecated`). **Só humano** move para `Approved`.
- Regras de negócio `RN-n` atômicas; critérios `AC-n` em *Dado/Quando/Então*, cada um verificável por teste automatizado.
- Casos de erro citam o código (`"{EntityPlural}.{Reason}"`) e o status HTTP esperado. Há AC de autorização.
- Termos do glossário; termo novo ⇒ adicionar a `docs/01-Product/Glossary.md` no mesmo PR.
- Suposições vão para "Perguntas em aberto" — nunca escondidas no texto.

## Contrato HTTP

Não há YAML escrito à mão: a spec descreve operação, request/response e erros; o contrato real vem do código (`.WithSummary`/`.Produces*`) e é guardado por `IntegrationTests/Api/OpenApiDocumentTests.cs`. Mudança breaking em endpoint existente exige decisão explícita do usuário (e possivelmente ADR).
