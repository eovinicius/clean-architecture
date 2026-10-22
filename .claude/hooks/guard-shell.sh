#!/usr/bin/env bash
# PreToolUse(Bash|PowerShell) — bloqueia comandos destrutivos, quebra do Gitflow, vazamento de segredos e pacotes de IA no runtime.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

cmd="$(json_get '.tool_input.command')"
if [[ -z "$cmd" ]]; then
  # Fail-closed: há um comando na entrada, mas o parser JSON não conseguiu lê-lo.
  [[ "$HOOK_INPUT" == *'"command"'* ]] && block "Não foi possível ler o comando (parser JSON). Bloqueado por segurança; instale jq ou node."
  exit 0
fi
normalized="$(printf '%s' "$cmd" | tr -s '[:space:]' ' ')"
# "git -C dir push" / "git -c k=v push" → "git push": opções globais não escapam das regras de git.
normalized="$(sed -E 's/(^|[;&|( ])git( +-[Cc] +[^ ]+)+ /\1git /g' <<<"$normalized")"

# Regras de "qual comando é este" avaliam cada segmento (&&, ||, ;, |) a partir do início,
# para que o texto de um argumento (ex.: git log --grep="git push --force") não dispare a regra.
segments=()
while IFS= read -r segment; do segments+=("$segment"); done < <(awk '{ gsub(/&&|\|\||;|\|/, "\n"); print }' <<<"$normalized")
prefix='^[ (]*(cmd(\.exe)? +/c +)?["'"'"']?(sudo +)?([a-z_][a-z0-9_]*=[^ ]* +)*'

deny() { block "$1 Comando: \`$normalized\`"; }
matches() { grep -qiE -- "$1" <<<"$normalized"; }
command_is() {
  local segment
  for segment in "${segments[@]}"; do
    grep -qiE -- "$prefix$1" <<<"$segment" && return 0
  done
  return 1
}

# 1. Remoção destrutiva (raiz, home, diretório atual, dados locais do Postgres em .containers/)
dangerous_target='("|'"'"')?(/|~|\$home|\$env:userprofile|[a-z]:[\\/]?|\*|\.{1,2}[\\/]?|(\./)?\.containers[\\/]?[^ ]*)("|'"'"')?( |\)|$)'
if command_is "rm +((-[a-z]*[rf][a-z]*|--recursive|--force) +)+$dangerous_target"; then
  deny "Remoção recursiva da raiz, home, diretório atual ou .containers/ (dados locais do Postgres) não é permitida."
fi
if matches 'remove-item\b.*-recurse' && matches " (-path +|-literalpath +)?$dangerous_target"; then
  deny "Remove-Item -Recurse na raiz, home, diretório atual ou .containers/ não é permitido."
fi

# 2. Git: Gitflow e proteção de histórico (regra 90-git-workflow)
if command_is 'git +push\b.*( --force| -f( |$)|--force-with-lease| \+)'; then
  deny "Force push é proibido."
fi
if command_is 'git +push\b(.*[ :"'"'"'])?(refs/heads/)?(main|master|development)("|'"'"')?( |\)|$)'; then
  deny "Push direto em main/development é proibido — abra um PR a partir de feature/*, bugfix/*, release/* ou hotfix/*."
fi
# "git push [remote] [HEAD]" envia a branch atual, qualquer que seja o remote.
if command_is 'git +push( +-[^ ]+)*( +[^ -][^ ]*)?( +-[^ ]+)*( +"?head"?)?( +-[^ ]+)* *\)? *$'; then
  current="$(git -C "$HOOK_PROJECT_DIR" symbolic-ref --short HEAD 2>/dev/null)"
  case "$current" in
    main|master|development) deny "Você está em '$current': push direto é proibido. Crie feature/<slug> ou bugfix/<slug> e abra um PR." ;;
  esac
fi
# -n é a forma curta de --no-verify no git commit.
if command_is 'git +(commit|push|merge|rebase)\b.*--no-verify' || command_is 'git +commit\b(.* )?-[a-z]*n[a-z]*( |$)'; then
  deny "--no-verify é proibido."
fi
if command_is 'git +(reset +--hard|clean +-[a-z]*f|checkout +(-- +)?\.( |$)|restore +(--staged +)?\.( |$)|stash +(drop|clear)\b)'; then
  deny "Operação git que descarta trabalho (reset --hard, clean -f, checkout/restore de tudo, stash drop/clear) deve ser executada manualmente pelo usuário."
fi
if command_is 'git +branch +-D +(main|development)( |$)'; then
  deny "Apagar main/development é proibido."
fi

