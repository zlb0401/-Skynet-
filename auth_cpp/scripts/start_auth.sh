#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PID_FILE="$ROOT/logs/auth.pid"
LOG_FILE="$ROOT/logs/auth.log"
BIN="$ROOT/build/auth_server"

mkdir -p "$ROOT/logs"

if [[ ! -x "$BIN" ]]; then
  echo "build auth_server first: cmake -S . -B build && cmake --build build"
  exit 1
fi

if [[ -f "$PID_FILE" ]] && kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
  echo "auth_server already running, pid=$(cat "$PID_FILE")"
  exit 0
fi

nohup "$BIN" >>"$LOG_FILE" 2>&1 &
echo $! >"$PID_FILE"
sleep 0.5
if kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
  echo "auth_server started, pid=$(cat "$PID_FILE") port=8889"
else
  echo "failed to start, see $LOG_FILE"
  exit 1
fi
