#!/usr/bin/env python3
"""Print the activation serial embedded in a Unity Personal licence file (.ulf).

Reads the licence from the UNITY_LICENSE environment variable (never from argv, so it
cannot leak into process listings). Same derivation GameCI uses: the base64
DeveloperData value, minus four leading bytes.
"""
import base64
import os
import re
import sys


def main():
    licence = os.environ.get("UNITY_LICENSE", "")
    match = re.search(r'<DeveloperData Value="([^"]+)"\s*/>', licence)
    if not match:
        print("UNITY_LICENSE does not look like a Unity .ulf file (no DeveloperData).", file=sys.stderr)
        return 1
    serial = base64.b64decode(match.group(1)).decode("latin-1")[4:]
    if not re.fullmatch(r"[A-Z0-9]{2}(-[A-Z0-9]{4}){5}", serial):
        print("Serial derived from UNITY_LICENSE has an unexpected format.", file=sys.stderr)
        return 1
    sys.stdout.write(serial)
    return 0


if __name__ == "__main__":
    sys.exit(main())
