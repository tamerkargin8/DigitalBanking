#!/usr/bin/env python3
"""Start the local synthetic demo in a detached session.

The foreground ``run_local.py`` intentionally terminates its child applications
on SIGTERM/SIGINT. Some IDE/harness launchers send that signal when their shell
closes, which is why a demo can look healthy and then disappear. This helper
starts the same runner in a new process session, writes a PID file and returns
immediately; the demo keeps running after the invoking terminal/session exits.

It only targets this worktree's loopback demo. It has no stop command and never
touches another worktree or a UAT database.
"""
import argparse
import os
import subprocess
import sys
import time
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--api-port", type=int, default=5180)
parser.add_argument("--dashboard-port", type=int, default=5181)
parser.add_argument("--database", default=None,
                    help="Optional SQLite file; default is .local/digitalbanking.db")
parser.add_argument("--wait-seconds", type=int, default=30)
args = parser.parse_args()

local = ROOT / ".local"
local.mkdir(mode=0o700, exist_ok=True)
pid_file = local / f"demo-{args.api_port}-{args.dashboard_port}.pid"
log_file = local / f"demo-{args.api_port}-{args.dashboard_port}.log"

if pid_file.exists():
    try:
        old_pid = int(pid_file.read_text().strip())
        os.kill(old_pid, 0)
    except (ValueError, ProcessLookupError, PermissionError):
        pid_file.unlink(missing_ok=True)
    else:
        print(f"Local demo already running (pid {old_pid}); use the existing process.")
        print(f"API: http://127.0.0.1:{args.api_port}  Dashboard: http://127.0.0.1:{args.dashboard_port}")
        raise SystemExit(0)

command = [
    sys.executable, str(ROOT / "scripts" / "run_local.py"),
    "--api-port", str(args.api_port),
    "--dashboard-port", str(args.dashboard_port),
]
if args.database:
    command += ["--database", args.database]

# start_new_session detaches from the invoking shell's process group, so a
# terminal/harness SIGTERM cannot reach run_local.py and trigger its stop handler.
with open(log_file, "ab") as log:
    process = subprocess.Popen(
        command,
        cwd=ROOT,
        stdin=subprocess.DEVNULL,
        stdout=log,
        stderr=subprocess.STDOUT,
        start_new_session=True,
    )
pid_file.write_text(str(process.pid), encoding="utf-8")

api_health = f"http://127.0.0.1:{args.api_port}/health"
for _ in range(max(args.wait_seconds, 1)):
    if process.poll() is not None:
        print(f"Local demo exited early (exit {process.returncode}). See {log_file}.")
        raise SystemExit(1)
    try:
        with urllib.request.urlopen(api_health, timeout=1) as response:
            if response.status == 200:
                print(f"Local demo started detached (pid {process.pid}).")
                print(f"API: http://127.0.0.1:{args.api_port}  Dashboard: http://127.0.0.1:{args.dashboard_port}")
                print(f"Log: {log_file}")
                raise SystemExit(0)
    except Exception:
        time.sleep(1)

print(f"Local demo process started but health check timed out. See {log_file}.")
raise SystemExit(2)
