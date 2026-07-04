# ZenIT Pilot Deployment Guide

## Prerequisites

- Windows 10 or Windows 11.
- .NET 8 SDK on the build machine.
- JumpCloud agent installed on pilot devices.
- A pilot device group in JumpCloud.
- Administrator rights only for install and uninstall scripts.

## Build

From `C:\ZenIT`:

```powershell
dotnet build .\ZenIT.sln
```

## Publish For Windows x64

From `C:\ZenIT`:

```powershell
dotnet publish .\src\ZenIT.App\ZenIT.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\win-x64
```

This creates:

```text
C:\ZenIT\publish\win-x64\ZenIT.exe
```

Or use the validation script:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\windows\Publish-ZenIT.ps1
```

The publish script fails clearly if ZenIT is running.

## Install And Uninstall

Install after publishing:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\windows\Install-ZenIT.ps1
```

Confirm these paths exist:

```text
C:\Program Files\ZenIT\ZenIT.exe
C:\Users\Public\Desktop\ZenIT.lnk
C:\ProgramData\Microsoft\Windows\Start Menu\Programs\ZenIT.lnk
```

The install script asks a running ZenIT window to close and waits up to 10 seconds before copying the new executable.

Uninstall while keeping logs, reports, and config:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\windows\Uninstall-ZenIT.ps1
```

Uninstall and remove ZenIT ProgramData:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\windows\Uninstall-ZenIT.ps1 -RemoveData
```

## JumpCloud Deployment

Deploy with the default published EXE:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\JumpCloud-Deploy-ZenIT.ps1
```

Deploy with a custom source EXE:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\JumpCloud-Deploy-ZenIT.ps1 -SourceExe "C:\Temp\ZenIT.exe"
```

Validate after deployment:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\Test-ZenITInstall.ps1
```

## Config

ZenIT creates this file on first launch:

```text
C:\ProgramData\ZenIT\Config\appsettings.json
```

Default values:

```json
{
  "AppMode": "Pilot",
  "ITSupportEmail": "it@zenhr.com",
  "CompanyName": "ZenHR",
  "UpdateChannel": "Production",
  "Theme": "Dark",
  "EnableExperimentalActions": false,
  "EnableTestMode": false,
  "EnableITMode": true,
  "AllowITCredentialChanges": false,
  "LogRetentionDays": 30,
  "ReportRetentionDays": 14,
  "ITModeUsername": "Ghaith",
  "ITModePasswordHash": "<SHA256 hash>"
}
```

Do not store secrets, plaintext passwords, tokens, or credentials in this file. IT Mode stores only a SHA256 password hash in `ITModePasswordHash`.

IT Mode username: `Ghaith`. The IT Mode password is provided separately by ZenHR IT. Normal app users cannot change IT Mode credentials; deployment/config scripts enforce the standard username and password hash.

## ProgramData Permissions

During install, `Install-ZenIT.ps1` grants standard Windows users Modify permission on these ZenIT data folders only:

```text
C:\ProgramData\ZenIT\Config
C:\ProgramData\ZenIT\Logs
C:\ProgramData\ZenIT\Reports
```

The installer does not modify `C:\Program Files\ZenIT` permissions.

ZenIT writes `ZenIT.log` plus typed logs in the logs folder: `Workflow.log`, `System.log`, `ITMode.log`, and `Errors.log`. Logs rotate automatically at 10 MB and keep 5 rotated files.

## Logs

Primary log:

```text
C:\ProgramData\ZenIT\Logs\ZenIT.log
```

Crash log:

```text
C:\ProgramData\ZenIT\Logs\ZenIT-crash.log
```

If ProgramData logging is denied, ZenIT falls back to:

```text
%LocalAppData%\ZenIT\Logs\ZenIT.log
%LocalAppData%\ZenIT\Logs\ZenIT-crash.log
```

The Logs page shows friendly summaries only. Technical details stay in local logs for IT troubleshooting.

## Reports

Reports and help request packages are stored at:

```text
C:\ProgramData\ZenIT\Reports
```

Report naming:

```text
DeviceReport-{DeviceName}-{Username}-{yyyyMMdd-HHmmss}.txt
HelpRequest-{DeviceName}-{Username}-{yyyyMMdd-HHmmss}.txt
SupportPackage-{DeviceName}-{Username}-{yyyyMMdd-HHmmss}.txt
SupportPackage-{DeviceName}-{Username}-{yyyyMMdd-HHmmss}.json
SupportPackage-{DeviceName}-{Username}-{yyyyMMdd-HHmmss}.html
```

## Workflow List

- Internet Not Working: checks network state, connectivity, DNS, proxy state, VPN hints, then runs allowlisted `ipconfig /flushdns`, `/release`, and `/renew`.
- DNS Issue: checks DNS servers and resolution, then refreshes DNS safely.
- Improve Device Performance: checks memory, disk, uptime, pending reboot, and clears current-user temp folders only.
- Camera or Microphone Not Working: checks privacy settings and meeting app state without changing devices or services.
- Sound Not Working: checks audio process/service hints without restarting services.
- Printer Not Working: checks print spooler process and printer readiness hints without changing drivers or printers.
- Slack Not Working: asks the user to close Slack first, then clears safe current-user Slack cache folders only.
- Zoom Not Working: asks the user to close Zoom first, then clears safe current-user Zoom cache folders only.
- Google Drive Not Syncing: checks Drive process, internet, and disk space, then gives guided next steps.
- Security Check: checks Kaspersky, Firewall, BitLocker, Defender, and JumpCloud/MDM signals read-only where available.
- Device Health Check: collects basic non-sensitive health values and creates a small summary.
- Create Support Package: creates TXT, JSON, and HTML troubleshooting reports for IT.
- Contact IT: opens the fixed IT Support Slack link and logs the click.

See `docs\Workflow-Implementation-Matrix.md` for the current implementation status, real checks, real fixes, and future managed-service items for each workflow.
See `docs\Button-Function-Audit.md` for a button-by-button execution and safety audit.

ZenIT also includes a workflow execution queue abstraction. The current queue executes immediately, while keeping the architecture ready for JumpCloud, remote actions, automation, and future assistant-driven execution.

## Privacy Boundaries

ZenIT does not collect:

- Personal files.
- Documents or Downloads.
- Browser history.
- Cookies.
- Saved passwords.
- Tokens.
- Email contents.
- Chat messages.
- Google Drive file names.
- Full installed software inventory.

ZenIT does not store admin credentials, elevate permissions, or execute arbitrary commands from the UI. Only registered workflows can run.

## Known Limitations

- No admin service yet.
- No driver repair yet.
- No SFC, DISM, or CHKDSK workflow yet.
- No automatic restart yet.
- No automatic email sending yet.
- No automatic printer driver reinstall.
- No automatic camera, microphone, or default audio device changes.

## Rollback Or Uninstall

1. Remove the JumpCloud deployment assignment from the pilot device group.
2. Run `Uninstall-ZenIT.ps1` as administrator.
3. Keep `C:\ProgramData\ZenIT` until IT confirms logs and reports are no longer needed.
4. Use `Uninstall-ZenIT.ps1 -RemoveData` only after IT approval.
