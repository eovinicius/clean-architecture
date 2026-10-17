#!/usr/bin/env bash
# Stop — se código .NET mudou, exige build (warnings já são erros) + testes unitários e de arquitetura antes de encerrar.
# IntegrationTests ficam de fora (Docker, lentos). Opt-out pontual: HARNESS_SKIP_STOP_VERIFY=1.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
cd "$HOOK_PROJECT_DIR" || exit 0

[[ "${HARNESS_SKIP_STOP_VERIFY:-0}" == "1" ]] && exit 0
command -v dotnet >/dev/null 2>&1 || exit 0

# stop_hook_active=true: o agente está tentando parar de novo após um bloqueio deste hook.
# Re-verifica (senão a correção nunca seria conferida), mas desiste após 3 tentativas para evitar loop.
attempts_file="$HOOK_LOG_DIR/.stop-attempts"
if [[ "$(json_get '.stop_hook_active')" == "true" ]]; then
  attempts=$(( $(cat "$attempts_file" 2>/dev/null || echo 0) + 1 ))
  if (( attempts >= 3 )); then
    rm -f "$attempts_file"
    log_event "GIVEUP" "build/testes ainda falhando após $attempts tentativas"
    printf '{"systemMessage":"[harness] Build/testes ainda falham após %s tentativas: a verificação NÃO foi concluída."}\n' "$attempts"
    exit 0
  fi
  mkdir -p "$HOOK_LOG_DIR" && printf '%s' "$attempts" > "$attempts_file"
else
  rm -f "$attempts_file"
fi

changed="$( { git diff --name-only HEAD 2>/dev/null; git ls-files --others --exclude-standard 2>/dev/null; } \
  | grep -E '\.(cs|csproj|props|targets|slnx|editorconfig)$|(^|/)\.editorconfig$' | sort -u)"
[[ -z "$changed" ]] && exit 0

# Pula se este mesmo conteúdo já foi verificado.
stamp="$HOOK_LOG_DIR/.last-verify"
hash="$(while IFS= read -r f; do printf '%s\n' "$f"; cat "$f" 2>/dev/null; done <<<"$changed" | git hash-object --stdin)"
[[ -f "$stamp" && "$(cat "$stamp")" == "$hash" ]] && exit 0

log="$(mktemp)"
trap 'rm -f "$log"' EXIT

# Orçamento total (240 + 2 × 150 s) abaixo do timeout de 600 s do hook em .claude/settings.json:
# se o Claude Code matar o hook, a parada passa sem verificação.
timeout 240 dotnet build CleanArchitecture.slnx --nologo -v quiet >"$log" 2>&1
status=$?
(( status == 124 )) && block "Build excedeu 240 s no hook Stop. Rode 'dotnet build CleanArchitecture.slnx' e confirme o resultado antes de concluir."
if (( status != 0 )); then
  block "Build falhou (warnings contam como erro). Corrija antes de concluir:
$(grep -E ': (error|warning|erro|aviso) ' "$log" | sed -E 's/ \[[^]]+\]$//' | sort -u | head -40)"
fi

for project in app/tests/Application.UnitTests app/tests/ArchitectureTests; do
  [[ -d "$project" ]] || continue
  timeout 150 dotnet test "$project" --no-build --nologo -v quiet >"$log" 2>&1
  status=$?
  (( status == 124 )) && block "Testes de $project excederam 150 s no hook Stop. Rode 'dotnet test $project' e confirme o resultado antes de concluir."
  if (( status != 0 )); then
    block "Testes falharam em $project. Corrija antes de concluir (não enfraqueça nem pule testes):
$(grep -E 'Failed |Com falha|Falha |Error Message|Mensagem de erro|Shouldly|\[FAIL\]' "$log" | head -40)
$(tail -15 "$log")"
  fi
done

mkdir -p "$HOOK_LOG_DIR" && printf '%s' "$hash" > "$stamp"
rm -f "$attempts_file"
log_event "VERIFIED" "build + Application.UnitTests + ArchitectureTests"
exit 0
