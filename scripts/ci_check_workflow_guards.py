#!/usr/bin/env python3
"""Static guardrails for the checked-in GitHub Actions workflows."""
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1]
ci = (root / ".github/workflows/ci.yml").read_text()
sql = (root / ".github/workflows/sqlserver-integration.yml").read_text()
errors = []

def require(cond, message):
    if not cond:
        errors.append(message)

require("permissions:\n  contents: read" in ci, "ci.yml must set contents: read")
require("permissions:\n  contents: read" in sql, "SQL workflow must set contents: read")
require("pull_request_target" not in ci + sql, "pull_request_target is forbidden")
require("write-all" not in ci + sql, "write-all permission is forbidden")
require("cache" not in ci.lower(), "cache action/step is not allowed in normal CI")
require("continue-on-error" not in ci + sql, "continue-on-error must not mask SQL failures")
require("accept_sqlserver_eula" in sql, "SQL workflow must require explicit manual EULA input")
require("workflow_dispatch" in sql and "pull_request:" not in sql and "push:" not in sql,
        "SQL workflow must be manual-only")
require("2022-CU27-ubuntu-22.04" in sql, "SQL Server image must be explicitly pinned")
require("verify_sqlserver_trx.py" in sql, "SQL workflow must gate on the 8-test TRX checker")
require("DIGITALBANKING_SQLSERVER_ALLOW_DESTRUCTIVE" in sql, "SQL workflow must set the destructive allow gate")
require("DIGITALBANKING_SQLSERVER_CONNECTION" in sql, "SQL workflow must supply the connection via env")
require("smoke-summary" in ci and "ci-test-summaries" in ci, "CI artifact summaries missing")
require("actions/checkout@" in ci and "actions/setup-dotnet@" in ci and "actions/upload-artifact@" in ci,
        "required official actions missing")
for action in re.findall(r"uses:\s*([^\s#]+)", ci + sql):
    if action.startswith(("actions/checkout@", "actions/setup-dotnet@", "actions/upload-artifact@")):
        ref = action.split("@", 1)[1]
        require(bool(re.fullmatch(r"[0-9a-f]{40}", ref)), f"action is not pinned to a full SHA: {action}")
require("secrets." not in ci, "normal CI must not consume repository secrets")

if errors:
    for error in errors:
        print("FAIL:", error)
    raise SystemExit(1)
print("PASS: workflow guardrails")
