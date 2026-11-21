#!/usr/bin/env python3
"""Summarise Unity/NUnit XML results as Markdown; exit 1 on failures or when nothing ran."""
import os
import sys
import xml.etree.ElementTree as ET


def summarise(path):
    if not os.path.exists(path):
        return None, f"| {os.path.basename(path)} | missing | | | |"
    root = ET.parse(path).getroot()
    total = int(root.get("total", 0))
    passed = int(root.get("passed", 0))
    failed = int(root.get("failed", 0))
    skipped = int(root.get("skipped", 0)) + int(root.get("inconclusive", 0))
    failures = [tc.get("fullname") for tc in root.iter("test-case") if tc.get("result") == "Failed"]
    return (total, failed, failures), f"| {os.path.basename(path)} | {total} | {passed} | {failed} | {skipped} |"


def main(paths):
    lines = ["| Results | Total | Passed | Failed | Skipped |", "|---|---|---|---|---|"]
    ok = True
    failures = []
    for path in paths:
        stats, line = summarise(path)
        lines.append(line)
        if stats is None or stats[0] == 0 or stats[1] > 0:
            ok = False
        if stats:
            failures.extend(stats[2])
    if failures:
        lines.append("")
        lines.append("Failed tests:")
        lines.extend(f"- `{name}`" for name in failures[:50])
    print("\n".join(lines))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
