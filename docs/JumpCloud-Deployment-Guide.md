# ZenIT JumpCloud Deployment Guide

## Pilot Scope

- Recommended pilot size: 3-5 users.
- Recommended pilot duration: 1 week.
- Target devices: Windows 10/11 laptops used by ZenHR employees.
- Deployment method: JumpCloud command running as Administrator or SYSTEM.

## Prerequisites

- Published `ZenIT.exe` is available at `C:\ZenIT\publish\win-x64\ZenIT.exe`, `C:\ZenIT\publish\win-x64-framework-dependent\ZenIT.exe`, or a reachable custom package path.
- Inno Setup 6 is installed on the packaging machine when building the JumpCloud Custom Apps installer.
- JumpCloud agent is healthy on pilot devices.
- Pilot users are informed that ZenIT creates local logs and support packages under `C:\ProgramData\ZenIT`.

## JumpCloud Custom Apps Installer

JumpCloud Custom Apps expects a real installer package, not the raw `ZenIT.exe` application executable. ZenIT uses Inno Setup to produce a silent-installable Windows installer.

Build the app and JumpCloud source EXE:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\Publish-ZenIT.ps1 -Mode SelfContained
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\Package-ZenIT-JumpCloud.ps1 -Mode SelfContained
```

Build the installer:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\Build-ZenITInstaller.ps1
```

Output:

```text
C:\ZenIT\publish\installer\ZenIT-Setup.exe
```

JumpCloud Custom Apps may reject unsigned EXE installers during package validation. For deployment, sign the installer with the ZenHR code-signing certificate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\Build-ZenITInstaller.ps1 -Sign -CertificateThumbprint "<code-signing-certificate-thumbprint>"
```

The build script signs `ZenIT-Setup.exe` with `signtool.exe` and verifies:

```powershell
Get-AuthenticodeSignature C:\ZenIT\publish\installer\ZenIT-Setup.exe
```

The signature status must be `Valid` before uploading to JumpCloud Custom Apps.

Validate the installer package:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\Test-ZenITInstaller.ps1
```

Optional elevated silent-install validation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\Test-ZenITInstaller.ps1 -InstallSilently
```

### Custom App Settings

- Application Name: `ZenIT`
- Upload: `C:\ZenIT\publish\installer\ZenIT-Setup.exe`
- Silent Install Flags: `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`
- Detection Method: `Display Name`
- Expected Display Name: `ZenIT`
- Deployment plan: start with a pilot device group before assigning to all devices.
- Signing requirement: upload a signed installer. Unsigned EXE installers may fail JumpCloud package validation.

The installer:

- Installs `C:\Program Files\ZenIT\ZenIT.exe`.
- Creates `C:\ProgramData\ZenIT\Config`, `Policy`, `Logs`, and `Reports`.
- Creates or updates production `appsettings.json`.
- Creates or updates protected `itpolicy.json` with the standard IT Mode username and SHA256 password hash.
- Grants Users Modify permission on Config, Logs, and Reports.
- Grants Users read-only access to Policy.
- Preserves existing logs and reports during upgrades.
- Creates Public Desktop and Start Menu shortcuts.
- Registers ZenIT in Windows Apps & Features.
- Supports `/SILENT`, `/VERYSILENT`, `/SUPPRESSMSGBOXES`, and `/NORESTART`.

## JumpCloud Package Size Strategy

JumpCloud command file uploads are limited to 150 MB. ZenIT supports two deployment package modes:

### Option A - Self-contained compressed EXE

Use this option when the published EXE is under the JumpCloud file limit. It includes the .NET runtime inside `ZenIT.exe`, so target devices do not need a separate .NET Desktop Runtime install.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\Publish-ZenIT.ps1 -Mode SelfContained
```

Output:

```text
C:\ZenIT\publish\win-x64\ZenIT.exe
```

### Option B - Framework-dependent EXE

Use this option if the self-contained EXE is blocked by the JumpCloud upload limit. Target devices must have the .NET 8 Desktop Runtime x64 installed.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\Publish-ZenIT.ps1 -Mode FrameworkDependent
```

Output:

```text
C:\ZenIT\publish\win-x64-framework-dependent\ZenIT.exe
```

Runtime prerequisite helper:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\Install-DotNetDesktopRuntime8.ps1
```

Optional package size check:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\Package-ZenIT-JumpCloud.ps1 -Mode SelfContained -Zip
```

## Deployment Command

Final publish command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\Publish-ZenIT.ps1 -Mode SelfContained
```

JumpCloud deployment using the default source path:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\JumpCloud-Deploy-ZenIT.ps1
```

Example with a custom source path:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\JumpCloud-Deploy-ZenIT.ps1 -SourceExe "C:\Temp\ZenIT.exe"
```

