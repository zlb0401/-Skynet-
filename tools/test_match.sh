#!/bin/bash
set -euo pipefail

HOST="${1:-127.0.0.1}"
PORT="${2:-8888}"
PY=python3
CLIENT="$(dirname "$0")/test_client.py"

echo "=== match test: test vs demo ==="
$PY "$CLIENT" --host "$HOST" --port "$PORT" --user test --password 123456 --match &
PID1=$!
sleep 0.5
$PY "$CLIENT" --host "$HOST" --port "$PORT" --user demo --password 123456 --match &
PID2=$!

wait $PID1; R1=$?
wait $PID2; R2=$?

if [[ $R1 -eq 0 && $R2 -eq 0 ]]; then
  echo "=== match test passed ==="
  exit 0
fi
echo "=== match test failed r1=$R1 r2=$R2 ==="
exit 1
