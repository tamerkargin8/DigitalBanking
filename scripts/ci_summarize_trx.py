#!/usr/bin/env python3
"""Parse a VSTest TRX file and emit a small, secret-free count summary."""
import argparse
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("trx")
parser.add_argument("--expect-skipped", type=int)
parser.add_argument("--expect-total", type=int)
parser.add_argument("--output")
args = parser.parse_args()
path = Path(args.trx)
if not path.is_file():
    raise SystemExit(f"TRX not found: {path}")
root = ET.parse(path).getroot()
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
counters = root.find(".//t:ResultSummary/t:Counters", ns)
if counters is None:
    raise SystemExit("TRX has no ResultSummary/Counters")
total = int(counters.get("total", "0"))
executed = int(counters.get("executed", "0"))
passed = int(counters.get("passed", "0"))
failed = int(counters.get("failed", "0"))
# VSTest can omit notExecuted even when individual results are NotExecuted.
# Count explicit skipped outcomes rather than inferring total - executed.
results = root.findall(".//t:Results/t:UnitTestResult", ns)
skipped_results = sum(r.get("outcome") == "NotExecuted" for r in results)
skipped = max(int(counters.get("notExecuted", "0")), skipped_results)
lines = [f"total={total} executed={executed} passed={passed} failed={failed} skipped={skipped}"]
if args.expect_total is not None and total != args.expect_total:
    lines.append(f"FAIL: expected total {args.expect_total}")
    print("\n".join(lines))
    sys.exit(1)
if args.expect_skipped is not None and skipped != args.expect_skipped:
    lines.append(f"FAIL: expected skipped {args.expect_skipped}")
    print("\n".join(lines))
    sys.exit(1)
print("\n".join(lines))
if args.output:
    Path(args.output).parent.mkdir(parents=True, exist_ok=True)
    Path(args.output).write_text(lines[0] + "\n", encoding="utf-8")
