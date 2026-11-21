#!/usr/bin/env bash
# Roda um hook Python com o primeiro interpretador que de fato executa.
# Uso: bash .claude/hooks/run-py.sh <script.py> [args...]
# No Windows, `python3` costuma ser o atalho da Microsoft Store (sai com exit 49 sem rodar nada);
# no macOS/Linux, muitas vezes só `python3` existe. Sem Python funcional, sai 0 (falha aberta,
# como os próprios hooks Python).
for c in python3 python py; do
  if command -v "$c" >/dev/null 2>&1 && "$c" -c '' >/dev/null 2>&1; then
    exec "$c" "$@"
  fi
done
echo "run-py.sh: nenhum Python funcional encontrado; hook $1 ignorado" >&2
exit 0
