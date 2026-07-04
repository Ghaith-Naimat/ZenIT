# ZenIT Pilot Test Checklist

## Pilot Group

- Recommended pilot size: 3-5 Windows users.
- Recommended duration: 1 week.
- Include a mix of laptop models if possible.
- Include users who regularly use Zoom, Slack, Chrome, Google Drive for Desktop, printers, and VPN.

## Deployment Tests

- Publish package is available to JumpCloud.
- Deploy command succeeds:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\JumpCloud-Deploy-ZenIT.ps1 -SourceExe "C:\Temp\ZenIT.exe"
```

- Validation command succeeds:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\Test-ZenITInstall.ps1
```

- Uninstall command succeeds during rollback testing:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\ZenIT\scripts\windows\JumpCloud-Uninstall-ZenIT.ps1
```

## Core App Tests

- App opens from the desktop shortcut.
- App opens from the Start Menu shortcut.
- Launching ZenIT twice keeps only one full app window open.
- Sidebar navigation works.
- Test Mode badge appears only when `EnableTestMode=true`.
- About page shows QA diagnostics only in Test Mode.
- Home shows device status, recent support package status, top workflows, and quick support.
- Contact IT opens the fixed Slack support link and logs the click.
- Quick Fixes search filters workflows live while typing.
- Quick Fixes category filters work.
- Device Health page shows grouped status chips.
- Device Health page shows a 0-100 health score.
- Logs page search, filter, timeline, and export work.
- Logs page newest/oldest sorting works and duration is visible.
- IT Mode unlocks only with configured password and remains unlocked until app closes.
- IT Mode does not show an in-app change password tool.
- IT Mode credentials are enforced by deployment/config scripts.

## Workflow Tests

- Internet Not Working completes with a friendly message.
- DNS Issue refreshes DNS safely.
- Improve Device Performance completes without touching Documents, Downloads, or Recycle Bin.
- Camera or Microphone Not Working gives guidance without changing devices.
- Sound Not Working does not restart services.
- Printer Not Working does not change drivers or printers.
- Slack Not Working asks user to close Slack when Slack is open.
- Zoom Not Working asks user to close Zoom when Zoom is open.
- Google Drive Not Syncing gives clear guidance.
- Security Check is read-only.
- Device Health Check refreshes health values.
- Create Support Package creates TXT, JSON, and HTML files.
- Contact IT opens IT Support in Slack.
- Workflow implementation matrix has been reviewed: `C:\ZenIT\docs\Workflow-Implementation-Matrix.md`.
- Button function audit has been reviewed: `C:\ZenIT\docs\Button-Function-Audit.md`.

## Privacy Review

Confirm reports and feedback packages do not contain:

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

## Success Criteria

- No crashes.
- No admin prompts during normal use.
- Deploy, validate, and uninstall scripts return expected exit codes.
- Reports and feedback files are created correctly.
- Logs are created correctly.
- UI is easy to understand.
- Workflow messages stay short and friendly.
- Actions complete in a reasonable time.
- No sensitive data appears in logs or reports.

## Rollback Steps

1. Remove the JumpCloud deployment assignment.
2. Run `JumpCloud-Uninstall-ZenIT.ps1`.
3. Keep `C:\ProgramData\ZenIT` for IT review.
4. Use `JumpCloud-Uninstall-ZenIT.ps1 -RemoveData` only after IT approval.

## Known Pilot Limitations

- No admin service yet.
- No driver repair yet.
- No SFC, DISM, or CHKDSK repair yet.
- No automatic restart yet.
- No automatic email sending yet.
- IT Mode diagnostics are read-only.
