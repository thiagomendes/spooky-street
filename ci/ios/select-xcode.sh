#!/usr/bin/env bash
# Selects the newest installed Xcode whose major version matches $1 (default 26) and prints its
# version. App Store Connect enforces a minimum Xcode/SDK for uploads; keep this in step with Apple.
set -euo pipefail
major=${1:-26}
candidate=$(find /Applications -maxdepth 1 \( -name "Xcode_${major}*.app" -o -name "Xcode-${major}*.app" \) | sort -V | tail -n 1)
if [[ -z "$candidate" ]]; then
  echo "::error title=Xcode ${major} not found::Installed: $(find /Applications -maxdepth 1 -name 'Xcode*.app' -exec basename {} \; | sort -V | tr '\n' ' '). Set the MACOS_RUNNER or XCODE_MAJOR repository variable."
  exit 1
fi
sudo xcode-select -s "$candidate/Contents/Developer"
xcodebuild -version
