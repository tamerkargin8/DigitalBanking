#!/usr/bin/env bash
# Real SQL Server integration runner. It never prints the connection string.
# The fixture creates and drops only a random DigitalBanking_IT_<guid> database.
set -euo pipefail
if [[ -z "${DIGITALBANKING_SQLSERVER_CONNECTION:-}" ]]; then
  echo "NOT RUN: DIGITALBANKING_SQLSERVER_CONNECTION is not set."
  echo "Set it to a disposable SQL Server connection with CREATE DATABASE permission."
  exit 2
fi
if [[ "${DIGITALBANKING_SQLSERVER_ALLOW_DESTRUCTIVE:-}" != "1" ]]; then
  echo "NOT RUN: set DIGITALBANKING_SQLSERVER_ALLOW_DESTRUCTIVE=1 only for a disposable test server."
  exit 2
fi
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
DOTNET_CLI_USE_MSBUILD_SERVER=0 bash scripts/dotnet.sh build DigitalBanking.slnx \
  -p:UseSharedCompilation=false -m:1 -nr:false --nologo
DOTNET_CLI_USE_MSBUILD_SERVER=0 bash scripts/dotnet.sh test \
  DigitalBanking.IntegrationTests/DigitalBanking.IntegrationTests.csproj \
  --no-build --nologo --filter FullyQualifiedName~SqlServerIntegrationTests
