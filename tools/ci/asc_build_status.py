#!/usr/bin/env python3
"""Poll App Store Connect until an uploaded build finishes processing (read-only API calls).

Environment: ASC_KEY_ID, ASC_ISSUER_ID, ASC_KEY_P8_BASE64.
Usage: asc_build_status.py --bundle-id ID --version 0.0.1 --build-number 57.1 [--timeout 1800]
Writes a Markdown line to $GITHUB_STEP_SUMMARY when available. Exit 0 = VALID,
1 = FAILED/INVALID, 2 = still processing at timeout or not found.
"""
import argparse
import base64
import json
import os
import sys
import time
import urllib.parse
import urllib.request

import jwt

API = "https://api.appstoreconnect.apple.com/v1"


def token():
    key = base64.b64decode(os.environ["ASC_KEY_P8_BASE64"]).decode()
    now = int(time.time())
    return jwt.encode({"iss": os.environ["ASC_ISSUER_ID"], "iat": now, "exp": now + 900, "aud": "appstoreconnect-v1"},
                      key, algorithm="ES256", headers={"kid": os.environ["ASC_KEY_ID"], "typ": "JWT"})


def get(path, params):
    url = f"{API}/{path}?{urllib.parse.urlencode(params)}"
    request = urllib.request.Request(url, headers={"Authorization": f"Bearer {token()}"})
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)


def summary(text):
    print(text)
    path = os.environ.get("GITHUB_STEP_SUMMARY")
    if path:
        with open(path, "a", encoding="utf-8") as f:
            f.write(text + "\n")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--bundle-id", required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--build-number", required=True)
    parser.add_argument("--timeout", type=int, default=1800)
    parser.add_argument("--interval", type=int, default=60)
    args = parser.parse_args()

    apps = get("apps", {"filter[bundleId]": args.bundle_id})["data"]
    if not apps:
        summary(f"App Store Connect has no app record for `{args.bundle_id}`.")
        return 2
    app_id = apps[0]["id"]
    deadline = time.time() + args.timeout
    state = "NOT_FOUND"
    while time.time() < deadline:
        builds = get("builds", {"filter[app]": app_id, "filter[version]": args.build_number,
                                "filter[preReleaseVersion.version]": args.version, "limit": 1})["data"]
        if builds:
            state = builds[0]["attributes"].get("processingState", "UNKNOWN")
            print(f"{time.strftime('%H:%M:%S')} build {args.version} ({args.build_number}): {state}", flush=True)
            if state in ("VALID", "FAILED", "INVALID"):
                break
        else:
            print(f"{time.strftime('%H:%M:%S')} build {args.build_number} not visible yet", flush=True)
        time.sleep(args.interval)

    summary(f"**App Store Connect processing:** version {args.version} build {args.build_number} → `{state}`")
    if state == "VALID":
        return 0
    return 1 if state in ("FAILED", "INVALID") else 2


if __name__ == "__main__":
    sys.exit(main())
