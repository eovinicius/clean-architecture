---
title: Pre-flight (super-board)
tags: [engineering, super-board]
---

# Pre-flight — super-board lint de 2026-11-21

Board: [board] clean-architecture (#4, eovinicius). Colunas ativas (Ready, Building, QA, Review): **0 issues**.

🔑 Credenciais que o loop vai precisar:
(nenhuma — nenhum card ativo pede credencial)

🛠 Ferramentas que o loop vai precisar:
[✓] gh CLI autenticado (verificado)
[✓] .NET SDK 10.0.401 (verificado; `global.json` pede 10.0.300 com roll-forward)
[✓] Docker instalado (29.9.2) — daemon parado; só `IntegrationTests` dependem dele e rodam no CI, fora do merge gate local

🌐 Ambiente:
[✓] Alvo: repositório local (`target.type = repo`), sem URL a checar
