#!/usr/bin/env python3
"""Write provenance.json for the exported Xcode project archive (read back by ci/ios/verify-artifact.sh)."""
import hashlib
import json
import os
import re
import sys


def main(archive, output):
    sha = hashlib.sha256()
    with open(archive, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            sha.update(chunk)
    with open("ProjectSettings/ProjectVersion.txt", encoding="utf-8") as f:
        unity = re.search(r"m_EditorVersion:\s*(\S+)", f.read()).group(1)
    with open("ProjectSettings/ProjectSettings.asset", encoding="utf-8") as f:
        version = re.search(r"^  bundleVersion: (\S+)", f.read(), re.M).group(1)
    data = {
        "schema": 1,
        "archive": os.path.basename(archive),
        "sha256": sha.hexdigest(),
        "size_bytes": os.path.getsize(archive),
        "commit": os.environ["GITHUB_SHA_TO_BUILD"],
        "run_id": os.environ["GITHUB_RUN_ID"],
        "run_number": os.environ["GITHUB_RUN_NUMBER"],
        "run_attempt": os.environ["GITHUB_RUN_ATTEMPT"],
        "workflow_ref": os.environ.get("GITHUB_WORKFLOW_REF", ""),
        "unity_version": unity,
        "unity_image": os.environ.get("UNITY_IMAGE", ""),
        "marketing_version": version,
        "export_build_number": os.environ.get("EXPORT_BUILD_NUMBER", ""),
    }
    with open(output, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2)
        f.write("\n")
    print(json.dumps(data, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1], sys.argv[2]))
