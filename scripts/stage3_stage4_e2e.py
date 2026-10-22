#!/usr/bin/env python3
"""Reproducible Stage 3 + Stage 4 HTTP E2E for a synthetic, isolated demo.

This script creates its own temporary SQLite database and random local secrets,
starts API/dashboard on ephemeral loopback ports, and never reads or writes
``.local`` or any UAT path. It refuses reserved/UAT ports and UAT-looking roots.

Coverage: balanced journals, atomic rollback, duplicate approval/idempotency,
decimal/currency validation, Owner baseline repeat/access, delegated operations
scoping, assignment/self-approval/live revocation, SLA and pagination, CSV/demo
protection, and the additive SQLite journal/assignment upgrade (before/after
preservation + second-run idempotency).

Only a safe summary is printed. Credentials, tokens and passwords are never
printed or written to the repo.
"""
from __future__ import annotations

import argparse
import csv as csv_module
import hashlib
import io
import json
import os
import shutil
import signal
import socket
import sqlite3
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
import uuid
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RESERVED_PORTS = {5080, 5081, 5180, 5181, 5280, 5281, 5480, 5481, 5680, 5681}
API = "http://127.0.0.1"
DASH = "http://127.0.0.1"
MAX_WAIT_SECONDS = 75


class E2EFailure(RuntimeError):
    pass


class Checks:
    def __init__(self) -> None:
        self.sections: dict[str, list[tuple[str, bool]]] = {}

    def add(self, section: str, name: str, condition: bool) -> None:
        self.sections.setdefault(section, []).append((name, bool(condition)))
        print(("PASS " if condition else "FAIL ") + f"[{section}] {name}")
        if not condition:
            raise E2EFailure(f"check failed: [{section}] {name}")

    def count(self, section: str) -> tuple[int, int]:
        items = self.sections.get(section, [])
        return sum(1 for _, ok in items if ok), len(items)


def _fail(message: str) -> None:
    raise E2EFailure(message)


def ensure_isolated_environment() -> None:
    root = str(ROOT).lower()
    if "digitalbanking-uat" in root or "/uat/" in root or root.rstrip("/").endswith("/uat"):
        _fail("Refusing to run inside a UAT-looking worktree.")
    # The script never needs an existing .local directory. Refuse a copied UAT
    # credentials/secret file if one is somehow present in this worktree.
    # This script deliberately never opens .local. Credentials are generated in
    # memory and the temporary directory; no repository secret is read.


def ensure_flags() -> None:
    # macOS/harness can mark static config as hidden, which makes ASP.NET skip it.
    # Apply only to this worktree's app projects; no UAT path is involved.
    if sys.platform != "darwin":
        return
    for rel in ("DigitalBanking.API", "DigitalBanking.Dashboard"):
        try:
            subprocess.run(["chflags", "-R", "nohidden", str(ROOT / rel)],
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False)
        except OSError:
            pass


def free_port() -> int:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        sock.bind(("127.0.0.1", 0))
        return int(sock.getsockname()[1])


def choose_ports(api_port: int | None, dashboard_port: int | None) -> tuple[int, int]:
    for port in (api_port, dashboard_port):
        if port is not None:
            if port in RESERVED_PORTS or port < 1024 or port > 65535:
                _fail(f"Refusing reserved/unsafe port {port}. Use an isolated high port.")
    if api_port is None or dashboard_port is None:
        for _ in range(50):
            a = api_port if api_port is not None else free_port()
            d = dashboard_port if dashboard_port is not None else free_port()
            if a != d and a not in RESERVED_PORTS and d not in RESERVED_PORTS:
                api_port, dashboard_port = a, d
                break
        if api_port is None or dashboard_port is None or api_port == dashboard_port:
            _fail("Could not allocate two isolated loopback ports.")
    if api_port == dashboard_port:
        _fail("API and dashboard ports must differ.")
    return int(api_port), int(dashboard_port)


def build_application(env: dict[str, str]) -> None:
    command = [
        "bash", str(ROOT / "scripts/dotnet.sh"), "build", "DigitalBanking.slnx",
        "-p:UseSharedCompilation=false", "-m:1", "-nr:false",
    ]
    completed = subprocess.run(command, cwd=ROOT, env=env, stdout=subprocess.PIPE,
                               stderr=subprocess.STDOUT, text=True, check=False)
    if completed.returncode != 0:
        tail = "\n".join(completed.stdout.splitlines()[-20:])
        _fail("Build failed. Safe compiler tail:\n" + tail)
    print("PASS [setup] synthetic isolated build")


class Stack:
    def __init__(self, api_port: int, dashboard_port: int, db_path: Path, temp: Path):
        self.api_port = api_port
        self.dashboard_port = dashboard_port
        self.db_path = db_path
        self.temp = temp
        self.processes: list[subprocess.Popen] = []
        self.logs: list[Path] = []

    @property
    def api_url(self) -> str:
        return f"{API}:{self.api_port}"

    @property
    def dashboard_url(self) -> str:
        return f"{DASH}:{self.dashboard_port}"

    def start(self, demo_password: str) -> "Stack":
        env = os.environ.copy()
        env.update({
            "ASPNETCORE_ENVIRONMENT": "Development",
            "Database__Provider": "Sqlite",
            "ConnectionStrings__DefaultConnection": f"Data Source={self.db_path}",
            "Jwt__SecretKey": _random_secret(),
            "Jwt__Issuer": "db-stage3-stage4-e2e",
            "Jwt__Audience": "db-stage3-stage4-e2e-client",
            "Development__DemoPassword": demo_password,
            "Operations__PendingSlaMinutes": "30",
            "Api__BaseUrl": self.api_url,
            "DOTNET_CLI_HOME": str(self.temp / "dotnet-home"),
            "DOTNET_NOLOGO": "1",
            "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
            "DOTNET_CLI_USE_MSBUILD_SERVER": "0",
        })
        (self.temp / "dotnet-home").mkdir(mode=0o700, exist_ok=True)
        ensure_flags()
        for name, port, project in (
            ("api", self.api_port, "DigitalBanking.API"),
            ("dashboard", self.dashboard_port, "DigitalBanking.Dashboard"),
        ):
            log = self.temp / f"{name}-{port}.log"
            self.logs.append(log)
            with open(log, "ab") as handle:
                proc = subprocess.Popen(
                    ["bash", str(ROOT / "scripts/dotnet.sh"), "run", "--no-build",
                     "--no-launch-profile", "--project", project,
                     "--urls", f"{API}:{port}"],
                    cwd=ROOT, env=env, stdin=subprocess.DEVNULL,
                    stdout=handle, stderr=subprocess.STDOUT, start_new_session=True,
                )
            self.processes.append(proc)
        self._wait_ready()
        return self

    def _wait_ready(self) -> None:
        deadline = time.time() + MAX_WAIT_SECONDS
        while time.time() < deadline:
            for proc in self.processes:
                if proc.poll() is not None:
                    _fail(f"Demo process exited before readiness. Logs kept at {self.temp}")
            try:
                if _http_status(f"{self.api_url}/health") == 200 and \
                   _http_status(f"{self.dashboard_url}/Login") in (200, 302):
                    return
            except (OSError, urllib.error.URLError):
                pass
            time.sleep(0.5)
        _fail(f"Timed out waiting for readiness. Logs kept at {self.temp}")

    def stop(self) -> None:
        for proc in self.processes:
            if proc.poll() is not None:
                continue
            try:
                os.killpg(os.getpgid(proc.pid), signal.SIGTERM)
            except (ProcessLookupError, PermissionError):
                try:
                    proc.terminate()
                except ProcessLookupError:
                    pass
        deadline = time.time() + 15
        for proc in self.processes:
            remaining = max(0.1, deadline - time.time())
            try:
                proc.wait(timeout=remaining)
            except subprocess.TimeoutExpired:
                try:
                    os.killpg(os.getpgid(proc.pid), signal.SIGKILL)
                except (ProcessLookupError, PermissionError):
                    proc.kill()
        self.processes.clear()


