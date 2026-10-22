#!/usr/bin/env bash
# CI-only isolated SQLite HTTP smoke. Starts and stops only its own PID.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
API_PORT="${CI_API_PORT:-5180}"
DASHBOARD_PORT="${CI_DASHBOARD_PORT:-5181}"
if [[ ! "$API_PORT" =~ ^[0-9]+$ || ! "$DASHBOARD_PORT" =~ ^[0-9]+$ ]] \
   || (( 10#$API_PORT < 1024 || 10#$API_PORT > 65535 || 10#$DASHBOARD_PORT < 1024 || 10#$DASHBOARD_PORT > 65535 )) \
   || [[ "$API_PORT" == "$DASHBOARD_PORT" ]]; then
  echo "CI_API_PORT/CI_DASHBOARD_PORT must be distinct unprivileged ports (1024-65535)." >&2
  exit 1
fi
# Refuse before touching credentials if either loopback port is already serving
# (for example a developer's demo). Readiness must never attach to another app.
if ! python3 - "$API_PORT" "$DASHBOARD_PORT" <<'PY'
import socket, sys
for raw in sys.argv[1:]:
    port = int(raw)
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        sock.settimeout(0.5)
        if sock.connect_ex(("127.0.0.1", port)) == 0:
            print(f"Port {port} is already occupied on loopback.", file=sys.stderr)
            sys.exit(1)
PY
then
  echo "Refusing to start: requested loopback port is occupied." >&2
  exit 1
fi
mkdir -p artifacts
if [[ -e .local/credentials.json ]]; then
  echo "Refusing to overwrite existing .local/credentials.json." >&2
  exit 1
fi
TMP_DIR="$(mktemp -d "${RUNNER_TEMP:-/tmp}/digitalbanking-smoke.XXXXXX")"
DB="$TMP_DIR/smoke.db"
APP_LOG="$TMP_DIR/app.log"
SUMMARY="$ROOT/artifacts/smoke-summary.txt"
python3 scripts/ci_prepare_credentials.py
python3 scripts/run_local.py --api-port "$API_PORT" --dashboard-port "$DASHBOARD_PORT" --database "$DB" >"$APP_LOG" 2>&1 &
APP_PID=$!
cleanup() {
  local status=$?
  if kill -0 "$APP_PID" 2>/dev/null; then
    kill "$APP_PID" 2>/dev/null || true
    wait "$APP_PID" 2>/dev/null || true
  fi
  exit "$status"
}
trap cleanup EXIT INT TERM
ready=0
for _ in $(seq 1 60); do
  if ! kill -0 "$APP_PID" 2>/dev/null; then
    echo "Demo process exited before readiness. See CI logs (no credentials are printed)." >&2
    exit 1
  fi
  if curl -fsS --max-time 2 "http://127.0.0.1:${API_PORT}/health" >/dev/null 2>&1 \
     && curl -fsS --max-time 2 "http://127.0.0.1:${DASHBOARD_PORT}/Login" >/dev/null 2>&1; then
    ready=1
    break
  fi
  sleep 1
done
if [[ "$ready" != "1" ]]; then
  echo "Timed out waiting for loopback demo readiness." >&2
  exit 1
fi
python3 scripts/smoke_test.py --api-port "$API_PORT" --dashboard-port "$DASHBOARD_PORT" | tee "$SUMMARY"
