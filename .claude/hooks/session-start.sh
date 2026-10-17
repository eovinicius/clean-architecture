#!/usr/bin/env bash
# SessionStart — injeta estado dinâmico e enxuto (git, SDK, Docker, specs ativas). Conteúdo estático fica no AGENTS.md.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
cd "$HOOK_PROJECT_DIR" || exit 0

branch="$(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo '?')"
changed="$(git status --porcelain 2>/dev/null | wc -l | tr -d ' ')"
base="development"
[[ "$branch" == hotfix/* ]] && base="main"
ahead="$(git rev-list --count "$base..HEAD" 2>/dev/null || echo '?')"

sdk="$(dotnet --version 2>/dev/null || echo 'não encontrado')"
pinned="$(grep -oE '"version" *: *"[^"]+"' global.json 2>/dev/null | sed -E 's/.*"([^"]+)"$/\1/')"

docker_ok="não"
docker info >/dev/null 2>&1 && docker_ok="sim"

active_specs=""
for f in docs/02-Specs/SPEC-*.md; do
  [[ -f "$f" ]] || continue
  status="$(grep -m1 -E '^status:' "$f" | sed -E 's/^status: *//; s/ *#.*$//')"
  case "$status" in
    Approved|"In Progress"|Review) active_specs+=$'\n'"  - $(basename "$f" .md) [$status]" ;;
  esac
done

spec_hint=""
if [[ "$branch" =~ (SPEC-[0-9]{4}) ]]; then
  match="$(ls docs/02-Specs/"${BASH_REMATCH[1]}"-*.md 2>/dev/null | head -1)"
  [[ -n "$match" ]] && spec_hint=$'\n'"Branch ligada a ${BASH_REMATCH[1]} → leia $match antes de editar código."
fi

branch_hint=""
case "$branch" in
  main|development) branch_hint=$'\n'"Você está em '$branch' (protegida). Para mudanças, crie feature/<slug> ou bugfix/<slug> a partir de development." ;;
esac

ctx="[harness] branch=$branch · commits à frente de $base=$ahead · arquivos alterados=$changed · .NET SDK=$sdk (global.json: ${pinned:-?}) · Docker=$docker_ok${branch_hint}${spec_hint}
Specs ativas:${active_specs:-$'\n'  - (nenhuma)}
Lembretes: spec antes de feature relevante · testes antes de código · sem LLM em runtime · build + testes antes de concluir."
[[ "$docker_ok" == "não" ]] && ctx+=$'\n'"Docker indisponível: IntegrationTests (Testcontainers) e docker compose não rodarão localmente."

emit_context "SessionStart" "$ctx"
exit 0
