#!/usr/bin/env bash
# Executed inside the GameCI editor container by run-unity.sh. Activates Unity once, runs each
# requested step, then releases the seat even if a step failed.
set -uo pipefail

# Activations are tied to a machine id; give each ephemeral container its own.
dbus-uuidgen > /etc/machine-id
mkdir -p /var/lib/dbus
ln -sf /etc/machine-id /var/lib/dbus/machine-id

blank=/tmp/BlankProject
mkdir -p "$blank/Assets"
client=$(find /opt/unity -path '*Licensing/Client/Unity.Licensing.Client' -type f 2>/dev/null | head -n 1)
mode=none

licensing_client() {
  "$client" "$@" --username "$UNITY_EMAIL" --password "$UNITY_PASSWORD" 2>&1 \
    | grep -viE "password|MachineBinding|Current context|License value|DeveloperData" | tail -n 15
  return "${PIPESTATUS[0]}"
}

editor_is_licensed() {
  unity-editor -batchmode -nographics -quit -projectPath "$blank" -logFile - > /tmp/licence-check.log 2>&1 \
    && ! grep -q "No valid Unity Editor license" /tmp/licence-check.log
}

# shellcheck disable=SC2329 # invoked through the EXIT trap
release_licence() {
  echo "::group::Release Unity licence ($mode)"
  case "$mode" in
    seat) licensing_client --deactivate-all || echo "::warning::Seat deactivation reported an error." ;;
    ulf) licensing_client --return-ulf || echo "::warning::ULF return reported an error." ;;
    serial)
      unity-editor -batchmode -nographics -logFile - -quit -returnlicense \
        -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" -projectPath "$blank" > /dev/null 2>&1 \
        || echo "::warning::Licence return reported an error."
      ;;
  esac
  echo "::endgroup::"
}

echo "::group::Activate Unity $(head -n 1 /project/ProjectSettings/ProjectVersion.txt)"
if [[ -n "${UNITY_SERIAL:-}" ]]; then
  unity-editor -batchmode -nographics -logFile - -quit -serial "$UNITY_SERIAL" \
    -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" -projectPath "$blank" > /tmp/activation.log 2>&1 \
    && mode=serial
elif [[ -n "$client" ]]; then
  for attempt in 1 2 3; do
    # Primary: assign the account's seat (Personal fallback) to this container.
    if licensing_client --activate-all --include-personal && editor_is_licensed; then
      mode=seat
      break
    fi
    licensing_client --deactivate-all > /dev/null || true
    # Fallback: request a Personal ULF licence file.
    if licensing_client --activate-ulf && editor_is_licensed; then
      mode=ulf
      break
    fi
    echo "::warning::Activation attempt $attempt failed; retrying in $((attempt * 20))s."
    sleep $((attempt * 20))
  done
else
  echo "Unity.Licensing.Client not found in this image."
fi
echo "Activation mode: $mode"
echo "::endgroup::"
if [[ "$mode" == none ]]; then
  echo "::error title=Unity activation failed::Check UNITY_EMAIL/UNITY_PASSWORD (no 2FA, email login not social), and that the account has a free Personal seat."
  exit 78
fi
trap release_licence EXIT

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
