#!/usr/bin/env python3
"""Run the local demo on loopback, with secrets outside version control."""
import argparse
import json
import os
from pathlib import Path
import secrets
import signal
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--service", choices=["api", "dashboard", "both"], default="both")
parser.add_argument("--api-port", type=int, default=5080)
parser.add_argument("--dashboard-port", type=int, default=5081)
parser.add_argument("--database", default=None,
                    help="Optional SQLite file (default: .local/digitalbanking.db). Useful for an isolated disposable smoke DB.")
args = parser.parse_args()
local = ROOT / ".local"
local.mkdir(mode=0o700, exist_ok=True)
credentials = local / "credentials.json"
try:
    fd = os.open(credentials, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
except FileExistsError:
    pass
else:
    with os.fdopen(fd, "w") as file:
        json.dump({"username": "admin", "password": secrets.token_urlsafe(18),
                   "jwt_secret": secrets.token_urlsafe(64)}, file)
settings = json.loads(credentials.read_text())
env = os.environ.copy()
env.update({"ASPNETCORE_ENVIRONMENT": "Development", "Database__Provider": "Sqlite",
            "ConnectionStrings__DefaultConnection": f"Data Source={Path(args.database) if args.database else local / 'digitalbanking.db'}",
            "Jwt__SecretKey": settings["jwt_secret"],
            "Development__DemoPassword": settings["password"],
            "Api__BaseUrl": f"http://127.0.0.1:{args.api_port}"})
processes = []

def stop(*_):
    for process in processes:
        if process.poll() is None:
            process.terminate()

signal.signal(signal.SIGTERM, stop)
signal.signal(signal.SIGINT, stop)
try:
    for name, port in [("API", args.api_port), ("Dashboard", args.dashboard_port)]:
        if args.service != "both" and args.service != name.lower():
            continue
        command = ["bash", str(ROOT / "scripts/dotnet.sh"), "run", "--no-build",
                   "--no-launch-profile", "--project", str(ROOT / f"DigitalBanking.{name}"),
                   "--urls", f"http://127.0.0.1:{port}"]
        processes.append(subprocess.Popen(command, cwd=ROOT, env=env))
    print(f"Local demo credentials: {credentials} (do not commit or share)", flush=True)
    while all(process.poll() is None for process in processes):
        time.sleep(0.5)
    result = next((p.returncode for p in processes if p.returncode is not None), 0)
finally:
    stop()
    for process in processes:
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
sys.exit(result)
