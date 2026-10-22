#!/usr/bin/env python3
"""Additive SQLite upgrade for append-only double-entry journal tables.

SQLite Development uses EnsureCreated, which does not add tables/columns to an
existing database. This helper is dry-run by default and requires --apply. It
creates JournalEntries/JournalLines plus the Stage 4 PaymentApprovals assignment
columns; it never drops or rewrites rows and never backfills baselines. SQL Server
uses the EF migration. A timestamped backup is written before changes.
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

DDL = [
    '''CREATE TABLE "JournalEntries" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_JournalEntries" PRIMARY KEY,
    "SourceKey" TEXT NOT NULL,
    "SourceOperation" INTEGER NOT NULL,
    "PaymentApprovalId" INTEGER NULL,
    "CorrelationId" TEXT NULL,
    "Currency" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "IsDemo" INTEGER NOT NULL,
    "IsBaseline" INTEGER NOT NULL,
    "ReversalOfJournalEntryId" TEXT NULL,
    "CreatedAt" TEXT NOT NULL
);''',
    '''CREATE TABLE "JournalLines" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_JournalLines" PRIMARY KEY,
    "JournalEntryId" TEXT NOT NULL,
    "AccountId" TEXT NULL,
    "AccountType" INTEGER NOT NULL,
    "Side" INTEGER NOT NULL,
    "Amount" TEXT NOT NULL,
    "Currency" TEXT NOT NULL,
    "TransactionId" TEXT NULL,
    "CorrelationId" TEXT NULL,
    "Description" TEXT NULL,
    CONSTRAINT "FK_JournalLines_Accounts_AccountId" FOREIGN KEY ("AccountId") REFERENCES "Accounts" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_JournalLines_JournalEntries_JournalEntryId" FOREIGN KEY ("JournalEntryId") REFERENCES "JournalEntries" ("Id") ON DELETE CASCADE
);''',
    'CREATE UNIQUE INDEX "IX_JournalEntries_SourceKey" ON "JournalEntries" ("SourceKey");',
    'CREATE INDEX "IX_JournalEntries_CorrelationId" ON "JournalEntries" ("CorrelationId");',
    'CREATE INDEX "IX_JournalEntries_Currency_CreatedAt" ON "JournalEntries" ("Currency", "CreatedAt");',
    'CREATE INDEX "IX_JournalEntries_PaymentApprovalId" ON "JournalEntries" ("PaymentApprovalId");',
    'CREATE INDEX "IX_JournalLines_AccountId" ON "JournalLines" ("AccountId");',
    'CREATE INDEX "IX_JournalLines_CorrelationId" ON "JournalLines" ("CorrelationId");',
    'CREATE INDEX "IX_JournalLines_JournalEntryId_Side" ON "JournalLines" ("JournalEntryId", "Side");',
    'CREATE INDEX "IX_JournalLines_TransactionId" ON "JournalLines" ("TransactionId");',
]

with sqlite3.connect(f"file:{path}?mode=ro", uri=True) as db:
    tables = {row[0] for row in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
    if "Accounts" not in tables:
        raise SystemExit("Accounts table not found; this does not look like a DigitalBanking SQLite database.")
    existing = tables & {"JournalEntries", "JournalLines"}
    if existing and len(existing) != 2:
        raise SystemExit(f"Partial journal schema detected ({sorted(existing)}); manual review required.")
    payment_columns = {row[1] for row in db.execute('PRAGMA table_info("PaymentApprovals")')}
    indexes = {row[0] for row in db.execute("SELECT name FROM sqlite_master WHERE type='index'")}
    journal_count = None
    if len(existing) == 2:
        journal_count = db.execute('SELECT COUNT(*) FROM "JournalEntries"').fetchone()[0]

statements = list(DDL) if not existing else []
assignment_columns = [
    ("AssignedAt", 'ALTER TABLE "PaymentApprovals" ADD COLUMN "AssignedAt" TEXT NULL;'),
    ("AssignedToUserId", 'ALTER TABLE "PaymentApprovals" ADD COLUMN "AssignedToUserId" TEXT NULL;'),
    ("AssignedToUsername", 'ALTER TABLE "PaymentApprovals" ADD COLUMN "AssignedToUsername" TEXT NULL;'),
]
for name, statement in assignment_columns:
    if name not in payment_columns:
        statements.append(statement)
assignment_index = "IX_PaymentApprovals_AssignedToUserId_Status"
if assignment_index not in indexes:
    statements.append(
        f'CREATE INDEX "{assignment_index}" ON "PaymentApprovals" ("AssignedToUserId", "Status");')

print(f"DB: {path}")
if not statements:
    journal_note = f" JournalEntries rows: {journal_count}." if journal_count is not None else ""
    print("Already up to date." + journal_note)
    raise SystemExit(0)
for statement in statements:
    print(("APPLY " if args.apply else "PLAN  ") + statement.splitlines()[0])
if not args.apply:
    print("Dry run only. Re-run with --apply after backing up and stopping the app.")
    raise SystemExit(0)

stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
backup = path.with_name(path.name + f".pre-journal-{stamp}.bak")
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
print("Applied. No baseline was created; run the Owner baseline preview/post explicitly.")