# Branch nova do Gitflow sai da base certa e atualizada (feature/bugfix/release ← development · hotfix ← main).
# Simula a cadeia (git switch development && git pull --ff-only && git switch -c feature/x): o hook roda antes dela.
remote_tip() {
  local tip guard=()
  command -v timeout >/dev/null 2>&1 && guard=(timeout 5)
  tip="$(GIT_TERMINAL_PROMPT=0 GCM_INTERACTIVE=never ${guard[@]+"${guard[@]}"} \
    git -C "$HOOK_PROJECT_DIR" ls-remote origin "refs/heads/$1" 2>/dev/null | cut -f1)"
  # Offline ou sem acesso: compara com o último fetch.
  [[ -n "$tip" ]] || tip="$(git -C "$HOOK_PROJECT_DIR" rev-parse -q --verify "refs/remotes/origin/$1" 2>/dev/null)"
  printf '%s' "$tip"
}
current_branch="$(git -C "$HOOK_PROJECT_DIR" symbolic-ref --short -q HEAD 2>/dev/null || echo HEAD)"
refreshed=" "
for segment in "${segments[@]}"; do
  read -ra words <<<"$(tr -d "\"'()" <<<"$segment")"
  while (( ${#words[@]} )) && [[ "${words[0]}" =~ ^([A-Za-z_][A-Za-z0-9_]*=.*|sudo)$ ]]; do words=("${words[@]:1}"); done
  [[ "${words[0]:-}" == git ]] || continue
  args=("${words[@]:2}")
  new_branch="" start_point=""
  case "${words[1]:-}" in
    pull) refreshed+="$current_branch origin/$current_branch " ;;
    fetch) refreshed+="origin/development origin/main " ;;
    switch|checkout)
      positional=() create=0
      for arg in ${args[@]+"${args[@]}"}; do
        case "$arg" in
          -c|-C|-b|-B|--create|--force-create) create=1 ;;
          --) break ;;
          -*) ;;
          *) positional+=("$arg") ;;
        esac
      done
      if (( create )); then
        new_branch="${positional[0]:-}" start_point="${positional[1]:-}"
      elif (( ${#positional[@]} )); then
        current_branch="${positional[0]}"
      fi ;;
    # "git branch <nome> [<base>]" cria; com opção (-d, -m, --show-current…) não.
    branch) [[ "${args[0]:-}" == -* ]] || new_branch="${args[0]:-}" start_point="${args[1]:-}" ;;
  esac
  case "$new_branch" in
    feature/*|bugfix/*|release/*) base=development ;;
    hotfix/*) base=main ;;
    *) continue ;;
  esac
  from="${start_point:-$current_branch}"
  from="${from#refs/heads/}" from="${from#refs/remotes/}"
  fix="git switch $base && git pull --ff-only && git switch -c $new_branch"
  if [[ "$from" != "$base" && "$from" != "origin/$base" ]]; then
    deny "'$new_branch' deve sair de '$base' (Gitflow), não de '$from'. Use: $fix"
  fi
  [[ "$refreshed" == *" $from "* ]] && continue
  tip="$(remote_tip "$base")"
  [[ -z "$tip" ]] && continue  # sem remoto: nada a comparar
  if ! git -C "$HOOK_PROJECT_DIR" merge-base --is-ancestor "$tip" "$from" 2>/dev/null; then
    deny "'$from' está atrás de origin/$base: atualize antes de criar a branch. Use: $fix (pull não fast-forward ou working tree sujo: pare e avise o usuário)."
  fi
done

# 3. Banco: EF Core só contra o Postgres local
if matches 'dotnet +ef +database +(update|drop)'; then
  if matches '(--connection|connectionstrings__)' && ! matches '(host|server)=(localhost|127\.0\.0\.1|postgres)(;|"|'"'"'| |$)'; then
    deny "dotnet ef database update/drop só é permitido contra o Postgres local (localhost/postgres do docker-compose)."
  fi
fi

# 4. Segredos: qualquer referência ao arquivo, seja qual for o comando (cat, cp, Select-String, node -e…).
without_example="${normalized//.env.example/}"
if grep -qiE -- '(^|[ "'"'"'=/\\(<])\.env(\.[a-z]+)?([ "'"'"'|;&)>]|$)|\.(pfx|pem|p12|key)([ "'"'"'|;&)>]|$)|id_rsa|id_ed25519|secrets\.json' <<<"$without_example"; then
  deny "Acesso a arquivo de segredos bloqueado."
fi
if command_is '(printenv|env|set|get-childitem +env:|gci +env:|dir +env:)( *$| *\))' || matches 'echo +\$(env:)?[a-z_]*(key|secret|token|password)'; then
  deny "Exposição de variáveis de ambiente/segredos bloqueada."
fi
if matches 'dotnet +user-secrets +list'; then
  deny "Listar user-secrets expõe valores. Peça ao usuário a chave necessária (sem o valor)."
fi

# 5. Execução remota arbitrária
if matches '(curl|wget|iwr|invoke-webrequest|irm|invoke-restmethod)[^|]*\| *((ba|z)?sh|iex|invoke-expression)' \
  || matches '(iex|invoke-expression)\b.*(irm|iwr|invoke-restmethod|invoke-webrequest|downloadstring)'; then
  deny "Download executado como script é proibido."
fi
if matches 'chmod +(-r +)?777'; then
  deny "chmod 777 é proibido."
fi

# 6. ADR-0003: nenhum SDK de LLM no runtime ("dotnet add package" e a forma do .NET 10, "dotnet package add")
add_package='dotnet +(add +[^ ]* *package|package +add) +'
if matches "$add_package(anthropic|openai|azure\.ai\.|microsoft\.semantickernel|microsoft\.extensions\.ai|awssdk\.bedrock|langchain|mistral|google\.genai|ollamasharp|llamasharp)"; then
  deny "Pacotes de IA/LLM são proibidos no runtime da aplicação (ADR-0003, regra 00-core #2)."
fi

# 7. Central Package Management: versão só em Directory.Packages.props
if matches "$add_package[^ ]+ +.*(-v|--version) "; then
  deny "Com CPM, declare a versão em Directory.Packages.props e use 'dotnet add package <Id>' sem --version."
fi

log_event "ALLOW" "$normalized"
exit 0
