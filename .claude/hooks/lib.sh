#!/usr/bin/env bash
# Biblioteca compartilhada dos hooks do Agent Harness (Claude Code; Git Bash no Windows, bash no Linux/macOS).
# Contrato: exit 0 = segue · exit 2 = bloqueia (stderr volta ao agente como instrução corretiva).
# JSON da entrada lido com jq → node → python → fallback em bash puro (nenhuma dependência obrigatória).
set -uo pipefail

# Caminho estilo Windows (C:\x\y) → estilo Git Bash (/c/x/y). Caminhos POSIX passam intactos.
to_posix() {
  local p="${1//\\//}"
  if [[ "$p" =~ ^([A-Za-z]):/(.*)$ ]]; then
    p="/$(printf '%s' "${BASH_REMATCH[1]}" | tr '[:upper:]' '[:lower:]')/${BASH_REMATCH[2]}"
  fi
  printf '%s' "$p"
}

HOOK_PROJECT_DIR="$(to_posix "${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel 2>/dev/null || pwd)}")"
HOOK_PROJECT_DIR="${HOOK_PROJECT_DIR%/}"
HOOK_LOG_DIR="$HOOK_PROJECT_DIR/.claude/logs"
HOOK_INPUT="$(cat 2>/dev/null || true)"

_json_engine=""
_detect_json_engine() {
  [[ -n "$_json_engine" ]] && return 0
  if command -v jq >/dev/null 2>&1; then _json_engine="jq"
  elif command -v node >/dev/null 2>&1 && node -e '' >/dev/null 2>&1; then _json_engine="node"
  # No Windows, "python3" pode ser só o atalho da Microsoft Store: testa se executa de verdade.
  elif command -v python3 >/dev/null 2>&1 && python3 -c '' >/dev/null 2>&1; then _json_engine="python3"
  elif command -v python >/dev/null 2>&1 && python -c '' >/dev/null 2>&1; then _json_engine="python"
  else _json_engine="bash"
  fi
}

# json_get '.tool_input.command' → string (objetos/arrays como JSON compacto). Vazio se ausente.
json_get() {
  local path="$1"
  _detect_json_engine
  case "$_json_engine" in
    jq)
      printf '%s' "$HOOK_INPUT" | jq -r "$path // empty" 2>/dev/null ;;
    node)
      printf '%s' "$HOOK_INPUT" | node -e '
        let raw = "";
        process.stdin.on("data", c => raw += c).on("end", () => {
          let v;
          try { v = JSON.parse(raw); } catch { return; }
          for (const k of process.argv[1].replace(/^\./, "").split(".")) {
            v = v !== null && typeof v === "object" ? v[k] : undefined;
            if (v === undefined || v === null) return;
          }
          process.stdout.write(typeof v === "string" ? v : JSON.stringify(v));
        });' "$path" 2>/dev/null ;;
    python3|python)
      printf '%s' "$HOOK_INPUT" | "$_json_engine" -c '
import json, sys
try:
    v = json.load(sys.stdin)
except Exception:
    sys.exit(0)
for k in sys.argv[1].lstrip(".").split("."):
    v = v.get(k) if isinstance(v, dict) else None
    if v is None:
        sys.exit(0)
sys.stdout.write(v if isinstance(v, str) else json.dumps(v))
' "$path" 2>/dev/null ;;
    *)
      # Fallback: primeira ocorrência da última chave (suficiente para strings simples sem aspas escapadas).
      local key="${path##*.}"
      printf '%s' "$HOOK_INPUT" | grep -o "\"$key\"[[:space:]]*:[[:space:]]*\"[^\"]*\"" | head -1 \
        | sed -E 's/^"[^"]*"[[:space:]]*:[[:space:]]*"(.*)"$/\1/' ;;
  esac
}

# Caminhos de arquivo tocados pela ferramenta (Edit/Write: file_path · NotebookEdit: notebook_path).
touched_paths() {
  local key p
  for key in .tool_input.file_path .tool_input.notebook_path; do
    p="$(json_get "$key")"
    [[ -n "$p" ]] && printf '%s\n' "$p"
  done
}

# Caminho relativo à raiz do repositório, com "/", independente de vir como C:\..., /c/... ou relativo.
relpath() {
  local p
  p="$(to_posix "$1")"
  local root_lower p_lower
  root_lower="$(printf '%s' "$HOOK_PROJECT_DIR" | tr '[:upper:]' '[:lower:]')"
  p_lower="$(printf '%s' "$p" | tr '[:upper:]' '[:lower:]')"
  if [[ "$p_lower" == "$root_lower"/* ]]; then
    p="${p:${#HOOK_PROJECT_DIR}+1}"
  fi
  p="${p#./}"
  printf '%s' "$p"
}

json_escape() {
  local s="$1"
  s="${s//\\/\\\\}"; s="${s//\"/\\\"}"; s="${s//$'\t'/\\t}"; s="${s//$'\r'/}"; s="${s//$'\n'/\\n}"
  printf '%s' "$s"
}

# SessionStart/UserPromptSubmit: injeta contexto adicional na conversa.
emit_context() {
  printf '{"hookSpecificOutput":{"hookEventName":"%s","additionalContext":"%s"}}\n' "$1" "$(json_escape "$2")"
}

# PreToolUse: pede confirmação humana em vez de bloquear.
ask_user() {
  log_event "ASK" "$1"
  printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"ask","permissionDecisionReason":"%s"}}\n' "$(json_escape "$1")"
  exit 0
}

block() {
  printf '[harness] %s\n' "$*" >&2
  log_event "BLOCK" "$*"
  exit 2
}

log_event() {
  mkdir -p "$HOOK_LOG_DIR" 2>/dev/null || return 0
  printf '%s\t%s\t%s\t%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$(basename "$0")" "$1" "${2//$'\n'/ }" \
    >> "$HOOK_LOG_DIR/hooks.log" 2>/dev/null || true
}
