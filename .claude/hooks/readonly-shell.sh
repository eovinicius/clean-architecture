#!/usr/bin/env bash
# PreToolUse(Bash) dos subagentes que não editam (architecture-reviewer, security-reviewer, ci-watcher) — registrado no
# frontmatter de cada um. Só passa comando de leitura/consulta; guard-shell.sh continua valendo por cima.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

cmd="$(json_get '.tool_input.command')"
if [[ -z "$cmd" ]]; then
  [[ "$HOOK_INPUT" == *'"command"'* ]] && block "Não foi possível ler o comando (parser JSON). Bloqueado por segurança; instale jq ou node."
  exit 0
fi
normalized="$(printf '%s' "$cmd" | tr -s '[:space:]' ' ')"

deny() { block "Subagente somente leitura: $1 Comando: \`$normalized\`"; }

# Substituição de comando esconde o que de fato roda.
[[ "$normalized" == *'$('* || "$normalized" == *'`'* ]] && deny "sem \$(...) nem crases; rode os comandos separadamente."

# Segmentos (|, ;, &&, ||, &) fora de aspas, com o conteúdo entre aspas esvaziado: um --jq '... | ...' não vira segmento
# e um '>' dentro de padrão de grep não conta como redirecionamento.
segments=()
while IFS= read -r segment; do segments+=("$segment"); done < <(awk '{
  out = ""; q = ""
  for (i = 1; i <= length($0); i++) {
    c = substr($0, i, 1)
    if (q != "") {
      if (c == q) { q = ""; out = out c }
      else if (c == "\\" && q == "\"") i++
      continue
    }
    if (c == "\"" || c == "'"'"'") { q = c; out = out c; continue }
    if (c == "\\") { i++; continue }
    if (c == "&" && substr($0, i - 1, 1) == ">") { out = out c; continue }
    if (c == "|" || c == ";" || c == "&") c = "\n"
    out = out c
  }
  print out
}' <<<"$normalized")

for segment in "${segments[@]}"; do
  # Redirecionamento só para /dev/null ou entre descritores (2>&1).
  rest="$(sed -E 's/[0-9]*>>? *(\/dev\/null|&[0-9])//g' <<<"$segment")"
  [[ "$rest" == *'>'* ]] && deny "sem redirecionamento para arquivo."

  read -r -a words <<<"$segment"
  [[ ${#words[@]} -eq 0 ]] && continue
  args=" ${words[*]:1} "

  case "${words[0]}" in
    git)
      # "git -C dir log" / "git --no-pager log" → subcomando é a primeira palavra depois das opções globais.
      i=1
      while [[ "${words[i]:-}" == -C || "${words[i]:-}" == --no-pager ]]; do
        [[ "${words[i]}" == -C ]] && i=$((i + 1))
        i=$((i + 1))
      done
      sub="${words[i]:-}"
      case "$sub" in
        diff|log|status|show|rev-parse|rev-list|ls-files|ls-tree|merge-base|blame|grep|cat-file|describe|shortlog) ;;
        branch)
          [[ "$args" =~ \ (-d|-D|-m|-M|-c|-C|-f|--delete|--move|--copy|--force|--set-upstream-to|-u)\  ]] && deny "git branch só para listar."
          ;;
        *) deny "git $sub não é consulta." ;;
      esac
      [[ "$args" == *' --output'* ]] && deny "git com --output escreve arquivo."
      ;;
    gh)
      case "${words[1]:-} ${words[2]:-}" in
        "pr view"|"pr checks"|"pr diff"|"pr list"|"pr status"|"run view"|"run list"|"run watch") ;;
        *) deny "gh ${words[1]:-} ${words[2]:-} não é consulta (rerun/merge/edit exigem pedido do usuário)." ;;
      esac
      ;;
    dotnet)
      case "${words[1]:-}" in
        test|build|list|--version|--info|--list-sdks) ;;
        *) deny "dotnet ${words[1]:-} não é verificação." ;;
      esac
      ;;
    sed)
      [[ "$args" =~ \ (-i[^ ]*|--in-place[^ ]*)\  ]] && deny "sed -i edita arquivo."
      ;;
    sort)
      [[ "$args" =~ \ (-o|--output[^ ]*)\  ]] && deny "sort -o escreve arquivo."
      ;;
    find)
      [[ "$args" =~ \ -(delete|exec|execdir|ok|okdir|fprint|fprint0|fprintf|fls)\  ]] && deny "find só para localizar."
      ;;
    grep|egrep|rg|cat|head|tail|wc|ls|uniq|cut|tr|jq|echo|printf|diff|pwd|basename|dirname|cd|test|true|column|nl|comm|file|stat) ;;
    *) deny "'${words[0]}' fora da lista de leitura (.claude/hooks/readonly-shell.sh)." ;;
  esac
done

exit 0
