#!/usr/bin/env bash
# Verifies the downloaded Xcode-project artifact came from this workflow run and the expected
# commit before anything is built or signed (spec §7 step 4 "verify its provenance").
# Usage: ci/ios/verify-artifact.sh <artifact-dir> <expected-commit> <expected-run-id>
set -euo pipefail
dir=$1
expected_commit=$2
expected_run=$3
provenance="$dir/provenance.json"
[[ -f "$provenance" ]] || { echo "::error::provenance.json missing from artifact"; exit 1; }

read_field() { python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))[sys.argv[2]])' "$provenance" "$1"; }

archive="$dir/$(read_field archive)"
[[ -f "$archive" ]] || { echo "::error::archive $(read_field archive) missing"; exit 1; }
actual_sha=$(shasum -a 256 "$archive" | cut -d' ' -f1)
if [[ "$actual_sha" != "$(read_field sha256)" ]]; then
  echo "::error::archive checksum mismatch"; exit 1
fi
if [[ "$(read_field commit)" != "$expected_commit" ]]; then
  echo "::error::artifact commit $(read_field commit) != expected $expected_commit"; exit 1
fi
if [[ "$(read_field run_id)" != "$expected_run" ]]; then
  echo "::error::artifact run $(read_field run_id) != this run $expected_run"; exit 1
fi
echo "Provenance OK: commit $(read_field commit), run $(read_field run_id), Unity $(read_field unity_version), sha256 ${actual_sha}"
mkdir -p "$dir/xcode"
tar -xzf "$archive" -C "$dir/xcode"
test -d "$dir/xcode/iOS/Unity-iPhone.xcodeproj" || { echo "::error::Unity-iPhone.xcodeproj not found in archive"; exit 1; }
