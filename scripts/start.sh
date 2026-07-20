#!/bin/bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

PID_FILE="$ROOT/logs/skynet.pid"
LOG_FILE="$ROOT/logs/skynet.log"

mkdir -p "$ROOT/logs"

if [[ -f "$PID_FILE" ]] && kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
  echo "skynet already running, pid=$(cat "$PID_FILE")"
  exit 0
fi

nohup "$ROOT/skynet/skynet" "$ROOT/server/config/config" >>"$LOG_FILE" 2>&1 &
echo $! >"$PID_FILE"
sleep 1

if kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
  echo "skynet started, pid=$(cat "$PID_FILE")"
  echo "log: $LOG_FILE"
else
  echo "failed to start skynet, see $LOG_FILE"
  exit 1
fi
