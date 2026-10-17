#!/usr/bin/env bash
# UserPromptSubmit — (1) bloqueia prompts com segredos; (2) resolve SPEC-/ADR-/RB- citados em ponteiros para o vault
# (progressive disclosure: injeta caminho + status, não o conteúdo).
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
cd "$HOOK_PROJECT_DIR" || exit 0

prompt="$(json_get '.prompt')"
[[ -z "$prompt" ]] && exit 0

secret_patterns=(
  'gh[pousr]_[A-Za-z0-9]{36,}'                 # GitHub tokens
  'github_pat_[A-Za-z0-9_]{40,}'               # GitHub fine-grained PAT
  'sk-ant-[A-Za-z0-9_-]{20,}'                  # Anthropic
  'sk-(proj-)?[A-Za-z0-9_-]{32,}'              # OpenAI
  '(AKIA|ASIA)[0-9A-Z]{16}'                    # AWS access key
  '-----BEGIN [A-Z ]*PRIVATE KEY-----'         # chaves privadas
  'xox[baprs]-[A-Za-z0-9-]{10,}'               # Slack
  'eyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}'   # JWT
)
for pattern in "${secret_patterns[@]}"; do
  if grep -qE -- "$pattern" <<<"$prompt"; then
    block "O prompt parece conter um segredo (token, chave ou JWT). Remova-o — segredos nunca devem ser enviados ao agente. Se a credencial for real, revogue-a."
  fi
done

# Connection string com senha real. Permitidos: a senha de dev local do compose (postgres) e placeholders (<x>, {x}, ${X}, ***).
while read -r value; do
  [[ -z "$value" ]] && continue
  case "$value" in
    postgres|\<*|\{*|\$*|\**) continue ;;
  esac
  block "O prompt contém uma connection string com senha. Use dotnet user-secrets e referencie a chave, sem colar o valor."
done < <(grep -oiE '(password|pwd)=[^;"'"'"' ]+' <<<"$prompt" | sed -E 's/^[^=]+=//')

refs="$(grep -oE '(SPEC|ADR)-[0-9]{4}|RB-[0-9]{3}' <<<"$prompt" | sort -u)"
[[ -z "$refs" ]] && exit 0

ctx="Notas do vault citadas no pedido (leia antes de agir):"
while read -r ref; do
  file="$(ls docs/02-Specs/"$ref"-*.md docs/04-ADRs/"$ref"-*.md docs/06-Runbooks/"$ref"-*.md 2>/dev/null | head -1)"
  if [[ -n "$file" ]]; then
    status="$(grep -m1 -E '^status:' "$file" | sed -E 's/^status: *//; s/ *#.*$//')"
    ctx+=$'\n'"- $ref → $file (status: ${status:-?})"
  else
    ctx+=$'\n'"- $ref → NÃO ENCONTRADO no vault. Não invente o conteúdo; pergunte ao usuário."
  fi
done <<<"$refs"

emit_context "UserPromptSubmit" "$ctx"
exit 0
