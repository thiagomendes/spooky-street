#!/usr/bin/env bash
# Diagnostic only: runs inside the pinned GameCI editor container and reports which Unity
# licence activation method works for this account. Never prints secret values.
# Writes /out/summary.md and, as a fallback, /out/*.alf (manual activation request).
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
  grep -iE "licen[cs]|entitlement|activat|token|seat|serial" "$1" \
    | grep -viE "password|-serial |DeveloperData" | tail -n 40
}

client_logs() {
  for log in "$HOME/.config/unity3d/Unity/Unity.Licensing.Client.log" \
             "$HOME/.config/unity3d/Unity/Unity.Entitlements.Audit.log"; do
    if [[ -f "$log" ]]; then
      echo "--- $(basename "$log") (filtered tail)"
      grep -viE "password|token\"?: ?\"[A-Za-z0-9]" "$log" | tail -n 25
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

# A) Entitlement licence file copied from the owner's Unity Hub (UNITY_LICENSE secret).
entitlement_paths=(
  "$HOME/.local/share/unity3d/Unity/licenses"
  "$HOME/.local/share/unity3d/licenses"
  "$HOME/.config/unity3d/Unity/licenses"
)
if [[ "${UNITY_LICENSE:-}" == *"<Entitlement"* || "${UNITY_LICENSE:-}" == *"EntitlementGroup"* ]]; then
  for dir in "${entitlement_paths[@]}"; do
    mkdir -p "$dir"
    printf '%s' "$UNITY_LICENSE" > "$dir/UnityEntitlementLicense.xml"
  done
  try_editor "A-entitlement-file" || true
  for dir in "${entitlement_paths[@]}"; do rm -f "$dir/UnityEntitlementLicense.xml"; done
else
  echo "| A-entitlement-file | ⏭️ skipped (UNITY_LICENSE is not an entitlement XML) |" >> "$summary"
fi

# B) Named-user login only (email + password, no serial, no file).
if [[ -n "${UNITY_EMAIL:-}" && -n "${UNITY_PASSWORD:-}" ]]; then
  if try_editor "B-username-password" -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD"; then
    unity-editor -batchmode -nographics -quit -returnlicense -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" \
      -projectPath "$blank" -logFile - > "$out/B-return.log" 2>&1 || true
  fi
else
  echo "| B-username-password | ⏭️ skipped (UNITY_EMAIL/UNITY_PASSWORD missing) |" >> "$summary"
fi

# C) Manual activation request file (.alf), always produced as a fallback.
echo "::group::Method C-manual-activation-file"
(cd "$out" && unity-editor -batchmode -nographics -quit -createManualActivationFile -logFile - > "$out/C-alf.log" 2>&1)
alf=$(find "$out" -maxdepth 1 -name '*.alf' | head -n 1)
echo "::endgroup::"
if [[ -n "$alf" ]]; then
  echo "| C-manual-activation-file | 📄 created $(basename "$alf") (download the artifact) |" >> "$summary"
else
  echo "| C-manual-activation-file | ❌ no .alf produced |" >> "$summary"
fi

# Logs may mention the account; keep only the summary and the .alf in the artifact.
rm -f "$out"/*.log
cat "$summary"
