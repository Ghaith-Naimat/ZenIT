# ZenIT macOS Deployment Guide

ZenIT for macOS is deployed through a signed or unsigned `.pkg` installer. Do not use DMG as the final JumpCloud deployment format.

## Requirements

- macOS 11 Big Sur or later.
- Build machine: macOS with .NET 8 SDK, `pkgbuild`, `productbuild`, and Xcode command line tools.
- Optional signing: Apple Developer ID Installer certificate.
- Optional notarization: Apple notarytool credentials or keychain profile.

## Build the App Bundle

The package build script uses `macos/ZenIT.app` as the preferred input. If it is missing, the script publishes the app from `macos/ZenIT.Mac.sln` for the selected runtime identifier.

```bash
./scripts/macos/build-pkg.sh --rid osx-arm64
```

You can also pass an explicit bundle:

```bash
./scripts/macos/build-pkg.sh --app macos/publish/osx-arm64/ZenIT.app
```

## Package Output

Final package:

```text
publish/macos/ZenIT-macOS.pkg
```

The installer places the application at:

```text
/Applications/ZenIT.app
```

## System Locations

The installer creates:

```text
/Library/Application Support/ZenIT/Config
/Library/Application Support/ZenIT/Policy
/Library/Logs/ZenIT
/Users/Shared/ZenIT/Reports
```

It creates `appsettings.json` with:

- `Language = en`
- `Theme = Dark`
- `UpdateChannel = Production`

It creates protected `itpolicy.json` with standard ZenHR IT Mode policy:

- `EnableITMode = true`
- `ITModeUsername = Ghaith`
- `ITModePasswordHash = 95FAB1FCF914BB5E3D56891BD2B1D03B40DD6066D3ED1327798A9673BB0A30FC`
- `AllowITCredentialChanges = false`
- `ContactITUrl = https://zenhr.slack.com/team/U09CGMUGV6K`
- `ITModeSessionTimeoutMinutes = 15`

The plaintext IT Mode password is not stored in the package, scripts, policy, or logs.

## Permissions

- Policy: root/admin write, everyone read-only.
- Config: local users can update non-sensitive preferences such as language and theme.
- Logs: local users can write support logs.
- Reports: local users can write support packages.

No LaunchAgent or background helper is installed. ZenIT only launches when opened by the user or management tooling.

## Signing

Unsigned packages can be built for internal testing:

```bash
./scripts/macos/build-pkg.sh
```

The script prints:

```text
Unsigned/not notarized package may trigger Gatekeeper warnings.
```

For deployment, sign with a Developer ID Installer certificate:

```bash
./scripts/macos/build-pkg.sh --sign "Developer ID Installer: ZenHR (TEAMID)"
```

## Notarization

Using a notarytool keychain profile:

```bash
./scripts/macos/build-pkg.sh \
  --sign "Developer ID Installer: ZenHR (TEAMID)" \
  --notarize \
  --keychain-profile "ZenHRNotary"
```

Using Apple ID credentials:

```bash
./scripts/macos/build-pkg.sh \
  --sign "Developer ID Installer: ZenHR (TEAMID)" \
  --notarize \
  --apple-id "it@zenhr.com" \
  --team-id "TEAMID" \
  --password "@keychain:AC_PASSWORD"
```

The build script submits with `xcrun notarytool submit --wait` and staples with `xcrun stapler staple`.

## Validation

Package-only validation:

```bash
./scripts/macos/test-zenit-install.sh
```

Elevated silent install and launch validation:

```bash
sudo ./scripts/macos/test-zenit-install.sh --install --launch
```

Checks include:

- Package exists and expands.
- `/Applications/ZenIT.app` exists after install.
- `appsettings.json` exists.
- `itpolicy.json` exists.
- Username is `Ghaith`.
- Timeout is `15`.
- Logs and Reports folders are writable.
- App launches when requested.

## JumpCloud Deployment

Upload the package to a GitHub Release as:

```text
ZenIT-macOS.pkg
```

JumpCloud command:

```bash
curl -L -o /tmp/ZenIT-macOS.pkg "https://github.com/Ghaith-Naimat/ZenIT/releases/latest/download/ZenIT-macOS.pkg"
sudo installer -pkg /tmp/ZenIT-macOS.pkg -target /
```

Start with a pilot device group before assigning the command to all managed Macs.

## Uninstall

Remove the app while preserving logs and reports:

```bash
sudo rm -rf /Applications/ZenIT.app
```

Remove all machine-wide ZenIT data only after IT approval:

```bash
sudo rm -rf "/Library/Application Support/ZenIT" /Library/Logs/ZenIT /Users/Shared/ZenIT
```
