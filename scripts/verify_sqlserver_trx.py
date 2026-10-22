#!/usr/bin/env python3
"""Fail unless the real SQL Server suite actually ran all eight tests."""
import argparse
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("trx")
parser.add_argument("--expected", type=int, default=8)
parser.add_argument("--output")
args = parser.parse_args()
path = Path(args.trx)
if not path.is_file():
    raise SystemExit(f"TRX not found: {path}")
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
counters = ET.parse(path).getroot().find(".//t:ResultSummary/t:Counters", ns)
if counters is None:
    raise SystemExit("TRX has no ResultSummary/Counters")
total = int(counters.get("total", "0"))
passed = int(counters.get("passed", "0"))
failed = int(counters.get("failed", "0"))
skipped = int(counters.get("notExecuted", "0"))
summary = f"total={total} passed={passed} failed={failed} skipped={skipped} expected={args.expected}"
print(summary)
if args.output:
    Path(args.output).parent.mkdir(parents=True, exist_ok=True)
    Path(args.output).write_text(summary + "\n", encoding="utf-8")
if total != args.expected or passed != args.expected or failed != 0 or skipped != 0:
    print("FAIL: SQL Server tests were skipped or did not all pass; this is NOT a valid SQL Server run.")
    sys.exit(1)