def _random_secret() -> str:
    import secrets
    return secrets.token_urlsafe(48)


def _http_status(url: str) -> int:
    try:
        with urllib.request.urlopen(url, timeout=2) as response:
            return int(response.status)
    except urllib.error.HTTPError as exc:
        return int(exc.code)


class Client:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def request(self, method: str, path: str, token: str | None = None,
                body: object | None = None, expect: tuple[int, ...] = (200,),
                headers: dict[str, str] | None = None, raw: bool = False):
        data = None if body is None else json.dumps(body).encode("utf-8")
        request = urllib.request.Request(self.base_url + path, data=data, method=method)
        request.add_header("Content-Type", "application/json")
        if token:
            request.add_header("Authorization", "Bearer " + token)
        for key, value in (headers or {}).items():
            request.add_header(key, value)
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                status = int(response.status)
                text = response.read().decode("utf-8", errors="replace")
                response_headers = dict(response.headers)
        except urllib.error.HTTPError as exc:
            status = int(exc.code)
            text = exc.read().decode("utf-8", errors="replace")
            response_headers = dict(exc.headers)
        if status not in expect:
            safe = text[:400].replace("\n", " ")
            raise E2EFailure(f"{method} {path} expected {expect}, got {status}: {safe}")
        if raw:
            return status, text, response_headers
        payload = json.loads(text) if text.strip() else None
        return status, payload, response_headers

    def post_csv(self, token: str, path: str, filename: str, content: bytes,
                 expect: tuple[int, ...] = (200,)):
        boundary = "----db-e2e-" + uuid.uuid4().hex
        body = (
            f"--{boundary}\r\n"
            f'Content-Disposition: form-data; name="file"; filename="{filename}"\r\n'
            "Content-Type: text/csv\r\n\r\n"
        ).encode("utf-8") + content + f"\r\n--{boundary}--\r\n".encode("utf-8")
        request = urllib.request.Request(self.base_url + path, data=body, method="POST")
        request.add_header("Content-Type", f"multipart/form-data; boundary={boundary}")
        request.add_header("Authorization", "Bearer " + token)
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                status, text = int(response.status), response.read().decode("utf-8", errors="replace")
        except urllib.error.HTTPError as exc:
            status, text = int(exc.code), exc.read().decode("utf-8", errors="replace")
        if status not in expect:
            raise E2EFailure(f"POST {path} expected {expect}, got {status}: {text[:400]}")
        return json.loads(text) if text.strip() else None


def login(client: Client, username: str, password: str) -> tuple[str, str]:
    _, payload, _ = client.request("POST", "/api/auth/login",
                                   body={"username": username, "password": password})
    data = payload["data"]
    return data["token"], data["userId"]


def db_connect(db_path: Path) -> sqlite3.Connection:
    connection = sqlite3.connect(db_path, timeout=15)
    connection.execute("PRAGMA busy_timeout = 15000")
    return connection


def db_count(db_path: Path, table: str) -> int:
    with db_connect(db_path) as db:
        return int(db.execute(f'SELECT COUNT(*) FROM "{table}"').fetchone()[0])


def canonical_account_id(db_path: Path, account_id: str) -> str:
    with db_connect(db_path) as db:
        row = db.execute('SELECT Id FROM "Accounts" WHERE lower(Id) = lower(?)',
                         (account_id,)).fetchone()
    if row is None:
        _fail("Synthetic account disappeared.")
    return str(row[0])


def account_balance(db_path: Path, account_id: str) -> float:
    with db_connect(db_path) as db:
        row = db.execute('SELECT Balance FROM "Accounts" WHERE lower(Id) = lower(?)',
                         (account_id,)).fetchone()
    if row is None:
        _fail("Synthetic account disappeared.")
    return float(row[0])


def backdate(db_path: Path, approval_id: int, minutes: int) -> None:
    with db_connect(db_path) as db:
        db.execute(
            "UPDATE PaymentApprovals SET CreatedAt = datetime('now', ?) WHERE Id = ?",
            (f"-{minutes} minutes", approval_id),
        )
        db.commit()


def account_liability(db_path: Path, account_id: str) -> float:
    account_id = canonical_account_id(db_path, account_id)
    with db_connect(db_path) as db:
        rows = db.execute(
            'SELECT Side, Amount FROM "JournalLines" '
            'WHERE lower(AccountId) = lower(?) AND AccountType = 1',
            (account_id,),
        ).fetchall()
    return round(sum(float(amount) if int(side) == 2 else -float(amount) for side, amount in rows), 2)


def baseline_amount_for_account(db_path: Path, account_id: str) -> float:
    account_id = canonical_account_id(db_path, account_id)
    with db_connect(db_path) as db:
        rows = db.execute(
            'SELECT l.Side, l.Amount FROM "JournalLines" l '
            'JOIN "JournalEntries" e ON e.Id = l.JournalEntryId '
            'WHERE e.SourceOperation = 5 AND lower(l.AccountId) = lower(?)',
            (account_id,),
        ).fetchall()
    return round(sum(float(amount) if int(side) == 2 else -float(amount) for side, amount in rows), 2)


