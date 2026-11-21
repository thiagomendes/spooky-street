#!/usr/bin/env bash
# Diagnostic only: runs inside the pinned GameCI editor container and reports which Unity
# licence activation method works for this account. Never prints secret values.
# Writes /out/summary.md. Hardware identifiers and credentials are filtered from output.
set -uo pipefail

out=/out
mkdir -p "$out"
summary="$out/summary.md"
editor_dir=$(dirname "$(readlink -f "$(command -v unity-editor)" 2>/dev/null || echo /opt/unity/Editor/Unity)")
client=$(find /opt/unity -path '*Licensing/Client/Unity.Licensing.Client' -type f 2>/dev/null | head -n 1)

dbus-uuidgen > /etc/machine-id
mkdir -p /var/lib/dbus
ln -sf /etc/machine-id /var/lib/dbus/machine-id
blank=/tmp/BlankProject
mkdir -p "$blank/Assets"

licence_lines() {
  # Licensing-related lines only; drop anything that could echo a credential argument.
  grep -iE "licen[cs]|entitlement|activat|token|seat|serial|ulf" "$1" \
    | grep -viE "password|-serial |DeveloperData|MachineBinding|Current context|License value" | tail -n 40
}

client_logs() {
  for log in "$HOME/.config/unity3d/Unity/Unity.Licensing.Client.log" \
             "$HOME/.config/unity3d/Unity/Unity.Entitlements.Audit.log"; do
    if [[ -f "$log" ]]; then
      echo "--- $(basename "$log") (filtered tail)"
      grep -viE "password|token\"?: ?\"[A-Za-z0-9]|MachineBinding|Current context|License value" "$log" | tail -n 25
    fi
  done
}

try_editor() {
  local name=$1
  shift
  local log="$out/${name}.log"
  echo "::group::Method ${name}"
  unity-editor -batchmode -nographics -quit -projectPath "$blank" -logFile - "$@" > "$log" 2>&1
  local code=$?
  licence_lines "$log"
  client_logs
  echo "::endgroup::"
  if [[ $code -eq 0 ]] && ! grep -qiE "No valid Unity Editor license|license is not valid|Failed to activate" "$log"; then
    echo "| ${name} | ✅ works (exit 0) |" >> "$summary"
    return 0
  fi
  echo "| ${name} | ❌ failed (exit ${code}) |" >> "$summary"
  return 1
}

{
  echo "## Unity licence probe — $(head -n 1 /project/ProjectSettings/ProjectVersion.txt)"
  echo
  echo "| Method | Result |"
  echo "|---|---|"
} > "$summary"

echo "Editor dir: $editor_dir"
if [[ -n "$client" ]]; then
  echo "::group::Unity.Licensing.Client --help"
  "$client" --help 2>&1 | head -n 80
  echo "::endgroup::"
fi

client_cmd() {
  # Runs Unity.Licensing.Client with the account credentials; prints filtered output only.
  "$client" "$@" --username "$UNITY_EMAIL" --password "$UNITY_PASSWORD" 2>&1 \
    | grep -viE "password|MachineBinding|Current context|License value|DeveloperData" | tail -n 30
  return "${PIPESTATUS[0]}"
}

ulf_report() {
  local found
  found=$(find /root /usr/share/unity3d /etc/unity3d -name '*.ulf' 2>/dev/null | head -n 1)
  if [[ -n "$found" ]]; then
    echo "ULF created at ${found}; contains DeveloperData: $(grep -q 'DeveloperData' "$found" && echo yes || echo no)"
  else
    echo "No .ulf file found after activation."
  fi
}

if [[ -z "${UNITY_EMAIL:-}" || -z "${UNITY_PASSWORD:-}" || -z "$client" ]]; then
  echo "| all | ⏭️ skipped (credentials or licensing client missing) |" >> "$summary"
else
  # D) Personal ULF via the licensing client (no serial).
  echo "::group::Method D: Unity.Licensing.Client --activate-ulf"
  client_cmd --activate-ulf
  echo "client exit: $?"
  ulf_report
  echo "::endgroup::"
  try_editor "D-activate-ulf" && echo "| D-activate-ulf detail | $(ulf_report) |" >> "$summary"
  echo "::group::Return ULF"
  client_cmd --return-ulf
  echo "::endgroup::"

  # E) Activate all subscriptions with Personal fallback.
  echo "::group::Method E: Unity.Licensing.Client --activate-all --include-personal"
  client_cmd --activate-all --include-personal
  echo "client exit: $?"
  "$client" --showEntitlements 2>&1 | grep -iE "entitlement|com\.unity|personal|expir" | head -n 20
  echo "::endgroup::"
  try_editor "E-activate-all-personal" || true
  echo "::group::Deactivate (free the seat)"
  client_cmd --deactivate-all
  echo "::endgroup::"
fi

# Logs may mention the account; keep only the summary and the .alf in the artifact.
rm -f "$out"/*.log
cat "$summary"
