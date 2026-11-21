#!/usr/bin/env bash
# Runs one or more Unity batch steps inside a pinned GameCI editor image on a GitHub-hosted
# Linux runner. Licence secrets arrive as environment variables and are passed to Docker by
# name only, never on a command line.
#
# Usage: ci/unity/run-unity.sh <image> <step> [<step> ...]
#   <step> is "<log-name>::<unity arguments separated by spaces>", for example
#   "editmode::-runTests -testPlatform EditMode -testResults ci-out/editmode-results.xml"
#
# Licence (GameCI-compatible secret names):
#   Personal: UNITY_LICENSE (.ulf contents) + UNITY_EMAIL + UNITY_PASSWORD
#   Plus/Pro: UNITY_SERIAL + UNITY_EMAIL + UNITY_PASSWORD
set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "usage: $0 <image> <log-name>::<unity args> [...]" >&2
  exit 64
fi

image=$1
shift

if [[ -z "${UNITY_EMAIL:-}" || -z "${UNITY_PASSWORD:-}" ]]; then
  echo "::error title=Unity activation::UNITY_EMAIL and UNITY_PASSWORD secrets are required."
  exit 78
fi

if [[ -z "${UNITY_SERIAL:-}" ]]; then
  if [[ -z "${UNITY_LICENSE:-}" ]]; then
    echo "::error title=Unity activation::Set UNITY_LICENSE (Personal .ulf contents) or UNITY_SERIAL (Plus/Pro)."
    exit 78
  fi
  UNITY_SERIAL=$(python3 "$(dirname "$0")/serial_from_ulf.py")
fi
echo "::add-mask::${UNITY_SERIAL}"
export UNITY_SERIAL UNITY_EMAIL UNITY_PASSWORD

mkdir -p ci-out
status=0
docker run --rm \
  --workdir /project \
  --shm-size=1025m \
  --env UNITY_SERIAL --env UNITY_EMAIL --env UNITY_PASSWORD \
  --env HOME=/root \
  --volume "$PWD:/project" \
  "$image" \
  bash /project/ci/unity/container.sh "$@" || status=$?

# Unity runs as root inside the container; hand the workspace back to the runner user so
# caching, artifact upload and git can read and clean it.
sudo chown -R "$(id -u):$(id -g)" "$PWD"
exit "$status"