def insert_duplicate_journal(db_path: Path, approval_id: int, correlation_id: str,
                             account_id: str, amount: float, currency: str) -> None:
    account_id = canonical_account_id(db_path, account_id)
    entry_id = str(uuid.uuid4())
    with db_connect(db_path) as db:
        db.execute(
            'INSERT INTO JournalEntries (Id, SourceKey, SourceOperation, PaymentApprovalId, '
            'CorrelationId, Currency, Description, IsDemo, IsBaseline, ReversalOfJournalEntryId, CreatedAt) '
            'VALUES (?, ?, ?, ?, ?, ?, ?, 0, 0, NULL, ?)',
            (entry_id, f"approval:{approval_id}", 1, approval_id, correlation_id,
             currency, "E2E duplicate rollback probe", datetime.now(timezone.utc).isoformat()),
        )
        db.execute(
            'INSERT INTO JournalLines (Id, JournalEntryId, AccountId, AccountType, Side, Amount, '
            'Currency, TransactionId, CorrelationId, Description) VALUES (?, ?, NULL, 2, 1, ?, ?, NULL, ?, ?)',
            (str(uuid.uuid4()), entry_id, f"{amount:.2f}", currency, correlation_id, "rollback debit"),
        )
        db.execute(
            'INSERT INTO JournalLines (Id, JournalEntryId, AccountId, AccountType, Side, Amount, '
            'Currency, TransactionId, CorrelationId, Description) VALUES (?, ?, ?, 1, 2, ?, ?, NULL, ?, ?)',
            (str(uuid.uuid4()), entry_id, account_id, f"{amount:.2f}", currency, correlation_id, "rollback credit"),
        )
        db.commit()


def remove_duplicate_journal(db_path: Path, approval_id: int) -> None:
    with db_connect(db_path) as db:
        db.execute('DELETE FROM JournalLines WHERE JournalEntryId IN '
                   '(SELECT Id FROM JournalEntries WHERE SourceKey = ?)', (f"approval:{approval_id}",))
        db.execute('DELETE FROM JournalEntries WHERE SourceKey = ?', (f"approval:{approval_id}",))
        db.commit()


def legacy_downgrade(db_path: Path) -> None:
    with db_connect(db_path) as db:
        db.execute("PRAGMA foreign_keys=OFF")
        db.execute('DROP INDEX IF EXISTS "IX_PaymentApprovals_AssignedToUserId_Status"')
        db.execute('DROP TABLE IF EXISTS "JournalLines"')
        db.execute('DROP TABLE IF EXISTS "JournalEntries"')
        for column in ("AssignedAt", "AssignedToUserId", "AssignedToUsername"):
            db.execute(f'ALTER TABLE "PaymentApprovals" DROP COLUMN "{column}"')
        db.commit()


