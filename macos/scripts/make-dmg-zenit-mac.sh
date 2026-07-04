#!/bin/zsh
# Builds a shareable ZenIT.dmg containing only the app bundle (drag-to-Applications layout).
# Output: macos/publish/ZenIT-Mac-<rid>.dmg
# Usage: ./make-dmg-zenit-mac.sh [osx-arm64|osx-x64|all]   (default: osx-arm64)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
MACOS_ROOT="$(dirname "$SCRIPT_DIR")"
PUBLISH_ROOT="$MACOS_ROOT/publish"

RID_ARG="${1:-osx-arm64}"
if [[ "$RID_ARG" == "all" ]]; then
    RIDS=(osx-arm64 osx-x64)
else
    RIDS=("$RID_ARG")
fi

for RID in "${RIDS[@]}"; do
    APP_BUNDLE="$PUBLISH_ROOT/$RID/ZenIT.app"
    if [[ ! -d "$APP_BUNDLE" ]]; then
        echo "==> ZenIT.app for $RID not found, publishing first"
        "$SCRIPT_DIR/publish-zenit-mac.sh" "$RID"
    fi

    STAGING="$(mktemp -d)/ZenIT"
    mkdir -p "$STAGING"
    ditto "$APP_BUNDLE" "$STAGING/ZenIT.app"
    ln -s /Applications "$STAGING/Applications"

    DMG_PATH="$PUBLISH_ROOT/ZenIT-Mac-$RID.dmg"
    rm -f "$DMG_PATH"
    hdiutil create -volname "ZenIT" -srcfolder "$STAGING" -ov -format UDZO "$DMG_PATH" >/dev/null
    rm -rf "$(dirname "$STAGING")"

    echo "==> Created $DMG_PATH"
done

echo "Done. Share the .dmg; on the target Mac open it and drag ZenIT into Applications."
