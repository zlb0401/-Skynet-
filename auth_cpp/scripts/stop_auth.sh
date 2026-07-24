#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PID_FILE="$ROOT/logs/auth.pid"
if [[ ! -f "$PID_FILE" ]]; then
  echo "pid file not found"
  exit 0
fi
PID="$(cat "$PID_FILE")"
if kill -0 "$PID" 2>/dev/null; then
  kill "$PID"
  echo "stopped auth_server, pid=$PID"
fi
rm -f "$PID_FILE"
