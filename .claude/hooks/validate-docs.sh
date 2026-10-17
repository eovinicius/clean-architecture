#!/usr/bin/env bash
# PostToolUse(Edit|Write) — valida o frontmatter das notas do vault (docs/). exit 2 devolve o problema ao agente.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

problems=""
while IFS= read -r raw; do
  rel="$(relpath "$raw")"
  [[ "$rel" == docs/*.md ]] || continue
  [[ "$rel" == docs/99-Templates/* || "$rel" == docs/.obsidian/* ]] && continue
  file="$HOOK_PROJECT_DIR/$rel"
  [[ -f "$file" ]] || continue

  if ! head -1 "$file" | tr -d '\r' | grep -q '^---$'; then
    problems+=$'\n'"- $rel: frontmatter YAML ausente (a primeira linha deve ser ---)."
    continue
  fi
  fm="$(tr -d '\r' < "$file" | awk 'NR==1{next} /^---$/{exit} {print}')"
  for key in title tags; do
    grep -qE "^$key:" <<<"$fm" || problems+=$'\n'"- $rel: campo obrigatório '$key' ausente."
  done

  id="$(grep -m1 -E '^id:' <<<"$fm" | sed -E 's/^id: *//; s/ *$//')"
  check_id() {
    local prefix="$1" pattern="$2"
    [[ -n "$id" ]] || { problems+=$'\n'"- $rel: '$prefix' sem campo 'id'."; return; }
    [[ "$id" =~ $pattern ]] || problems+=$'\n'"- $rel: id '$id' fora do formato $prefix."
    [[ "$(basename "$rel")" == "$id"-* ]] || problems+=$'\n'"- $rel: o nome do arquivo deve começar com o id ($id-)."
  }
  check_status() {
    grep -qE "^status: *($1) *(#.*)?$" <<<"$fm" || problems+=$'\n'"- $rel: status inválido (use $1)."
  }

  case "$rel" in
    docs/02-Specs/SPEC-*.md)
      check_id "SPEC-NNNN" '^SPEC-[0-9]{4}$'
      check_status 'Draft|Review|Approved|In Progress|Implemented|Deprecated'
      for key in created updated; do grep -qE "^$key:" <<<"$fm" || problems+=$'\n'"- $rel: spec sem '$key'."; done
      ;;
    docs/04-ADRs/ADR-*.md)
      check_id "ADR-NNNN" '^ADR-[0-9]{4}$'
      check_status 'Proposed|Accepted|Superseded|Deprecated'
      grep -qE '^date:' <<<"$fm" || problems+=$'\n'"- $rel: ADR sem 'date'."
      ;;
    docs/06-Runbooks/RB-*.md)
      check_id "RB-NNN" '^RB-[0-9]{3}$'
      check_status 'Draft|Approved|Deprecated'
      ;;
  esac
done < <(touched_paths | sort -u)

[[ -n "$problems" ]] && block "Nota do vault fora do padrão (.claude/rules/80-docs-vault.md):$problems"
exit 0
