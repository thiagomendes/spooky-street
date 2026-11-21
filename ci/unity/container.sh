#!/usr/bin/env bash
# Executed inside the GameCI editor container by run-unity.sh. Activates Unity once, runs each
# requested step, then returns the licence even if a step failed.
set -uo pipefail

# Personal activations are tied to a machine id; give each ephemeral container its own.
dbus-uuidgen > /etc/machine-id
mkdir -p /var/lib/dbus
ln -sf /etc/machine-id /var/lib/dbus/machine-id

blank=/tmp/BlankProject
mkdir -p "$blank/Assets"

# shellcheck disable=SC2329 # invoked through the EXIT trap
return_licence() {
  echo "::group::Return Unity licence"
  unity-editor -batchmode -nographics -logFile - -quit -returnlicense \
    -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" -projectPath "$blank" > /dev/null 2>&1 \
    && echo "Licence returned." || echo "::warning::Licence return reported an error (Personal seats may need manual return)."
  echo "::endgroup::"
}

echo "::group::Activate Unity $(head -n 1 /project/ProjectSettings/ProjectVersion.txt)"
activated=0
delay=15
for attempt in 1 2 3; do
  if unity-editor -batchmode -nographics -logFile - -quit \
      -serial "$UNITY_SERIAL" -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" \
      -projectPath "$blank" > /tmp/activation.log 2>&1; then
    activated=1
    break
  fi
  echo "::warning::Activation attempt $attempt failed; retrying in ${delay}s."
  grep -iE "licen[cs]e|entitlement|error" /tmp/activation.log | grep -v -- "-password" | tail -n 15 || true
  sleep "$delay"
  delay=$((delay * 2))
done
echo "::endgroup::"
if [[ $activated -ne 1 ]]; then
  echo "::error title=Unity activation failed::Check UNITY_LICENSE/UNITY_SERIAL, UNITY_EMAIL and UNITY_PASSWORD, and that a seat is free."
  exit 78
fi
trap return_licence EXIT

overall=0
for step in "$@"; do
  name=${step%%::*}
  args=${step#*::}
  log="/project/ci-out/unity-${name}.log"
  echo "::group::Unity step: ${name}"
  # shellcheck disable=SC2086 # step arguments are space-separated by contract and contain no spaces themselves
  unity-editor -batchmode -nographics -projectPath /project -logFile - $args 2>&1 | tee "$log"
  code=${PIPESTATUS[0]}
  echo "::endgroup::"
  echo "Unity step '${name}' exited with ${code}"
  if grep -q "Scripts have compiler errors" "$log"; then
    echo "::error title=Unity compile errors (${name})::$(grep -m5 -E 'error CS[0-9]+' "$log" | tr '\n' ' ')"
  fi
  if [[ $code -ne 0 && $overall -eq 0 ]]; then
    overall=$code
  fi
done
exit "$overall"
