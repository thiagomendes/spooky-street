#!/usr/bin/env bash
# Every check that runs without a Unity licence, editor, Mac or phone. Use in cloud sessions
# before pushing; CI runs the same set in the "Static checks" job.
# Needs: python3 with tools/requirements-ci.txt, .NET 8 SDK.
set -euo pipefail
cd "$(dirname "$0")/.."
run() { echo "==> $*"; "$@"; }
run python3 tools/ci/validate_project.py
run python3 tools/unity_meta.py --check
run actionlint
run shellcheck ci/unity/*.sh ci/ios/*.sh tools/check-local.sh
run yamllint -c .yamllint.yml .github
if command -v ruby > /dev/null; then run ruby -c ios/fastlane/Fastfile; fi
run dotnet test tools/core-tests --nologo
run python3 -m unittest discover -s tools/audio/tests -t tools/audio
echo "All local checks passed."
