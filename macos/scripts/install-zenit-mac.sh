#!/bin/zsh
# Installs ZenIT.app into /Applications (falls back to ~/Applications without sudo)
# and provisions the per-user IT Mode policy file.
# Usage: ./install-zenit-mac.sh [path-to-ZenIT.app]

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
MACOS_ROOT="$(dirname "$SCRIPT_DIR")"
DEFAULT_APP="$MACOS_ROOT/publish/osx-arm64/ZenIT.app"
APP_SOURCE="${1:-$DEFAULT_APP}"

if [[ ! -d "$APP_SOURCE" ]]; then
    echo "error: ZenIT.app not found at $APP_SOURCE. Run publish-zenit-mac.sh first." >&2
    exit 1
fi

TARGET="/Applications/ZenIT.app"
if [[ -w "/Applications" ]]; then
    rm -rf "$TARGET"
    ditto "$APP_SOURCE" "$TARGET"
else
    TARGET="$HOME/Applications/ZenIT.app"
    mkdir -p "$HOME/Applications"
    rm -rf "$TARGET"
    ditto "$APP_SOURCE" "$TARGET"
fi

# Provision IT Mode policy. The app treats a missing policy file as IT Mode disabled,
# matching the Windows deployment where the installer delivers itpolicy.json.
POLICY_DIR="$HOME/Library/Application Support/ZenIT/Policy"
POLICY_PATH="$POLICY_DIR/itpolicy.json"
mkdir -p "$POLICY_DIR"
if [[ ! -f "$POLICY_PATH" ]]; then
    cat > "$POLICY_PATH" <<'JSON'
{
  "EnableITMode": true,
  "ITModeUsername": "Ghaith",
  "ITModePasswordHash": "95FAB1FCF914BB5E3D56891BD2B1D03B40DD6066D3ED1327798A9673BB0A30FC",
  "AllowITCredentialChanges": false,
  "ITModeSessionTimeoutMinutes": 15,
  "ContactITUrl": "https://zenhr.slack.com/team/U09CGMUGV6K",
  "AllowedITWorkflows": []
}
JSON
fi

echo "Installed: $TARGET"
echo "IT policy: $POLICY_PATH"
echo "Launch with: open \"$TARGET\""
