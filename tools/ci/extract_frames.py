#!/usr/bin/env python3
"""Extract review frames (and Unity-normalised files) printed in a CI job log.

Cloud sessions cannot download Actions artifacts, so ci.yml prints captured frames as base64 between
"-----BEGIN FRAME <name>-----" / "-----END FRAME <name>-----" markers. Feed this script a job log, either
raw text or the JSON saved by the GitHub MCP get_job_logs tool.

Usage: extract_frames.py <log-file> <output-dir>
"""
import base64
import json
import os
import re
import sys

TIMESTAMP = re.compile(r"^\d{4}-\d\d-\d\dT[\d:.]+Z ")


def log_lines(path):
    text = open(path, encoding="utf-8").read()
    try:
        text = json.loads(text)["logs_content"]
    except (ValueError, KeyError, TypeError):
        pass
    return [TIMESTAMP.sub("", line) for line in text.split("\n")]


def main(path, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    lines = log_lines(path)
    name, chunks, written = None, [], []
    for line in lines:
        begin = re.match(r"-----BEGIN (?:FRAME (\S+)|UNITY (NORMALIZED))-----", line)
        if begin:
            name = begin.group(1) or "unity-normalized.tar.gz"
            chunks = []
            continue
        if name and line.startswith("-----END "):
            target = os.path.join(out_dir, name)
            with open(target, "wb") as f:
                f.write(base64.b64decode("".join(chunks), validate=True))
            written.append(target)
            name = None
            continue
        if name:
            chunks.append(line.strip())
    for line in lines:
        if "[FrameCapture]" in line or "[Visual]" in line or line.startswith("| ") or line.startswith("- visual"):
            print(line[:240])
    for target in written:
        print("wrote", target, os.path.getsize(target), "bytes")
    return 0 if written else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1], sys.argv[2]))
