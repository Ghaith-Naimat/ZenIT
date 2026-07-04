# ZenIT for Mac - Deployment Guide

## Requirements

- macOS 11 (Big Sur) or later; Apple Silicon (`osx-arm64`) or Intel (`osx-x64`).
- Build machine: .NET 8 SDK. End-user devices need nothing extra - the publish output is self-contained.

## Build the deployable

```bash
cd macos
./scripts/publish-zenit-mac.sh all
```

Outputs per architecture:

- `publish/<rid>/ZenIT.app` - self-contained app bundle (ad-hoc signed).
- `publish/ZenIT-Mac-<rid>.zip` - zip of the bundle for distribution.

## Local install

```bash
./scripts/install-zenit-mac.sh
```

This copies `ZenIT.app` into `/Applications` (or `~/Applications` when not writable) and
provisions `~/Library/Application Support/ZenIT/Policy/itpolicy.json`. Without the policy
file, the app runs with IT Mode disabled - identical behavior to the Windows build.

## JumpCloud / MDM deployment

1. Upload `ZenIT-Mac-<rid>.zip` to your software distribution source.
2. Deployment command (per user session):

```bash
unzip -o ZenIT-Mac-osx-arm64.zip -d /Applications
```

3. Deliver `itpolicy.json` to `~/Library/Application Support/ZenIT/Policy/` for each managed
   user (or run `install-zenit-mac.sh` as the login user).
4. For distribution outside MDM-trusted channels, replace the ad-hoc `codesign --sign -` in
   `publish-zenit-mac.sh` with a Developer ID Application identity and notarize the bundle,
   otherwise Gatekeeper will quarantine downloads.

## Data locations (per user)

| Purpose | Path |
| --- | --- |
| Config | `~/Library/Application Support/ZenIT/Config/appsettings.json` |
| IT policy | `~/Library/Application Support/ZenIT/Policy/itpolicy.json` |
| Logs | `~/Library/Application Support/ZenIT/Logs/` |
| Reports | `~/Library/Application Support/ZenIT/Reports/` |
| Fallback log | `~/Library/Logs/ZenIT/ZenIT.log` |

## Elevation model

The app always runs non-elevated for employees. IT workflows flagged "requires admin"
(DHCP renew, Wi-Fi restart, disk verify, software update install, Core Audio / printing /
mDNSResponder restarts, Spotlight reindex) stop safely with "requires administrator
permissions" unless the process runs as root (e.g. launched by IT through approved elevated
tooling). This mirrors the Windows behavior where admin repairs stop when not elevated.

## Uninstall

```bash
./scripts/uninstall-zenit-mac.sh          # keeps user data
./scripts/uninstall-zenit-mac.sh --purge  # removes user data too
```

## Verification checklist

- `dotnet test ZenIT.Mac.sln` - 54/54 tests green.
- Launch the app: Home, Quick Fixes, My Device, IT Mode, and About pages render; EN/AR
  language toggle flips layout to RTL.
- Run "Check My Device" - a `DeviceReport-*.txt` appears in the Reports folder.
- IT Mode unlock uses the standard configured username and password; failed attempts are
  logged to `ITMode.log`.
