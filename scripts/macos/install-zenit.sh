#!/bin/bash
# Configures a macOS ZenIT installation. The .pkg payload installs ZenIT.app
# into /Applications; this script creates machine-wide config, policy, logs,
# and reports paths with deployment-safe permissions.

set -euo pipefail

TARGET_VOLUME="${3:-/}"
if [[ "$TARGET_VOLUME" == "/" ]]; then
  PREFIX=""
else
  PREFIX="$TARGET_VOLUME"
fi

APP_SOURCE="${1:-}"
APP_TARGET="$PREFIX/Applications/ZenIT.app"

CONFIG_DIR="$PREFIX/Library/Application Support/ZenIT/Config"
POLICY_DIR="$PREFIX/Library/Application Support/ZenIT/Policy"
LOG_DIR="$PREFIX/Library/Logs/ZenIT"
REPORT_DIR="$PREFIX/Users/Shared/ZenIT/Reports"
CONFIG_PATH="$CONFIG_DIR/appsettings.json"
POLICY_PATH="$POLICY_DIR/itpolicy.json"
INSTALL_LOG="$LOG_DIR/Install.log"

log() {
  mkdir -p "$LOG_DIR"
  printf '[%s] %s\n' "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" "$1" >> "$INSTALL_LOG" || true
}

write_json_atomic() {
  local target="$1"
  local temp
  temp="${target}.$(uuidgen | tr '[:upper:]' '[:lower:]').tmp"
  cat > "$temp"
  if [[ -f "$target" ]]; then
    cp "$target" "$target.bak" || true
  fi
  mv "$temp" "$target"
}

install_app_if_requested() {
  if [[ -n "$APP_SOURCE" ]]; then
    if [[ ! -d "$APP_SOURCE" ]]; then
      echo "ZenIT.app source not found: $APP_SOURCE" >&2
      exit 1
    fi
    rm -rf "$APP_TARGET"
    mkdir -p "$(dirname "$APP_TARGET")"
    ditto "$APP_SOURCE" "$APP_TARGET"
  fi
}

create_folders() {
  mkdir -p "$CONFIG_DIR" "$POLICY_DIR" "$LOG_DIR" "$REPORT_DIR"
}

write_config() {
  write_json_atomic "$CONFIG_PATH" <<'JSON'
{
  "Language": "en",
  "Theme": "Dark",
  "UpdateChannel": "Production"
}
JSON
}

write_policy() {
  write_json_atomic "$POLICY_PATH" <<'JSON'
{
  "EnableITMode": true,
  "ITModeUsername": "Ghaith",
  "ITModePasswordHash": "95FAB1FCF914BB5E3D56891BD2B1D03B40DD6066D3ED1327798A9673BB0A30FC",
  "AllowITCredentialChanges": false,
  "ContactITUrl": "https://zenhr.slack.com/team/U09CGMUGV6K",
  "ITModeSessionTimeoutMinutes": 15,
  "AllowedITWorkflows": []
}
JSON
}

apply_permissions() {
  chown -R root:admin "$PREFIX/Library/Application Support/ZenIT" "$LOG_DIR" "$PREFIX/Users/Shared/ZenIT" 2>/dev/null || true

  chmod 1777 "$PREFIX/Library/Application Support/ZenIT"
  chmod 1777 "$CONFIG_DIR"
  chmod 755 "$POLICY_DIR"
  chmod 666 "$CONFIG_PATH"
  chmod 644 "$POLICY_PATH"

  # Logs and reports are intentionally writable by local users for employee-run
  # support package creation without requiring elevation.
  chmod 1777 "$LOG_DIR"
  chmod 1777 "$REPORT_DIR"
}

main() {
  log "Starting ZenIT macOS configuration."
  install_app_if_requested
  create_folders
  write_config
  write_policy
  apply_permissions
  log "ZenIT macOS configuration completed successfully."
  echo "ZenIT configured successfully."
}

main "$@"
