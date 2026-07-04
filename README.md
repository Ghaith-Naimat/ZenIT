# ZenIT

ZenIT is ZenHR's Windows self-service IT assistant for employees and IT support.

Version: 1.0.0

## Solution Layout

- `src/ZenIT.App` - WPF desktop app, MVVM shell, navigation, localization, IT Mode UI.
- `src/ZenIT.Core` - workflow registry/execution, device health, logging, reports, config, security helpers.
- `src/ZenIT.Service` - reserved for a future managed Windows service.
- `assets/logo` - source logo assets used for app branding.
- `scripts/windows` - publish, install, uninstall, JumpCloud deployment, and validation scripts.
- `docs` - deployment, QA, security, workflow, and architecture documentation.

## Build

```powershell
dotnet build .\ZenIT.sln
dotnet build .\ZenIT.sln -c Release
.\scripts\windows\Publish-ZenIT.ps1
```

The published executable is created at `publish/win-x64/ZenIT.exe`.

## macOS Version

A native macOS version (Avalonia UI) lives in `macos/` with its own solution, scripts, and
docs. It shares no build files with the Windows app, so nothing here changes for Windows.
See `macos/README.md` and `macos/docs/Mac-Deployment-Guide.md`.

## Security Boundaries

Employee workflows are non-elevated and registered in `WorkflowRegistry`. ZenIT does not store plaintext passwords and does not collect personal files, browser history, cookies, tokens, documents, downloads, emails, chat content, or Google Drive file names.

IT Mode requires the standard configured username and SHA256 password hash, requires confirmation for repair workflows, and stops safely when administrator permissions are required but unavailable.