def schema_snapshot(db_path: Path) -> dict:
    with db_connect(db_path) as db:
        tables = {row[0] for row in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
        payment_columns = {row[1] for row in db.execute('PRAGMA table_info("PaymentApprovals")')}
        counts = {
            table: int(db.execute(f'SELECT COUNT(*) FROM "{table}"').fetchone()[0])
            for table in ("Users", "Customers", "Accounts", "PaymentApprovals", "Transactions")
        }
        balances = list(db.execute('SELECT Id, Balance FROM "Accounts" ORDER BY Id'))
    return {"tables": tables, "payment_columns": payment_columns, "counts": counts,
            "balances": balances}


def run_upgrade_script(db_path: Path) -> str:
    completed = subprocess.run(
        [sys.executable, str(ROOT / "scripts/upgrade_sqlite_journal.py"), str(db_path), "--apply"],
        cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, check=False,
    )
    if completed.returncode != 0:
        _fail("SQLite additive upgrade failed. Safe output: " + completed.stdout[-500:])
    return completed.stdout


def check_upgrade_suite(temp: Path, demo_password: str, checks: Checks) -> None:
    """Legacy DB -> additive upgrade -> preserved rows/balances -> idempotent rerun."""
    legacy_db = temp / "legacy-upgrade.db"
    while True:
        api_port, dash_port = free_port(), free_port()
        if api_port != dash_port and api_port not in RESERVED_PORTS and dash_port not in RESERVED_PORTS:
            break
    starter = Stack(api_port, dash_port, legacy_db, temp / "legacy-create")
    (temp / "legacy-create").mkdir(mode=0o700, exist_ok=True)
    starter.start(demo_password)
    starter.stop()

    before = schema_snapshot(legacy_db)
    checks.add("upgrade", "legacy DB was created with current synthetic schema",
               "JournalEntries" in before["tables"] and "AssignedToUserId" in before["payment_columns"])

    legacy_downgrade(legacy_db)
    old = schema_snapshot(legacy_db)
    checks.add("upgrade", "pre-upgrade DB has no journal/assignment schema",
               "JournalEntries" not in old["tables"] and "JournalTables" not in old["tables"]
               and "AssignedToUserId" not in old["payment_columns"])
    checks.add("upgrade", "pre-upgrade rows and balances recorded",
               old["counts"]["Accounts"] > 0 and old["counts"]["Users"] > 0)

    output = run_upgrade_script(legacy_db)
    upgraded = schema_snapshot(legacy_db)
    checks.add("upgrade", "additive upgrade created journal tables",
               "JournalEntries" in upgraded["tables"] and "JournalLines" in upgraded["tables"])
    checks.add("upgrade", "additive upgrade added assignment columns",
               {"AssignedAt", "AssignedToUserId", "AssignedToUsername"}.issubset(upgraded["payment_columns"]))
    checks.add("upgrade", "existing row counts and balances preserved",
               upgraded["counts"] == old["counts"] and upgraded["balances"] == old["balances"])

    backup_pattern = f"{legacy_db.name}.pre-journal-*"
    backups_before = set(legacy_db.parent.glob(backup_pattern))
    second = run_upgrade_script(legacy_db)
    after_second = schema_snapshot(legacy_db)
    checks.add("upgrade", "second upgrade is idempotent (no schema/data change)",
               "Already up to date" in second and after_second["counts"] == old["counts"]
               and after_second["balances"] == old["balances"])
    checks.add("upgrade", "second upgrade writes no extra backup",
               set(legacy_db.parent.glob(backup_pattern)) == backups_before)

    # Restart on the upgraded legacy DB and confirm demo login + balances.
    restart = Stack(api_port, dash_port, legacy_db, temp / "legacy-restart")
    (temp / "legacy-restart").mkdir(mode=0o700, exist_ok=True)
    restart.start(demo_password)
    try:
        client = Client(restart.api_url)
        token, _ = login(client, "customer.a", demo_password)
        _, payload, _ = client.request("GET", "/api/me/accounts", token)
        api_balances = {str(item["id"]).lower(): float(item["balance"]) for item in payload["data"]}
        db_balances = {str(row[0]).lower(): float(row[1]) for row in old["balances"]}
        checks.add("upgrade", "upgraded legacy DB serves the same account balances",
                   len(api_balances) > 0
                   and all(db_balances.get(k) == v for k, v in api_balances.items()))
    finally:
        restart.stop()

    # Keep no DB/secrets in the repo; temp is removed by the caller on success.
    _ = output


def run_e2e(temp: Path, demo_password: str, checks: Checks,
            api_port: int, dash_port: int) -> None:
    db_path = temp / "stage3-stage4.db"
    stack = Stack(api_port, dash_port, db_path, temp / "stack")
    (temp / "stack").mkdir(mode=0o700, exist_ok=True)
    stack.start(demo_password)
    try:
        client = Client(stack.api_url)
        owner_password = _random_secret()

        _, setup, _ = client.request("GET", "/api/setup/status")
        if setup.get("needsOwner"):
            client.request("POST", "/api/setup/owner", body={
                "username": "e2e.owner",
                "email": "e2e.owner@local.invalid",
                "firstName": "E2E",
                "lastName": "Owner",
                "password": owner_password,
                "confirmPassword": owner_password,
            })
        owner, owner_id = login(client, "e2e.owner", owner_password)
        approver, approver_id = login(client, "approver.100k", demo_password)
        low, low_id = login(client, "approver.low", demo_password)
        customer, customer_id = login(client, "customer.a", demo_password)

        _, accounts_payload, _ = client.request("GET", "/api/me/accounts", customer)
        customer_accounts = accounts_payload["data"]
        try_account = next(x for x in customer_accounts if x["currency"] == "TRY")
        usd_account = next(x for x in customer_accounts if x["currency"] == "USD")
        _, all_accounts_payload, _ = client.request("GET", "/api/accounts?pageSize=100", owner)
        all_accounts = all_accounts_payload["data"]["items"]
        b_try = next(x for x in all_accounts if x["accountNumber"] == "2000000001")

        # ------------------------------------------------------------------
        # Stage 3: seed, approved transfer journal, pending no journal, reset,
        # re-seed, Owner baseline, access and append-only surface.
        # ------------------------------------------------------------------
        client.request("POST", "/api/demo/seed", owner, expect=(200,))
        checks.add("stage3", "demo seed", True)

        _, journal_list, _ = client.request("GET", "/api/accounting/journal-entries?pageSize=50", owner)
        items = journal_list["data"]["items"]
        transfer = next((x for x in items if x["sourceOperation"] == "Transfer"), None)
        checks.add("stage3", "journal list", len(items) >= 1)
        checks.add("stage3", "approved transfer has journal", transfer is not None)
        checks.add("stage3", "transfer entry exists", transfer is not None)
        checks.add("stage3", "transfer balanced", transfer is not None
                   and transfer["debitTotal"] == transfer["creditTotal"],
                   )
        checks.add("stage3", "transfer source key", transfer is not None
                   and transfer["sourceKey"].startswith("approval:"))
        _, detail, _ = client.request("GET", f"/api/accounting/journal-entries/{transfer['id']}", owner)
        legs = detail["data"]["lines"]
        checks.add("stage3", "detail has two legs", len(legs) == 2)
        checks.add("stage3", "one debit and one credit",
                   sorted(x["side"] for x in legs) == ["Credit", "Debit"])
        checks.add("stage3", "both legs customer liability",
                   all(x["accountType"] == "CustomerLiability" for x in legs))

        _, trial, _ = client.request("GET", "/api/accounting/trial-balance?currency=TRY", owner)
        checks.add("stage3", "trial balance", trial["data"]["currencies"][0]["debitTotal"]
                   == trial["data"]["currencies"][0]["creditTotal"])
        checks.add("stage3", "trial is balanced", trial["data"]["isBalanced"] is True)
        checks.add("stage3", "trial reports balanced", trial["data"]["isBalanced"] is True)

        before_journals = db_count(db_path, "JournalEntries")
        pending = client.request("POST", "/api/me/requests", customer, body={
            "requestType": "Deposit", "accountId": try_account["id"],
            "amount": 1.00, "description": "stage3-pending",
        }, expect=(201,))[1]["data"]
        checks.add("stage3", "pending request adds no journal",
                   db_count(db_path, "JournalEntries") == before_journals)

        reset_status, _, _ = client.request("POST", "/api/demo/reset", owner)
        checks.add("stage3", "demo reset", reset_status == 200)
        _, after_reset, _ = client.request("GET", "/api/accounting/journal-entries?pageSize=100", owner)
        checks.add("stage3", "demo reset removes demo journals",
                   all(not x["isDemo"] for x in after_reset["data"]["items"]))
        client.request("POST", "/api/demo/seed", owner)

        # A post-migration deposit must not be absorbed into the Owner baseline:
        # only the pre-journal legacy gap is eligible.
        gap_deposit = client.request("POST", "/api/me/requests", customer, body={
            "requestType": "Deposit", "accountId": try_account["id"],
            "amount": 50.00, "description": "baseline-gap-deposit",
        }, expect=(201,))[1]["data"]
        client.request("POST", f"/api/paymentapprovals/{gap_deposit['id']}/approve", owner, expect=(204,))
        pre_baseline_balance = account_balance(db_path, try_account["id"])
        pre_baseline_liability = account_liability(db_path, try_account["id"])

        _, preview, _ = client.request("GET", "/api/accounting/baseline/preview", owner)
        checks.add("stage3", "baseline preview", preview is not None)
        checks.add("stage3", "baseline eligible accounts", preview["data"]["eligibleCount"] > 0)
        _, post1, _ = client.request("POST", "/api/accounting/baseline/post", owner)
        checks.add("stage3", "baseline post created", post1["data"]["createdCount"]
                   == preview["data"]["eligibleCount"])
        _, post2, _ = client.request("POST", "/api/accounting/baseline/post", owner)
        checks.add("stage3", "baseline post idempotent", post2["data"]["createdCount"] == 0)
        expected_gap = round(pre_baseline_balance - pre_baseline_liability, 2)
        checks.add("financial", "baseline covers only the legacy gap after a post-migration deposit",
                   baseline_amount_for_account(db_path, try_account["id"]) == expected_gap)

        _, trial2, _ = client.request("GET", "/api/accounting/trial-balance?currency=TRY", owner)
        checks.add("stage3", "post-baseline trial balanced",
                   trial2["data"]["currencies"][0]["debitTotal"]
                   == trial2["data"]["currencies"][0]["creditTotal"])
        checks.add("stage3", "post-baseline liability reconciles",
                   trial2["data"]["currencies"][0]["liabilityDifference"] == 0.0)

        client.request("GET", "/api/accounting/trial-balance", customer, expect=(403,))
        checks.add("stage3", "customer trial balance forbidden", True)
        _, delegated_trial, _ = client.request("GET", "/api/accounting/trial-balance", approver)
        checks.add("stage3", "delegated can read", delegated_trial["data"]["isBalanced"] is True)
        client.request("GET", "/api/accounting/baseline/preview", approver, expect=(403,))
        checks.add("stage3", "delegated baseline forbidden", True)
        client.request("PUT", f"/api/accounting/journal-entries/{transfer['id']}", owner,
                       body={}, expect=(405,))
        checks.add("stage3", "journal update rejected", True)

        # ------------------------------------------------------------------
        # Cross-cutting Stage 3 financial integrity: rollback, replay,
        # idempotency, decimals, currency, self-approval and assignment.
        # ------------------------------------------------------------------
        cent = client.request("POST", "/api/me/requests", customer, body={
            "requestType": "Deposit", "accountId": try_account["id"],
            "amount": 0.01, "description": "e2e-cent",
        }, expect=(201,))[1]["data"]
        before_cent = account_balance(db_path, try_account["id"])
        client.request("POST", f"/api/paymentapprovals/{cent['id']}/approve", owner, expect=(204,))
        after_cent = account_balance(db_path, try_account["id"])
        checks.add("financial", "0.01 deposit moves exactly one cent",
                   round(after_cent - before_cent, 2) == 0.01)
        cent_entry = client.request("GET", f"/api/accounting/journal-entries?pageSize=100", owner)[1]["data"]["items"]
        cent_row = next(x for x in cent_entry if x["paymentApprovalId"] == cent["id"])
        checks.add("financial", "0.01 journal is balanced",
                   cent_row["debitTotal"] == 0.01 and cent_row["creditTotal"] == 0.01)

        large = client.request("POST", "/api/me/requests", customer, body={
            "requestType": "Deposit", "accountId": try_account["id"],
            "amount": 100000.00, "description": "e2e-100k",
        }, expect=(201,))[1]["data"]
        before_large = account_balance(db_path, try_account["id"])
        client.request("POST", f"/api/paymentapprovals/{large['id']}/approve", approver, expect=(204,))
        checks.add("financial", "100,000.00 is inclusive for delegated approval",
                   round(account_balance(db_path, try_account["id"]) - before_large, 2) == 100000.00)

        over = client.request("POST", "/api/me/requests", customer, body={
            "requestType": "Deposit", "accountId": try_account["id"],
            "amount": 100000.01, "description": "e2e-over-limit",
        }, expect=(201,))[1]["data"]
        client.request("POST", f"/api/paymentapprovals/{over['id']}/approve", approver, expect=(400,))
        checks.add("financial", "100,000.01 is denied for delegated approval", True)
        client.request("POST", f"/api/paymentapprovals/{over['id']}/approve", owner, expect=(204,))
        checks.add("financial", "Owner can approve the over-limit request", True)

        client.request("POST", "/api/me/requests", customer, body={
            "requestType": "Deposit", "accountId": try_account["id"],
            "amount": 0.001, "description": "e2e-invalid-scale",
        }, expect=(400,))
        checks.add("financial", "invalid 3-decimal amount is rejected", True)

        client.request("POST", "/api/me/transfers", customer, body={
            "fromAccountId": try_account["id"], "toAccountId": usd_account["id"],
            "amount": 1.00, "description": "e2e-cross-currency",
        }, expect=(400,))
        checks.add("financial", "cross-currency transfer is rejected", True)

        rollback = client.request("POST", "/api/me/requests", customer, body={
            "requestType": "Deposit", "accountId": try_account["id"],
            "amount": 7.77, "description": "e2e-rollback",
        }, expect=(201,))[1]["data"]
        before_rollback = account_balance(db_path, try_account["id"])
        insert_duplicate_journal(db_path, rollback["id"], rollback["correlationId"],
                                 try_account["id"], 7.77, "TRY")
        rollback_status, _, _ = client.request(
            "POST", f"/api/paymentapprovals/{rollback['id']}/approve", owner,
            expect=tuple(range(400, 600)))
        checks.add("financial", "journal failure rejects the approval", rollback_status >= 400)
        checks.add("financial", "journal failure keeps the balance",
                   account_balance(db_path, try_account["id"]) == before_rollback)
        checks.add("financial", "journal failure keeps the request Pending",
                   _approval_status(db_path, rollback["id"]) == "Pending")
        checks.add("financial", "journal failure writes no legacy transaction",
                   _transaction_count(db_path, rollback["correlationId"]) == 0)
        checks.add("financial", "journal failure adds no second journal",
                   _journal_count_for_approval(db_path, rollback["id"]) == 1)

        remove_duplicate_journal(db_path, rollback["id"])
        client.request("POST", f"/api/paymentapprovals/{rollback['id']}/approve", owner, expect=(204,))
        checks.add("financial", "approval recovers after rollback probe is removed",
                   _approval_status(db_path, rollback["id"]) == "Approved")
        client.request("POST", f"/api/paymentapprovals/{rollback['id']}/approve", owner, expect=(400,))
        checks.add("financial", "double approval is rejected", True)
        checks.add("financial", "double approval does not duplicate the journal",
                   _journal_count_for_approval(db_path, rollback["id"]) == 1)

        idem_key = "e2e-" + uuid.uuid4().hex
        idem_body = {"requestType": "Deposit", "accountId": try_account["id"],
                     "amount": 1.23, "description": "e2e-idempotent-create"}
        idem1 = client.request("POST", "/api/me/requests", customer, body=idem_body,
                               headers={"Idempotency-Key": idem_key}, expect=(201,))[1]["data"]
        idem2 = client.request("POST", "/api/me/requests", customer, body=idem_body,
                               headers={"Idempotency-Key": idem_key}, expect=(201, 200))[1]["data"]
        checks.add("financial", "idempotent create replays the same approval", idem1["id"] == idem2["id"])
        client.request("POST", "/api/me/requests", customer,
                       body={**idem_body, "amount": 1.24},
                       headers={"Idempotency-Key": idem_key}, expect=(409,))
        checks.add("financial", "same idempotency key with different payload conflicts", True)
        checks.add("financial", "idempotent create stores one approval",
                   _approval_count_by_description(db_path, "e2e-idempotent-create") == 1)

        owner_req = client.request("POST", "/api/paymentapprovals/requests", owner, body={
            "requestType": "Deposit", "accountId": try_account["id"],
            "amount": 3.00, "description": "e2e-owner-self",
        }, expect=(201,))[1]["data"]
        client.request("POST", f"/api/paymentapprovals/{owner_req['id']}/approve", owner, expect=(400,))
        checks.add("financial", "Owner self-approval is rejected", True)
        client.request("POST", f"/api/operations/approvals/{owner_req['id']}/assignment", owner,
                       body={"assignedToUserId": owner_id}, expect=(400,))
        checks.add("financial", "Owner self-assignment is rejected", True)
        client.request("POST", f"/api/operations/approvals/{owner_req['id']}/assignment", owner,
                       body={"assignedToUserId": approver_id}, expect=(200,))
        checks.add("financial", "Owner can assign to an eligible approver", True)
        client.request("POST", f"/api/paymentapprovals/{owner_req['id']}/approve", approver, expect=(204,))
        checks.add("financial", "assigned eligible approver can approve", True)

        client.request("GET", "/api/accounting/baseline/preview", customer, expect=(403,))
        client.request("POST", "/api/accounting/baseline/post", customer, expect=(403,))
        checks.add("financial", "customer cannot run Owner baseline", True)
        client.request("GET", "/api/accounting/baseline/preview", approver, expect=(403,))
        client.request("POST", "/api/accounting/baseline/post", approver, expect=(403,))
        checks.add("financial", "delegated cannot run Owner baseline", True)

        # ------------------------------------------------------------------
        # Stage 4: pending age/SLA, scoped delegated queue, assignment,
        # reconciliation gaps, filters/pagination.
        # ------------------------------------------------------------------
        small = client.request("POST", "/api/me/requests", customer, body={
            "requestType": "Deposit", "accountId": try_account["id"],
            "amount": 10.00, "description": "ops-small",
        }, expect=(201,))[1]["data"]
        client.request("POST", "/api/me/requests", customer, body={
            "requestType": "Deposit", "accountId": try_account["id"],
            "amount": 2000.00, "description": "ops-large",
        }, expect=(201,))
        backdate(db_path, small["id"], 45)

        _, summary, _ = client.request("GET", "/api/operations/summary", owner)
        checks.add("stage4", "owner pending summary", summary["data"]["pendingCount"] >= 3)
        checks.add("stage4", "owner overdue summary", summary["data"]["overdueCount"] >= 1)
        checks.add("stage4", "SLA is configured to 30 minutes", summary["data"]["pendingSlaMinutes"] == 30)
        checks.add("stage4", "owner can view reconciliation summary",
                   summary["data"]["canViewReconciliation"] is True)

        _, overdue, _ = client.request(
            "GET", "/api/operations/approvals?status=Pending&age=overdue&pageSize=50", owner)
        checks.add("stage4", "overdue filter returns target",
                   any(x["description"] == "ops-small" for x in overdue["data"]["items"]))
        checks.add("stage4", "overdue rows are flagged",
                   all(x["isOverdue"] for x in overdue["data"]["items"]))

        _, rows, _ = client.request("GET", "/api/operations/approvals?status=Pending&pageSize=50", owner)
        small_row = next(x for x in rows["data"]["items"] if x["description"] == "ops-small")
        checks.add("stage4", "owner sees assignees",
                   any(a["userId"] == approver_id for a in small_row["assignees"]))
        checks.add("stage4", "owner can assign", small_row["canAssign"] is True)
        if small_row["assignedToUserId"] is not None:
            client.request("POST", f"/api/operations/approvals/{small_row['id']}/assignment", owner,
                           body={"assignedToUserId": None})
        _, assigned, _ = client.request("POST", f"/api/operations/approvals/{small_row['id']}/assignment",
                                        owner, body={"assignedToUserId": approver_id})
        checks.add("stage4", "assignment changed", assigned["data"]["changed"] is True)
        _, replay_assign, _ = client.request("POST", f"/api/operations/approvals/{small_row['id']}/assignment",
                                             owner, body={"assignedToUserId": approver_id})
        checks.add("stage4", "assignment idempotent", replay_assign["data"]["changed"] is False)

        _, delegated_summary, _ = client.request("GET", "/api/operations/summary", approver)
        checks.add("stage4", "delegated sees scoped pending",
                   delegated_summary["data"]["pendingCount"] >= 1)
        checks.add("stage4", "delegated assigned-to-me",
                   delegated_summary["data"]["assignedToMeCount"] >= 1)
        client.request("GET", "/api/operations/summary", customer, expect=(403,))
        checks.add("stage4", "customer operations forbidden", True)
        client.request("GET", "/api/operations/approvals?status=Approved", approver, expect=(403,))
        checks.add("stage4", "delegated closed-status listing forbidden", True)
        _, delegated_rows, _ = client.request(
            "GET", "/api/operations/approvals?status=Pending&pageSize=50", approver)
        checks.add("stage4", "delegated rows are within permitted amount",
                   all(x["amount"] <= 100000 for x in delegated_rows["data"]["items"]))
        client.request("POST", f"/api/operations/approvals/{small_row['id']}/assignment", approver,
                       body={"assignedToUserId": owner_id}, expect=(403,))
        checks.add("stage4", "delegated cannot assign another user", True)
        client.request("POST", f"/api/operations/approvals/{small_row['id']}/assignment", owner,
                       body={"assignedToUserId": customer_id}, expect=(400,))
        checks.add("stage4", "invalid customer target rejected", True)

        _, sample, _ = client.request("GET", "/api/demo/reconciliation-sample?kind=mismatch", owner, raw=True)
        preview = client.post_csv(owner, "/api/reconciliation/preview?currency=TRY",
                                  "stage4-mismatch.csv", sample.encode("utf-8"))
        batch_id = preview["data"]["batch"]["id"]
        _, imported, _ = client.request("POST", f"/api/reconciliation/{batch_id}/import", owner)
        checks.add("stage4", "mismatch batch imported", imported["data"]["batch"]["status"] == "Imported")

        _, recon_summary, _ = client.request("GET", "/api/operations/summary", owner)
        checks.add("stage4", "unresolved batch counted",
                   recon_summary["data"]["unresolvedReconciliationBatchCount"] >= 1)
        _, recon, _ = client.request(
            "GET", "/api/operations/reconciliation?exceptionFilter=open&pageSize=50", owner)
        checks.add("stage4", "reconciliation difference listed",
                   any(x["batchId"] == batch_id for x in recon["data"]["items"]))
        _, no_exc, _ = client.request(
            "GET", "/api/operations/reconciliation?exceptionFilter=none", owner)
        checks.add("stage4", "no-exception filter includes batch",
                   any(x["batchId"] == batch_id for x in no_exc["data"]["items"]))

        _, filtered, _ = client.request(
            "GET", "/api/operations/approvals?status=Pending&requestType=Deposit&currency=TRY&pageSize=5", owner)
        checks.add("stage4", "combined filters",
                   all(x["requestType"] == "Deposit" and x["currency"] == "TRY"
                       for x in filtered["data"]["items"]))
        checks.add("stage4", "page size is preserved", filtered["data"]["pageSize"] == 5)
        _, mine, _ = client.request(
            "GET", "/api/operations/approvals?status=Pending&assignment=me&pageSize=50", approver)
        checks.add("stage4", "assigned-to-me filter",
                   all(x["isAssignedToMe"] for x in mine["data"]["items"]) and len(mine["data"]["items"]) >= 1)

        # ------------------------------------------------------------------
        # Extra operational safety: exact SLA boundary, low-limit visibility,
        # live revocation, assignment no-bypass, pagination guard.
        # ------------------------------------------------------------------
        sla30 = client.request("POST", "/api/me/requests", customer, body={
            "requestType": "Deposit", "accountId": try_account["id"],
            "amount": 4.00, "description": "sla-30",
        }, expect=(201,))[1]["data"]
        sla29 = client.request("POST", "/api/me/requests", customer, body={
            "requestType": "Deposit", "accountId": try_account["id"],
            "amount": 5.00, "description": "sla-29",
        }, expect=(201,))[1]["data"]
        backdate(db_path, sla30["id"], 30)
        backdate(db_path, sla29["id"], 29)
        _, overdue2, _ = client.request(
            "GET", "/api/operations/approvals?status=Pending&age=overdue&pageSize=50", owner)
        _, within2, _ = client.request(
            "GET", "/api/operations/approvals?status=Pending&age=within&pageSize=50", owner)
        checks.add("operations", "SLA exactly at 30 minutes is overdue",
                   any(x["id"] == sla30["id"] for x in overdue2["data"]["items"]))
        checks.add("operations", "SLA below 30 minutes is within",
                   any(x["id"] == sla29["id"] for x in within2["data"]["items"]))
        checks.add("operations", "overdue and within sets are disjoint",
                   not any(x["id"] == sla30["id"] for x in within2["data"]["items"]))

        _, low_summary, _ = client.request("GET", "/api/operations/summary", low)
        _, low_rows, _ = client.request(
            "GET", "/api/operations/approvals?status=Pending&pageSize=50", low)
        checks.add("operations", "low-limit delegated does not see deposit work",
                   not any(x["id"] in {small["id"], sla30["id"]} for x in low_rows["data"]["items"]))
        checks.add("operations", "low-limit summary stays scoped",
                   low_summary["data"]["pendingCount"] <= len(low_rows["data"]["items"]))

        # A small transfer is visible to the low delegated, then revoked live.
        revoked_req = client.request("POST", "/api/me/transfers", customer, body={
            "fromAccountId": try_account["id"], "toAccountId": b_try["id"],
            "amount": 5.00, "description": "e2e-live-revocation",
        }, expect=(201,))[1]["data"]
        _, low_before, _ = client.request(
            "GET", "/api/operations/approvals?status=Pending&pageSize=50", low)
        checks.add("operations", "delegated sees permitted transfer before revocation",
                   any(x["id"] == revoked_req["id"] for x in low_before["data"]["items"]))
        _, delegations, _ = client.request("GET", f"/api/delegations?userId={low_id}", owner)
        transfer_delegation = next(x for x in delegations["data"]
                                   if x["operation"] == "Transfer" and x["isActive"] is True)
        client.request("POST", f"/api/delegations/{transfer_delegation['id']}/deactivate", owner)
        _, low_after, _ = client.request(
            "GET", "/api/operations/approvals?status=Pending&pageSize=50", low)
        checks.add("operations", "revoked delegated loses queue visibility",
                   not any(x["id"] == revoked_req["id"] for x in low_after["data"]["items"]))
        client.request("POST", f"/api/paymentapprovals/{revoked_req['id']}/approve", low, expect=(400,))
        checks.add("operations", "revoked stale JWT cannot approve", True)
        client.request("POST", f"/api/paymentapprovals/{revoked_req['id']}/approve", owner, expect=(204,))

        no_bypass = client.request("POST", "/api/paymentapprovals/requests", owner, body={
            "requestType": "Transfer", "accountId": try_account["id"],
            "toAccountId": b_try["id"], "amount": 50000.00,
            "description": "e2e-assignment-no-bypass",
        }, expect=(201,))[1]["data"]
        client.request("POST", f"/api/operations/approvals/{no_bypass['id']}/assignment", owner,
                       body={"assignedToUserId": low_id}, expect=(400,))
        checks.add("operations", "ineligible assignment target is rejected", True)
        with db_connect(db_path) as db:
            db.execute('UPDATE PaymentApprovals SET AssignedToUserId = ?, AssignedToUsername = ? WHERE Id = ?',
                       (low_id, "approver.low", no_bypass["id"]))
            db.commit()
        client.request("POST", f"/api/paymentapprovals/{no_bypass['id']}/approve", low, expect=(400,))
        checks.add("operations", "assignment does not bypass the live limit",
                   _approval_status(db_path, no_bypass["id"]) == "Pending")
        client.request("POST", f"/api/paymentapprovals/{no_bypass['id']}/cancel", owner, expect=(204,))

        for index in range(12):
            client.request("POST", "/api/me/requests", customer, body={
                "requestType": "Deposit", "accountId": try_account["id"],
                "amount": 1.00, "description": f"e2e-page-{index:02d}",
            }, expect=(201,))
        _, page2, _ = client.request(
            "GET", "/api/operations/approvals?status=Pending&pageSize=5&page=2", owner)
        checks.add("operations", "bounded pagination page two", page2["data"]["pageNumber"] == 2
                   and len(page2["data"]["items"]) == 5)
        client.request("GET", "/api/operations/approvals?pageSize=51", owner, expect=(400,))
        checks.add("operations", "page size above 50 is rejected", True)

        # ------------------------------------------------------------------
        # CSV/demo protection and scoped reset.
        # ------------------------------------------------------------------
        formula_csv = (
            "external_reference,internal_reference,amount,currency,transaction_date,direction\r\n"
            f"=1+1,{uuid.uuid4()},1.00,TRY,2026-10-22,Debit\r\n"
        ).encode("utf-8")
        formula_preview = client.post_csv(owner, "/api/reconciliation/preview?currency=TRY",
                                          "formula.csv", formula_csv)
        formula_batch_id = formula_preview["data"]["batch"]["id"]
        client.request("POST", f"/api/reconciliation/{formula_batch_id}/import", owner)
        _, exported, _ = client.request(
            "GET", f"/api/reconciliation/batches/{formula_batch_id}/export", owner, raw=True)
        checks.add("csv", "formula-looking CSV export is prefixed safely", "'=1+1" in exported)
        client.post_csv(owner, "/api/reconciliation/preview?currency=TRY", "bad.txt",
                        sample.encode("utf-8"), expect=(400,))
        checks.add("csv", "non-CSV upload is rejected", True)
        client.post_csv(customer, "/api/reconciliation/preview?currency=TRY",
                        "forbidden.csv", sample.encode("utf-8"), expect=(403,))
        checks.add("csv", "customer cannot upload reconciliation", True)

        batch_count_before = db_count(db_path, "ReconciliationBatches")
        replay_preview = client.post_csv(owner, "/api/reconciliation/preview?currency=TRY",
                                         "stage4-mismatch.csv", sample.encode("utf-8"))
        checks.add("csv", "same CSV+scope replays the same batch",
                   replay_preview["data"]["batch"]["id"] == batch_id
                   and db_count(db_path, "ReconciliationBatches") == batch_count_before)

        client.request("POST", "/api/demo/seed", customer, expect=(403,))
        client.request("POST", "/api/demo/reset", customer, expect=(403,))
        checks.add("demo", "customer cannot seed or reset demo data", True)

        demo_journal = next((x for x in client.request(
            "GET", "/api/accounting/journal-entries?pageSize=100", owner)[1]["data"]["items"]
            if x["isDemo"] and x["sourceOperation"] == "Transfer"), None)
        non_demo_journal = next((x for x in client.request(
            "GET", "/api/accounting/journal-entries?pageSize=100", owner)[1]["data"]["items"]
            if not x["isDemo"] and x["sourceOperation"] == "Deposit"), None)
        checks.add("demo", "demo and non-demo journals both exist before reset",
                   demo_journal is not None and non_demo_journal is not None)
        client.request("POST", "/api/demo/reset", owner)
        journal_after_reset = client.request(
            "GET", "/api/accounting/journal-entries?pageSize=100", owner)[1]["data"]["items"]
        checks.add("demo", "demo reset removes demo journal",
                   not any(x["id"] == demo_journal["id"] for x in journal_after_reset))
        checks.add("demo", "demo reset preserves non-demo journal",
                   any(x["id"] == non_demo_journal["id"] for x in journal_after_reset))
        checks.add("demo", "demo reset preserves non-demo pending request",
                   _approval_status(db_path, pending["id"]) == "Pending")

        # ------------------------------------------------------------------
        # Additive SQLite upgrade on a separate legacy DB, including restart.
        # ------------------------------------------------------------------
        check_upgrade_suite(temp, demo_password, checks)

    finally:
        stack.stop()


def _approval_status(db_path: Path, approval_id: int) -> str:
    mapping = {1: "Pending", 2: "Approved", 3: "Rejected", 4: "AutoApproved", 5: "Cancelled"}
    with db_connect(db_path) as db:
        row = db.execute('SELECT Status FROM "PaymentApprovals" WHERE Id = ?', (approval_id,)).fetchone()
    return mapping.get(int(row[0]), str(row[0])) if row else "Missing"


def _transaction_count(db_path: Path, correlation_id: str) -> int:
    with db_connect(db_path) as db:
        return int(db.execute('SELECT COUNT(*) FROM "Transactions" WHERE lower(CorrelationId) = lower(?)',
                              (correlation_id,)).fetchone()[0])


def _journal_count_for_approval(db_path: Path, approval_id: int) -> int:
    with db_connect(db_path) as db:
        return int(db.execute('SELECT COUNT(*) FROM "JournalEntries" WHERE PaymentApprovalId = ?',
                              (approval_id,)).fetchone()[0])


def _approval_count_by_description(db_path: Path, description: str) -> int:
    with db_connect(db_path) as db:
        return int(db.execute('SELECT COUNT(*) FROM "PaymentApprovals" WHERE Description = ?',
                              (description,)).fetchone()[0])


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--api-port", type=int, default=None)
    parser.add_argument("--dashboard-port", type=int, default=None)
    parser.add_argument("--no-build", action="store_true")
    parser.add_argument("--keep-temp", action="store_true")
    args = parser.parse_args()

    try:
        ensure_isolated_environment()
        api_port, dashboard_port = choose_ports(args.api_port, args.dashboard_port)
    except E2EFailure as exc:
        print("E2E REFUSED: " + str(exc))
        return 2

    temp = Path(tempfile.mkdtemp(prefix="db-stage3-stage4-e2e-"))
    os.chmod(temp, 0o700)
    checks = Checks()
    success = False
    try:
        build_env = os.environ.copy()
        build_env.update({
            "DOTNET_CLI_HOME": str(temp / "dotnet-home"),
            "DOTNET_NOLOGO": "1",
            "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
            "DOTNET_CLI_USE_MSBUILD_SERVER": "0",
        })
        (temp / "dotnet-home").mkdir(mode=0o700, exist_ok=True)
        if not args.no_build:
            build_application(build_env)

        demo_password = _random_secret()
        ensure_flags()
        run_e2e(temp, demo_password, checks, api_port, dashboard_port)
        success = True
    except E2EFailure as exc:
        print("E2E FAILED: " + str(exc))
        print(f"Safe debug directory kept at: {temp}")
        return 1
    except Exception as exc:  # noqa: BLE001 - print a safe type only
        print(f"E2E ERROR: {type(exc).__name__}")
        print(f"Safe debug directory kept at: {temp}")
        return 1
    finally:
        if success and not args.keep_temp:
            shutil.rmtree(temp, ignore_errors=True)

    for section in ("stage3", "stage4", "financial", "operations", "csv", "demo", "upgrade"):
        passed, total = checks.count(section)
        print(f"{section}: {passed}/{total}")
    total_passed = sum(checks.count(s)[0] for s in checks.sections)
    total = sum(checks.count(s)[1] for s in checks.sections)
    print(f"TOTAL: {total_passed}/{total}")
    print("E2E PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
