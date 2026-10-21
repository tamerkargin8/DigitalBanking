#!/usr/bin/env python3
"""Additive SQLite upgrade for account name/type and account-opening metadata.

SQLite Development uses EnsureCreated, which does not alter an existing database.
This helper is dry-run by default and requires --apply. It never drops data, and
refuses paths that look like the separate UAT copy. A timestamped backup is
created next to the database before changes. SQL Server uses its EF migration,
not this script.
"""
import argparse
import datetime as dt
import shutil
import sqlite3
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("database")
parser.add_argument("--apply", action="store_true")
args = parser.parse_args()

path = Path(args.database).expanduser().resolve()
if not path.is_file():
    raise SystemExit(f"DB not found: {path}")
if "digitalbanking-uat" in str(path).lower() or "/uat/" in str(path).lower():
    raise SystemExit("Refusing to touch a UAT database. Run this only on your own synthetic copy.")

columns = {
    "Accounts": [
        ("AccountName", "TEXT NOT NULL DEFAULT 'Vadesiz Hesap'"),
        ("AccountType", "INTEGER NOT NULL DEFAULT 1"),
    ],
    "PaymentApprovals": [
        ("RequestedAccountName", "TEXT NULL"),
        ("RequestedAccountType", "INTEGER NULL"),
    ],
}

with sqlite3.connect(f"file:{path}?mode=ro", uri=True) as db:
    existing = {}
    for table in columns:
        existing[table] = {row[1] for row in db.execute(f'PRAGMA table_info("{table}")')}

statements = []
for table, items in columns.items():
    for name, ddl in items:
        if name not in existing[table]:
            statements.append(f'ALTER TABLE "{table}" ADD COLUMN "{name}" {ddl};')

print(f"DB: {path}")
if not statements:
    print("Already up to date; no changes.")
    raise SystemExit(0)
for statement in statements:
    print(("APPLY " if args.apply else "PLAN  ") + statement)
if not args.apply:
    print("Dry run only. Re-run with --apply after backing up and stopping the app.")
    raise SystemExit(0)

stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
backup = path.with_name(path.name + f".pre-account-metadata-{stamp}.bak")
shutil.copy2(path, backup)
print(f"Backup: {backup}")

with sqlite3.connect(path) as db:
    db.execute("BEGIN IMMEDIATE")
    try:
        for statement in statements:
            db.execute(statement)
        db.commit()
    except Exception:
        db.rollback()
        raise
print("Applied. Existing rows use Vadesiz Hesap / Checking defaults.")