The script:

- Copies ZenIT to `C:\Program Files\ZenIT\ZenIT.exe`.
- Creates `C:\ProgramData\ZenIT\Config`, `Policy`, `Logs`, and `Reports`.
- Grants Users Modify permission only on Config, Logs, and Reports.
- Grants Users read-only access to Policy.
- Creates Public Desktop shortcut: `C:\Users\Public\Desktop\ZenIT.lnk`.
- Creates Start Menu shortcut: `C:\ProgramData\Microsoft\Windows\Start Menu\Programs\ZenIT.lnk`.
- Sets both shortcut icons to `C:\Program Files\ZenIT\ZenIT.exe,0`.
- Requests a Windows shell icon refresh after shortcut creation.
- Creates or updates appsettings with final deployment defaults and protected IT policy with standard IT Mode credentials.
- Sets `UpdateChannel` to `Production`.
- Sets `Theme` to `Dark`.
- Writes install details to `C:\ProgramData\ZenIT\Logs\Install.log`.
- ZenIT writes `ZenIT.log` plus typed logs: `Workflow.log`, `System.log`, `ITMode.log`, and `Errors.log`.
- Is idempotent and can be rerun.

## Local Install Command

After publishing locally:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\Install-ZenIT.ps1
```

Confirm these paths exist after install:

```text
C:\Program Files\ZenIT\ZenIT.exe
C:\Users\Public\Desktop\ZenIT.lnk
C:\ProgramData\Microsoft\Windows\Start Menu\Programs\ZenIT.lnk
```

If Windows Explorer still displays an older cached icon immediately after install, restart Explorer or sign out and back in. The installed EXE and shortcuts already point to the current ZenIT application icon.
For managed installs where an immediate icon repaint is required, pass `-RefreshExplorer` to restart Explorer after shortcut creation.

## Validation Command

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\Test-ZenITInstall.ps1
```

Validation checks:

- EXE exists.
- Desktop shortcut exists.
- Start Menu shortcut exists.
- ProgramData folders exist.
- Config and protected policy exist.
- Config, Logs, and Reports are writable.
- Policy is readable but not user-writable.
- Version can be read if available.
- IT Mode username equals the standard ZenHR IT username.
- IT Mode password hash equals the standard SHA256 hash.
- In-app IT credential changes are disabled.
- No plaintext password-shaped config field exists.

## IT Mode

- IT Mode username: `Ghaith`
- IT Mode password is provided separately by ZenHR IT.
- The app stores only a SHA256 password hash in `C:\ProgramData\ZenIT\Policy\itpolicy.json`.
- Normal app users cannot change IT Mode credentials; deployment/policy scripts enforce the standard username and hash.

## Code Signing

`Publish-ZenIT.ps1` supports optional signing:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\Publish-ZenIT.ps1 -Sign -CertificateThumbprint "<thumbprint>"
```

Unsigned builds are allowed for local validation, but broad enterprise rollout should use a signed app EXE, signed installer EXE, and signed deployment scripts. JumpCloud Custom Apps may reject unsigned EXE installers.

## Uninstall Command

Keep ProgramData:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\JumpCloud-Uninstall-ZenIT.ps1
```

Remove ProgramData after IT approval:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\JumpCloud-Uninstall-ZenIT.ps1 -RemoveData
```

## Success Criteria

- ZenIT launches for normal users without admin prompts.
- Home, Quick Fixes, Device Health, Logs, About, and IT Mode navigation works.
- Create Support Package generates TXT, JSON, and HTML files.
- Device Health shows the calculated health score.
- Logs page supports search, filters, duration, and newest/oldest sorting.
- Log files rotate automatically at 10 MB and keep 5 rotated files.
- Contact IT opens the fixed Slack support link.
- Logs page shows friendly summaries.
- No crashes during normal workflows.

## Rollback

1. Remove the JumpCloud deployment assignment.
2. Run `JumpCloud-Uninstall-ZenIT.ps1`.
3. Keep ProgramData for IT review unless `-RemoveData` is explicitly approved.
4. Validate removal of app files and shortcuts.

## Privacy Notes

ZenIT does not collect personal files, browser history, cookies, saved passwords, tokens, email contents, chat messages, or Google Drive file names. Support packages are local files prepared for IT review and are not emailed automatically.

## Known Limitations

- No admin service yet.
- No automatic email sending yet.
- No driver repair yet.
- No SFC, DISM, or CHKDSK repair workflow yet.
- IT Mode diagnostics are read-only.

## Workflow Matrix

Use `C:\ZenIT\docs\Workflow-Implementation-Matrix.md` during pilot review to confirm which workflows perform real safe fixes, which are diagnostic-only, and which require a future managed service.
