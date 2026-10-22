#!/usr/bin/env python3
"""Create ephemeral local credentials for CI smoke. Never prints secrets."""
import json
import os
import secrets
from pathlib import Path

root = Path(__file__).resolve().parents[1]
local = root / ".local"
local.mkdir(mode=0o700, exist_ok=True)
path = local / "credentials.json"
if path.exists():
    raise SystemExit("Refusing to overwrite an existing .local/credentials.json.")
value = {
    "username": "admin",
    "password": "Synthetic" + secrets.token_urlsafe(18) + "1",
    "jwt_secret": secrets.token_urlsafe(64),
    "owner_username": "ci-owner-" + secrets.token_hex(4),
    "owner_password": "SyntheticOwner" + secrets.token_urlsafe(14) + "1"
}
fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
with os.fdopen(fd, "w") as file:
    json.dump(value, file)
print("Ephemeral CI credentials created (contents not printed).")
