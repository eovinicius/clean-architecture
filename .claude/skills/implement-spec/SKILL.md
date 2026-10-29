---
name: implement-spec
description: Implementa uma spec Approved do vault (docs/02-Specs) de ponta a ponta — plano, testes por critério de aceite, domínio, slice da Application, endpoint, migration, verificação e atualização do status da spec. Use quando o usuário pedir para implementar SPEC-NNNN.
argument-hint: SPEC-NNNN
---

# implement-spec

Entrada: `SPEC-NNNN`. Pré-condição: `status: Approved` (ou `In Progress`). Se estiver `Draft`/`Review` ⇒ pare e informe o usuário.

## 1. Contexto (progressivo)

- A spec inteira e os ADRs listados em `adrs:`.
- Do código, **somente** o que a spec toca: a entidade em `app/src/Domain/{Feature}/`, um slice análogo em `app/src/Application/{Feature}/` e o endpoint espelho em `app/src/Web.Api/Endpoints/{Feature}/`. As rules certas carregam sozinhas pelos caminhos.

## 2. Plano (antes de editar)

Mostre ao usuário uma tabela **AC → camada/arquivos → teste que prova**. Aguarde confirmação se houver: mudança breaking em endpoint existente, migration destrutiva, pacote novo ou decisão estrutural (esta pede ADR → skill `create-adr`).
Mude a spec para `status: In Progress` e atualize `updated:`.

## 3. Executar em fatias (uma AC por vez quando possível)

1. **Teste vermelho** no nível mais baixo que prova a AC (`Application.UnitTests` antes de `IntegrationTests`), com `// SPEC-NNNN AC-n`. Rode e mostre a falha.
2. **Domain**: entidade/erros/eventos novos via skill `add-entity` (ou edição pontual do catálogo `{Entity}Errors`). Cada `RN-n` de estado vira método da entidade, com `{Entity}Tests` (`.claude/rules/architecture.md`, Domain).
3. **Application + endpoint**: skill `add-feature` (command/query, handler, validator, endpoint, testes).
4. **Persistência**: modelo mudou ⇒ skill `add-migration` (revise o arquivo gerado).
5. **Observabilidade**, se a spec pedir sinais: skill `instrument-observability`.
6. Rode os testes da fatia até ficarem verdes antes de ir para a próxima AC.

## 4. Verificar

- `dotnet build CleanArchitecture.slnx` (warnings são erros).
- `dotnet test app/tests/Application.UnitTests` e `dotnet test app/tests/ArchitectureTests`.
- `dotnet test app/tests/IntegrationTests` se tocou API/persistência (precisa de Docker; sem Docker, diga isso no relatório).
- Cobertura da spec: `grep -rn "SPEC-NNNN" app/tests`. Toda AC precisa aparecer.

## 5. Fechar

1. `/review` (ou os subagentes `architecture-reviewer` + `security-reviewer`) e resolva os bloqueantes.
2. Spec → `status: Implemented`, `updated:` de hoje; atualize `docs/02-Specs/_Index.md`.
3. Sinais novos em `docs/03-Architecture/Observability.md`; termos novos no glossário.
4. Relatório: ACs cobertas (teste por AC), comandos rodados com resultado, o que ficou pendente. **Commit só se o usuário pedir.**
5. PR aberto (a pedido do usuário) ⇒ subagente `ci-watcher` acompanha o CI; falha ⇒ corrija a causa apontada antes de pedir merge.
