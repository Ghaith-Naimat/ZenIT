#!/bin/bash
# Validates the macOS ZenIT package and, optionally, performs a silent install.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PKG_PATH="$REPO_ROOT/publish/macos/ZenIT-macOS.pkg"
INSTALL="false"
LAUNCH="false"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --pkg) PKG_PATH="$2"; shift 2 ;;
    --install) INSTALL="true"; shift ;;
    --launch) LAUNCH="true"; shift ;;
    -h|--help)
      echo "Usage: test-zenit-install.sh [--pkg path] [--install] [--launch]"
      exit 0
      ;;
    *) echo "Unknown argument: $1" >&2; exit 1 ;;
  esac
done

failures=()

pass() { echo "[PASS] $1"; }
fail() { echo "[FAIL] $1"; failures+=("$1"); }

if [[ -f "$PKG_PATH" ]]; then
  pass "Package exists: $PKG_PATH"
  SIZE_BYTES="$(stat -f%z "$PKG_PATH")"
  SIZE_MIB="$(awk "BEGIN { printf \"%.2f\", $SIZE_BYTES / 1048576 }")"
  echo "[INFO] Package size: $SIZE_MIB MiB"
else
  fail "Package exists: $PKG_PATH"
fi

if command -v pkgutil >/dev/null 2>&1 && [[ -f "$PKG_PATH" ]]; then
  pkgutil --check-signature "$PKG_PATH" || true
  pkgutil --expand-full "$PKG_PATH" "$(mktemp -d)/ZenITPkg" >/dev/null 2>&1 && pass "Package expands successfully" || fail "Package expands successfully"
fi

if [[ "$INSTALL" == "true" ]]; then
  if [[ "$(id -u)" -ne 0 ]]; then
    fail "Silent install requires sudo/root"
  else
    installer -pkg "$PKG_PATH" -target /
    pass "Silent installer command completed"
  fi
fi

APP_PATH="/Applications/ZenIT.app"
CONFIG_PATH="/Library/Application Support/ZenIT/Config/appsettings.json"
POLICY_PATH="/Library/Application Support/ZenIT/Policy/itpolicy.json"
LOG_DIR="/Library/Logs/ZenIT"
REPORT_DIR="/Users/Shared/ZenIT/Reports"

if [[ -d "$APP_PATH" ]]; then pass "/Applications/ZenIT.app exists"; else fail "/Applications/ZenIT.app exists"; fi
if [[ -f "$CONFIG_PATH" ]]; then pass "appsettings.json exists"; else fail "appsettings.json exists"; fi
if [[ -f "$POLICY_PATH" ]]; then
  pass "itpolicy.json exists"
  if /usr/bin/python3 - "$POLICY_PATH" <<'PY'
import json, sys
path = sys.argv[1]
with open(path, encoding="utf-8") as f:
    data = json.load(f)
checks = {
    "EnableITMode true": data.get("EnableITMode") is True,
    "Username is Ghaith": data.get("ITModeUsername") == "Ghaith",
    "Timeout is 15": data.get("ITModeSessionTimeoutMinutes") == 15,
    "Credential changes disabled": data.get("AllowITCredentialChanges") is False,
    "Password hash present": data.get("ITModePasswordHash") == "95FAB1FCF914BB5E3D56891BD2B1D03B40DD6066D3ED1327798A9673BB0A30FC",
}
failed = [name for name, ok in checks.items() if not ok]
if failed:
    print("[FAIL] " + "; ".join(failed))
    sys.exit(1)
print("[PASS] IT policy values are correct")
PY
  then
    :
  else
    fail "IT policy values are correct"
  fi
else
  fail "itpolicy.json exists"
fi

if [[ -d "$LOG_DIR" && -w "$LOG_DIR" ]]; then pass "Logs folder writable"; else fail "Logs folder writable"; fi
if [[ -d "$REPORT_DIR" && -w "$REPORT_DIR" ]]; then pass "Reports folder writable"; else fail "Reports folder writable"; fi
if [[ -f "$POLICY_PATH" && ! -w "$POLICY_PATH" ]]; then pass "Policy is read-only for current non-root user"; else echo "[INFO] Policy write check skipped or current user can write (expected for root/admin validation)."; fi

if [[ "$LAUNCH" == "true" ]]; then
  open -gj "$APP_PATH"
  sleep 5
  if pgrep -f "ZenIT" >/dev/null 2>&1; then
    pass "ZenIT launches"
    pkill -f "ZenIT" || true
  else
    fail "ZenIT launches"
  fi
fi

if [[ ${#failures[@]} -gt 0 ]]; then
  echo "[RESULT] ZenIT macOS validation failed: ${failures[*]}"
  exit 1
fi

echo "[RESULT] ZenIT macOS validation passed."
