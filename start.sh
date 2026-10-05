#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PARENT_START="$SCRIPT_DIR/../start.sh"

if [ -f "$PARENT_START" ]; then
    exec "$PARENT_START" "$@"
else
    echo "Erro: script start.sh principal não encontrado em $PARENT_START"
    exit 1
fi
