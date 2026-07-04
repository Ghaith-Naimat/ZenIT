#!/bin/zsh
# Publishes ZenIT for Mac as a self-contained .app bundle.
# Output: macos/publish/<rid>/ZenIT.app and macos/publish/ZenIT-Mac-<rid>.zip
# Usage: ./publish-zenit-mac.sh [osx-arm64|osx-x64|all]   (default: osx-arm64)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
MACOS_ROOT="$(dirname "$SCRIPT_DIR")"
APP_PROJECT="$MACOS_ROOT/src/ZenIT.Mac.App/ZenIT.Mac.App.csproj"
PUBLISH_ROOT="$MACOS_ROOT/publish"
APP_VERSION="1.0.0"
BUNDLE_ID="com.zenhr.zenit"

RID_ARG="${1:-osx-arm64}"
if [[ "$RID_ARG" == "all" ]]; then
    RIDS=(osx-arm64 osx-x64)
else
    RIDS=("$RID_ARG")
fi

if ! command -v dotnet >/dev/null 2>&1; then
    if [[ -x "$HOME/.dotnet/dotnet" ]]; then
        export PATH="$HOME/.dotnet:$PATH"
    else
        echo "error: dotnet SDK not found. Install .NET 8 SDK first." >&2
        exit 1
    fi
fi

make_icns() {
    local source_png="$1"
    local output_icns="$2"
    local iconset
    iconset="$(mktemp -d)/ZenIT.iconset"
    mkdir -p "$iconset"
    for size in 16 32 64 128 256 512; do
        sips -z $size $size "$source_png" --out "$iconset/icon_${size}x${size}.png" >/dev/null
        local doubled=$((size * 2))
        sips -z $doubled $doubled "$source_png" --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
    done
    iconutil -c icns "$iconset" -o "$output_icns"
    rm -rf "$(dirname "$iconset")"
}

for RID in "${RIDS[@]}"; do
    echo "==> Publishing ZenIT for $RID"
    OUT_DIR="$PUBLISH_ROOT/$RID"
    BIN_DIR="$OUT_DIR/bin"
    rm -rf "$OUT_DIR"
    mkdir -p "$BIN_DIR"

    dotnet publish "$APP_PROJECT" \
        -c Release \
        -r "$RID" \
        --self-contained true \
        -p:PublishSingleFile=false \
        -o "$BIN_DIR"

    APP_BUNDLE="$OUT_DIR/ZenIT.app"
    CONTENTS="$APP_BUNDLE/Contents"
    mkdir -p "$CONTENTS/MacOS" "$CONTENTS/Resources"

    cp -R "$BIN_DIR/." "$CONTENTS/MacOS/"
    make_icns "$MACOS_ROOT/src/ZenIT.Mac.App/Assets/logo.png" "$CONTENTS/Resources/ZenIT.icns"

    cat > "$CONTENTS/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key><string>ZenIT</string>
    <key>CFBundleDisplayName</key><string>ZenIT</string>
    <key>CFBundleIdentifier</key><string>${BUNDLE_ID}</string>
    <key>CFBundleVersion</key><string>${APP_VERSION}</string>
    <key>CFBundleShortVersionString</key><string>${APP_VERSION}</string>
    <key>CFBundleExecutable</key><string>ZenIT</string>
    <key>CFBundleIconFile</key><string>ZenIT.icns</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>LSMinimumSystemVersion</key><string>11.0</string>
    <key>NSHighResolutionCapable</key><true/>
    <key>NSHumanReadableCopyright</key><string>ZenHR Internal Tool</string>
</dict>
</plist>
PLIST

    chmod +x "$CONTENTS/MacOS/ZenIT"

    # Ad-hoc sign so Gatekeeper allows local runs; replace "-" with a Developer ID
    # identity for distribution outside MDM-trusted deployment.
    codesign --force --deep --sign - "$APP_BUNDLE"

    ZIP_PATH="$PUBLISH_ROOT/ZenIT-Mac-$RID.zip"
    rm -f "$ZIP_PATH"
    (cd "$OUT_DIR" && ditto -c -k --keepParent "ZenIT.app" "$ZIP_PATH")

    echo "==> Created $APP_BUNDLE"
    echo "==> Created $ZIP_PATH"
done

echo "Done."
