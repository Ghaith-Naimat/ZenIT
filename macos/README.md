# ZenIT for Mac

ZenIT for Mac is the macOS version of ZenHR's self-service IT assistant. It lives entirely
inside this `macos/` folder with its own solution, so the Windows app (`../ZenIT.sln`,
`../src`, `../scripts/windows`) is completely unaffected.

Version: 1.0.0

## Solution Layout

- `ZenIT.Mac.sln` - macOS-only solution.
- `src/ZenIT.Mac.App` - Avalonia desktop app (same navigation, IT Mode, localization, and branding as the Windows WPF app).
- `src/ZenIT.Mac.Core` - macOS port of ZenIT.Core: workflow registry/execution, device health, logging, reports, config, security helpers. Namespaces stay `ZenIT.Core.*` so files diff cleanly against the Windows core.
- `src/ZenIT.Mac.Tests` - xUnit suite ported from the Windows tests (54 tests).
- `scripts/` - publish (.app bundle + zip), install, and uninstall scripts.
- `docs/` - macOS deployment guide.

## Build & Test

```bash
dotnet build ZenIT.Mac.sln
dotnet test ZenIT.Mac.sln
dotnet run --project src/ZenIT.Mac.App/ZenIT.Mac.App.csproj
```

## Publish

```bash
./scripts/publish-zenit-mac.sh osx-arm64   # Apple Silicon (or osx-x64 / all)
./scripts/install-zenit-mac.sh             # copies ZenIT.app to /Applications and provisions the IT policy
```

The published bundle is created at `publish/<rid>/ZenIT.app` plus a deployable
`publish/ZenIT-Mac-<rid>.zip`.

## What changed vs. Windows

| Area | Windows | macOS |
| --- | --- | --- |
| UI framework | WPF | Avalonia 11 |
| Data root | `C:\ProgramData\ZenIT` | `~/Library/Application Support/ZenIT` |
| Open folder | `explorer.exe` | `open` (allowlisted to Logs/Reports) |
| Flush DNS | `ipconfig /flushdns` | `dscacheutil -flushcache` (+ `killall -HUP mDNSResponder` when elevated) |
| System repair | SFC / DISM | `diskutil verifyVolume /`, SIP/Gatekeeper/FileVault checks, Spotlight reindex |
| Service restarts | Print Spooler, BITS, Windows Update, audio services | CUPS, mDNSResponder, Core Audio |
| Updates | winget upgrade | `softwareupdate -l` / `softwareupdate -i -a` |
| Security check | Kaspersky, Windows Firewall, BitLocker, JumpCloud | Application Firewall, FileVault, Gatekeeper, SIP, JumpCloud (no Kaspersky on ZenHR Macs) |
| App refresh targets | chrome.exe, slack.exe, Zoom.exe, GoogleDriveFS.exe | Google Chrome.app, Slack.app, zoom.us.app, Google Drive.app |
| Graceful app close | `CloseMainWindow()` | SIGTERM, then SIGKILL fallback |
| Elevation check | WindowsPrincipal administrator role | `Environment.IsPrivilegedProcess` (root) |

Employee workflows (Fix Everything, Fix Internet, Speed Up Device, Fix Chrome/Slack/Zoom/Drive,
Camera & Mic, Sound, Security Check, Check My Device, Support Package, Contact IT) keep the
same IDs, names, and safety model. The Windows-only Actions subsystem (unused by the UI) was
not ported.

## Security Boundaries

Same model as Windows: employee workflows are non-elevated and cache/diagnostic-safe only;
every IT command is validated against a fixed allowlist; IT Mode requires the configured
username and SHA256 password hash; admin-only repairs stop safely when ZenIT is not elevated;
no plaintext passwords, personal files, browser data, or chat content are ever collected.
