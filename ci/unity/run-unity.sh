#!/usr/bin/env bash
# Runs one or more Unity batch steps inside a pinned GameCI editor image on a GitHub-hosted
# Linux runner. Credentials arrive as environment variables and are passed to Docker by name
# only, never on the host command line.
#
# Usage: ci/unity/run-unity.sh <image> <step> [<step> ...]
#   <step> is "<log-name>::<unity arguments separated by spaces>", for example
#   "editmode::-runTests -testPlatform EditMode -testResults /project/ci-out/editmode-results.xml"
#
# Licence (repository secrets):
#   Personal: UNITY_EMAIL + UNITY_PASSWORD (activated through Unity.Licensing.Client; verified by
#             the "Unity licence probe" workflow on 2026-11-21 with Unity 6000.3.28f1)
#   Plus/Pro: additionally UNITY_SERIAL
set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "usage: $0 <image> <log-name>::<unity args> [...]" >&2
  exit 64
fi

image=$1
shift

if [[ -z "${UNITY_EMAIL:-}" || -z "${UNITY_PASSWORD:-}" ]]; then
  echo "::error title=Unity activation::UNITY_EMAIL and UNITY_PASSWORD secrets are required (docs/ci-setup.md)."
  exit 78
fi
if [[ -n "${UNITY_SERIAL:-}" ]]; then
  echo "::add-mask::${UNITY_SERIAL}"
fi

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
