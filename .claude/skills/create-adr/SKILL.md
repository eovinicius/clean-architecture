---
name: create-adr
description: Registra uma decisão arquitetural (ADR) no vault Obsidian em docs/04-ADRs, com contexto, alternativas reais e consequências verificáveis. Use ao introduzir dependência, padrão, tecnologia ou mudança estrutural, ao substituir uma decisão existente, ou quando o usuário pedir para registrar uma decisão.
argument-hint: <decisão a registrar>
---

# create-adr

1. **Já existe decisão sobre o tema?** `grep -il "<tema>" docs/04-ADRs/*.md`. Se existe e está `Accepted`, a nova ADR **substitui** a antiga (`supersedes`) — nunca reescreva a antiga.
2. **ID**: maior `ADR-NNNN` + 1. Arquivo `docs/04-ADRs/ADR-NNNN-<slug-em-ingles-kebab>.md` a partir de `docs/99-Templates/TPL-ADR.md`, com `status: Proposed` e `date:` de hoje.
3. **Escreva**:
   - **Contexto**: forças concretas (requisito, volume, restrição, time). Cite specs com `[[SPEC-...]]`.
   - **Decisão**: frase afirmativa e verificável ("Vamos usar X para Y, configurado com Z").
   - **Alternativas**: ao menos duas reais, cada uma com o motivo da rejeição. Versões e capacidades de bibliotecas confirmadas via MCP (`nuget`, `microsoft-learn`, `context7`), não de memória.
   - **Consequências**: positivas, negativas e riscos aceitos.
   - **Como verificar**: teste de arquitetura (`app/tests/ArchitectureTests`), hook, teste automatizado ou item de checklist do `ca-review`.
4. **Substituição**: na ADR antiga, altere **só** `status: Superseded` e `superseded_by: "[[ADR-NNNN-...]]"`. O hook `protect-files.sh` vai pedir confirmação: é esperado.
5. Atualize `docs/04-ADRs/_Index.md` e o campo `adrs:` das specs afetadas.
6. Se a decisão é verificável por máquina, proponha o teste ou hook correspondente (ex.: regra nova em `ArchitectureTests/Layers/LayerTests.cs`). Não o implemente sem o usuário pedir.
7. `Proposed → Accepted` só depois da aprovação humana no PR.
