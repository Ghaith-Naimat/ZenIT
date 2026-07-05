#!/bin/bash
# Configures a macOS ZenIT installation. The .pkg payload installs ZenIT.app
# into /Applications; this script creates machine-wide config, policy, logs,
# and reports paths with deployment-safe permissions.
#
# Package rule: optional configuration, ownership, and permission operations
# must not abort installation. The script exits non-zero only when ZenIT.app
# cannot be copied to or found in /Applications.

set -u

TARGET_VOLUME="${3:-/}"
if [[ "$TARGET_VOLUME" == "/" ]]; then
  PREFIX=""
else
  PREFIX="$TARGET_VOLUME"
fi

APP_SOURCE="${ZENIT_APP_SOURCE:-}"
if [[ -z "$APP_SOURCE" && "${1:-}" == *.app && -d "${1:-}" ]]; then
  APP_SOURCE="$1"
fi
APP_TARGET="$PREFIX/Applications/ZenIT.app"

CONFIG_DIR="$PREFIX/Library/Application Support/ZenIT/Config"
POLICY_DIR="$PREFIX/Library/Application Support/ZenIT/Policy"
LOG_DIR="$PREFIX/Library/Logs/ZenIT"
REPORT_DIR="$PREFIX/Users/Shared/ZenIT/Reports"
CONFIG_PATH="$CONFIG_DIR/appsettings.json"
POLICY_PATH="$POLICY_DIR/itpolicy.json"
INSTALL_LOG="$LOG_DIR/install.log"

log() {
  mkdir -p "$LOG_DIR" 2>/dev/null || true
  printf '[%s] %s\n' "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" "$1" >> "$INSTALL_LOG" 2>/dev/null || true
}

warn() {
  echo "warning: $1" >&2
  log "warning: $1"
}

fatal() {
  echo "error: $1" >&2
  log "fatal: $1"
  exit 1
}

write_json_atomic() {
  local target="$1"
  local temp
  local target_dir
  target_dir="$(dirname "$target")"
  mkdir -p "$target_dir" 2>/dev/null || return 1
  temp="${target}.$(uuidgen 2>/dev/null | tr '[:upper:]' '[:lower:]').tmp"
  if [[ "$temp" == "$target..tmp" ]]; then
    temp="${target}.$(date +%s).$$.tmp"
  fi
  cat > "$temp" || {
    rm -f "$temp" 2>/dev/null || true
    return 1
  }
  if [[ -f "$target" ]]; then
    cp "$target" "$target.bak" 2>/dev/null || true
  fi
  if ! mv "$temp" "$target"; then
    rm -f "$temp" 2>/dev/null || true
    return 1
  fi
}

install_app_if_requested() {
  if [[ -n "$APP_SOURCE" ]]; then
    if [[ ! -d "$APP_SOURCE" ]]; then
      fatal "ZenIT.app source not found: $APP_SOURCE"
    fi
    rm -rf "$APP_TARGET" 2>/dev/null || true
    mkdir -p "$(dirname "$APP_TARGET")" || fatal "Cannot create application directory: $(dirname "$APP_TARGET")"
    ditto "$APP_SOURCE" "$APP_TARGET" || fatal "Cannot copy ZenIT.app to $APP_TARGET"
  fi

  if [[ ! -d "$APP_TARGET" ]]; then
    fatal "ZenIT.app was not installed at $APP_TARGET"
  fi
}

create_folders() {
  mkdir -p "$CONFIG_DIR" 2>/dev/null || warn "Could not create config folder: $CONFIG_DIR"
  mkdir -p "$POLICY_DIR" 2>/dev/null || warn "Could not create policy folder: $POLICY_DIR"
  mkdir -p "$LOG_DIR" 2>/dev/null || warn "Could not create log folder: $LOG_DIR"
  mkdir -p "$REPORT_DIR" 2>/dev/null || warn "Could not create reports folder: $REPORT_DIR"
}

write_config() {
  write_json_atomic "$CONFIG_PATH" <<'JSON' || warn "Could not write appsettings.json"
{
  "Language": "en",
  "Theme": "Dark",
  "UpdateChannel": "Production"
}
JSON
}

write_policy() {
  write_json_atomic "$POLICY_PATH" <<'JSON' || warn "Could not write itpolicy.json"
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

  chmod 1777 "$PREFIX/Library/Application Support/ZenIT" 2>/dev/null || warn "Could not update permissions for Application Support root"
  chmod 1777 "$CONFIG_DIR" 2>/dev/null || warn "Could not update permissions for config folder"
  chmod 755 "$POLICY_DIR" 2>/dev/null || warn "Could not update permissions for policy folder"
  chmod 666 "$CONFIG_PATH" 2>/dev/null || warn "Could not update permissions for appsettings.json"
  chmod 644 "$POLICY_PATH" 2>/dev/null || warn "Could not update permissions for itpolicy.json"

  # Logs and reports are intentionally writable by local users for employee-run
  # support package creation without requiring elevation.
  chmod 1777 "$LOG_DIR" 2>/dev/null || warn "Could not update permissions for log folder"
  chmod 1777 "$REPORT_DIR" 2>/dev/null || warn "Could not update permissions for reports folder"
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
  exit 0
}

main "$@"
