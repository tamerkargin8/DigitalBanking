#!/bin/bash
# Read-only verification of the local .NET SDK and VS Code discovery prerequisites.
# This does not launch or restart VS Code.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SDK="${DOTNET_ROOT:-$HOME/Documents/Codex/toolchains/dotnet}"
failures=0
warnings=0

ok() { printf 'OK   %s\n' "$1"; }
warn() { printf 'WARN %s\n' "$1"; warnings=$((warnings + 1)); }
fail() { printf 'FAIL %s\n' "$1"; failures=$((failures + 1)); }

required_version="$(python3 - <<'PY' "$ROOT/global.json"
import json,sys
print(json.load(open(sys.argv[1]))["sdk"]["version"])
PY
)"

if [ -x "$SDK/dotnet" ]; then
  ok "bundled dotnet executable: $SDK/dotnet"
else
  fail "bundled dotnet executable missing: $SDK/dotnet"
fi

if [ -x "$SDK/dotnet" ] && "$SDK/dotnet" --list-sdks | grep -q "$required_version"; then
  ok "global.json SDK $required_version is installed"
else
  fail "global.json SDK $required_version is not reported by $SDK/dotnet --list-sdks"
fi

if command -v dotnet >/dev/null 2>&1; then
  ok "dotnet is on the current shell PATH ($(command -v dotnet))"
else
  warn "dotnet is not on the current shell PATH; scripts/dotnet.sh still works"
fi

gui_root="$(launchctl getenv DOTNET_ROOT || true)"
gui_path="$(launchctl getenv PATH || true)"
if [ "$gui_root" = "$SDK" ]; then
  ok "launchd DOTNET_ROOT is set for newly launched GUI apps"
else
  warn "launchd DOTNET_ROOT is not set; run scripts/setup_macos_vscode_env.sh and restart VS Code"
fi

case ":$gui_path:" in
  *":$SDK:"*) ok "launchd PATH contains the bundled SDK" ;;
  *) warn "launchd PATH does not contain $SDK; run scripts/setup_macos_vscode_env.sh" ;;
esac

settings="$ROOT/.vscode/settings.json"
if python3 - <<'PY' "$settings" "$SDK/dotnet"
import json,sys
settings=json.load(open(sys.argv[1]))
expected=sys.argv[2]
paths={item.get("path") for item in settings.get("dotnetAcquisitionExtension.existingDotnetPath", [])}
raise SystemExit(0 if expected in paths else 1)
PY
then
  ok ".vscode existingDotnetPath points at the bundled SDK"
else
  warn ".vscode existingDotnetPath does not point at $SDK/dotnet"
fi

if [ -e "$ROOT/DigitalBanking.slnx" ]; then
  ok "solution file present: DigitalBanking.slnx"
else
  fail "DigitalBanking.slnx missing"
fi

echo
if [ "$failures" -gt 0 ]; then
  echo "VS Code SDK prerequisites: FAILED ($failures failure(s), $warnings warning(s))."
  exit 1
fi

echo "VS Code SDK prerequisites: local checks passed ($warnings warning(s))."
echo "GUI verification still requires restarting VS Code and opening DigitalBanking.slnx;"
echo "this script cannot inspect the running IDE window."
