#!/bin/bash
set -euo pipefail
if command -v dotnet >/dev/null 2>&1; then
  exec dotnet "$@"
fi
sdk="${DOTNET_ROOT:-$HOME/Documents/Codex/toolchains/dotnet}"
export DOTNET_ROOT="$sdk"
export DOTNET_CLI_HOME="${TMPDIR:-/tmp}/digitalbanking-dotnet-cli"
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
exec "$sdk/dotnet" "$@"
