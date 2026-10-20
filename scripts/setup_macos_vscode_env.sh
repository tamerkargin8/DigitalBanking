#!/bin/bash
# Configure the GUI (launchd) environment so VS Code's C# Dev Kit can find the
# already-installed .NET 10 SDK when VS Code was launched from Finder/Dock.
#
# The project-local .vscode/settings.json existingDotnetPath only tells the
# extension host which dotnet to run its own .NET code with; project SDK discovery
# still runs `dotnet --info` from PATH. GUI apps do not read shell profiles, so we
# publish DOTNET_ROOT/PATH to launchd for newly launched apps.
set -euo pipefail

SDK="${DOTNET_ROOT:-$HOME/Documents/Codex/toolchains/dotnet}"
if [ ! -x "$SDK/dotnet" ]; then
  echo "Bundled .NET SDK not found at: $SDK" >&2
  echo "Set DOTNET_ROOT to the SDK directory and retry." >&2
  exit 1
fi

echo "Using SDK: $SDK"
"$SDK/dotnet" --version

launchctl setenv DOTNET_ROOT "$SDK"

CURRENT_PATH="$(launchctl getenv PATH || true)"
case ":$CURRENT_PATH:" in
  *":$SDK:"*) ;;
  *)
    if [ -n "$CURRENT_PATH" ]; then
      launchctl setenv PATH "$SDK:$CURRENT_PATH"
    else
      launchctl setenv PATH "$SDK:/usr/local/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin"
    fi
    ;;
esac

echo "launchd DOTNET_ROOT: $(launchctl getenv DOTNET_ROOT)"
echo "launchd PATH:        $(launchctl getenv PATH)"
echo
echo "Restart VS Code (Cmd+Q, then reopen) so the C# Dev Kit extension host"
echo "inherits the new PATH. This setting lasts for the current login session;"
echo "run this script again after a reboot, or launch VS Code from a terminal"
echo "that already has the SDK on PATH."
