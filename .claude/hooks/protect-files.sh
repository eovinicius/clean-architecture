#!/usr/bin/env bash
# PreToolUse(Edit|Write|NotebookEdit) — protege segredos, dados locais, artefatos gerados pelo EF, migrations mergeadas e ADRs aceitos.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

while IFS= read -r raw; do
  [[ -z "$raw" ]] && continue
  rel="$(relpath "$raw")"
  base="$(basename "$rel")"

  # Segredos, certificados e estado local
  case "$rel" in
    .env.example|*/.env.example) ;;
    .env|.env.*|*/.env|*/.env.*) block "Edição de '$rel' bloqueada: segredos não são gerenciados por agentes (use dotnet user-secrets)." ;;
    *.pfx|*.pem|*.key|*.p12|*/secrets/*|secrets/*|*/secrets.json|secrets.json) block "Edição de '$rel' bloqueada (segredo/certificado)." ;;
    .git/*|*/.git/*) block "Edição direta em .git é proibida." ;;
    .containers/*) block "'.containers/' guarda os dados locais do Postgres — não é editável por agentes." ;;
  esac

  # Artefatos gerados pelo dotnet ef
  case "$base" in
    *ModelSnapshot.cs) block "'$base' é gerado pelo dotnet ef. Mude o modelo/configuração e gere uma migration (skill add-migration)." ;;
  esac

  # Migrations que já estão em main ou development são imutáveis (refs remotas cobrem a branch local desatualizada)
  if [[ "$rel" == */Migrations/*.cs ]]; then
    for branch in main development origin/main origin/development; do
      if git -C "$HOOK_PROJECT_DIR" cat-file -e "$branch:$rel" 2>/dev/null; then
        block "Migration '$base' já está em '$branch' e é imutável. Crie uma nova migration (skill add-migration)."
      fi
    done
  fi

  # ADR aceita é imutável — só status/superseded_by, com confirmação humana
  if [[ "$rel" == docs/04-ADRs/ADR-*.md && -f "$HOOK_PROJECT_DIR/$rel" ]] && grep -qE '^status: *Accepted' "$HOOK_PROJECT_DIR/$rel"; then
    ask_user "A ADR '$base' está Accepted (imutável). Só altere status/superseded_by ao substituí-la por uma nova ADR. Confirmar a edição?"
  fi
done < <(touched_paths | sort -u)

exit 0
