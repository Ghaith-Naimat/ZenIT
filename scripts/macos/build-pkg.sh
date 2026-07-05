#!/bin/bash
# Builds a JumpCloud-ready macOS .pkg installer for ZenIT.
#
# Output:
#   publish/macos/ZenIT-macOS.pkg
#
# Examples:
#   ./scripts/macos/build-pkg.sh
#   ./scripts/macos/build-pkg.sh --rid osx-x64
#   ./scripts/macos/build-pkg.sh --sign "Developer ID Installer: ZenHR (...)" --notarize --keychain-profile "AC_PASSWORD"

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
MACOS_ROOT="$REPO_ROOT/macos"
RID="osx-arm64"
APP_INPUT="$MACOS_ROOT/ZenIT.app"
OUTPUT_DIR="$REPO_ROOT/publish/macos"
OUTPUT_PKG="$OUTPUT_DIR/ZenIT-macOS.pkg"
SIGN_IDENTITY="${DEVELOPER_ID_INSTALLER:-}"
NOTARIZE="false"
KEYCHAIN_PROFILE="${NOTARY_KEYCHAIN_PROFILE:-}"
APPLE_ID="${NOTARY_APPLE_ID:-}"
TEAM_ID="${NOTARY_TEAM_ID:-}"
PASSWORD="${NOTARY_PASSWORD:-}"

usage() {
  cat <<USAGE
Usage: build-pkg.sh [options]

Options:
  --rid <osx-arm64|osx-x64>          Runtime identifier to publish when macos/ZenIT.app is missing.
  --app <path-to-ZenIT.app>          App bundle to package. Default: macos/ZenIT.app.
  --sign <Developer ID Installer>    Sign final pkg with Developer ID Installer identity.
  --notarize                         Submit and staple with xcrun notarytool/stapler.
  --keychain-profile <profile>       notarytool keychain profile.
  --apple-id <email>                 Apple ID for notarytool.
  --team-id <team-id>                Apple developer team ID for notarytool.
  --password <app-password>          App-specific password for notarytool.
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --rid) RID="$2"; shift 2 ;;
    --app) APP_INPUT="$2"; shift 2 ;;
    --sign) SIGN_IDENTITY="$2"; shift 2 ;;
    --notarize) NOTARIZE="true"; shift ;;
    --keychain-profile) KEYCHAIN_PROFILE="$2"; shift 2 ;;
    --apple-id) APPLE_ID="$2"; shift 2 ;;
    --team-id) TEAM_ID="$2"; shift 2 ;;
    --password) PASSWORD="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage; exit 1 ;;
  esac
done

require_tool() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "error: required tool not found: $1" >&2
    exit 1
  fi
}

require_tool pkgbuild
require_tool productbuild
require_tool ditto

if [[ ! -d "$APP_INPUT" ]]; then
  echo "ZenIT.app not found at $APP_INPUT. Publishing macOS app for $RID..."
  "$MACOS_ROOT/scripts/publish-zenit-mac.sh" "$RID"
  APP_INPUT="$MACOS_ROOT/publish/$RID/ZenIT.app"
fi

if [[ ! -d "$APP_INPUT" ]]; then
  echo "error: ZenIT.app was not found after publish: $APP_INPUT" >&2
  exit 1
fi

rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

PAYLOAD_ROOT="$WORK_DIR/payload"
PKG_SCRIPTS="$WORK_DIR/scripts"
COMPONENT_PKG="$WORK_DIR/ZenIT-component.pkg"
mkdir -p "$PAYLOAD_ROOT/Applications" "$PKG_SCRIPTS"

ditto "$APP_INPUT" "$PAYLOAD_ROOT/Applications/ZenIT.app"
cp "$SCRIPT_DIR/install-zenit.sh" "$PKG_SCRIPTS/postinstall"
tr -d '\r' < "$PKG_SCRIPTS/postinstall" > "$PKG_SCRIPTS/postinstall.tmp"
mv "$PKG_SCRIPTS/postinstall.tmp" "$PKG_SCRIPTS/postinstall"
chmod 755 "$PKG_SCRIPTS/postinstall"

pkgbuild \
  --root "$PAYLOAD_ROOT" \
  --scripts "$PKG_SCRIPTS" \
  --identifier "com.zenhr.zenit" \
  --version "1.0.0" \
  --install-location "/" \
  "$COMPONENT_PKG"

PRODUCTBUILD_ARGS=(--package "$COMPONENT_PKG")
SIGNING_STATUS="Unsigned"
if [[ -n "$SIGN_IDENTITY" ]]; then
  PRODUCTBUILD_ARGS+=(--sign "$SIGN_IDENTITY")
  SIGNING_STATUS="Signed with $SIGN_IDENTITY"
else
  echo "warning: Unsigned package. Unsigned/not notarized package may trigger Gatekeeper warnings." >&2
fi
PRODUCTBUILD_ARGS+=("$OUTPUT_PKG")

productbuild "${PRODUCTBUILD_ARGS[@]}"

NOTARIZATION_STATUS="Not notarized"
if [[ "$NOTARIZE" == "true" ]]; then
  require_tool xcrun
  if [[ -n "$KEYCHAIN_PROFILE" ]]; then
    xcrun notarytool submit "$OUTPUT_PKG" --keychain-profile "$KEYCHAIN_PROFILE" --wait
  elif [[ -n "$APPLE_ID" && -n "$TEAM_ID" && -n "$PASSWORD" ]]; then
    xcrun notarytool submit "$OUTPUT_PKG" --apple-id "$APPLE_ID" --team-id "$TEAM_ID" --password "$PASSWORD" --wait
  else
    echo "error: notarization requested but credentials are missing. Provide --keychain-profile or --apple-id/--team-id/--password." >&2
    exit 1
  fi
  xcrun stapler staple "$OUTPUT_PKG"
  NOTARIZATION_STATUS="Notarized and stapled"
elif [[ -z "$SIGN_IDENTITY" ]]; then
  echo "warning: Unsigned/not notarized package may trigger Gatekeeper warnings." >&2
fi

SIZE_BYTES="$(stat -f%z "$OUTPUT_PKG")"
SIZE_MIB="$(awk "BEGIN { printf \"%.2f\", $SIZE_BYTES / 1048576 }")"

echo "Package: $OUTPUT_PKG"
echo "Size: $SIZE_MIB MiB"
echo "Signing status: $SIGNING_STATUS"
echo "Notarization status: $NOTARIZATION_STATUS"
