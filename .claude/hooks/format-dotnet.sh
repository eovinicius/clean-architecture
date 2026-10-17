#!/usr/bin/env bash
# PostToolUse(Edit|Write) — aplica a formatação de espaços do .editorconfig nos .cs editados (rápido, sem build).
# O CI roda 'dotnet format --verify-no-changes'; isto evita a falha mais comum. Nunca bloqueia.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
command -v dotnet >/dev/null 2>&1 || exit 0

files=()
while IFS= read -r raw; do
  rel="$(relpath "$raw")"
  [[ "$rel" == *.cs && "$rel" != */obj/* && "$rel" != */bin/* && -f "$HOOK_PROJECT_DIR/$rel" ]] && files+=("$rel")
done < <(touched_paths | sort -u)
[[ ${#files[@]} -eq 0 ]] && exit 0

(cd "$HOOK_PROJECT_DIR" && timeout 60 dotnet format whitespace --folder --include "${files[@]}" >/dev/null 2>&1) || true
log_event "FORMAT" "${files[*]}"
exit 0
