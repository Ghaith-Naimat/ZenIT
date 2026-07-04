# ZenIT Enterprise QA Checklist

Use this checklist before company-wide rollout.

## UI and Responsiveness

- Verify Home, Quick Fixes, My Device, IT Mode, and About at 1366x768, 1920x1080, and 2560x1440.
- Confirm no overlapping cards, clipped text, horizontal overflow, or hidden controls.
- Confirm workflow cards keep fixed status height and buttons stay aligned while running.
- Confirm the app remains responsive during long workflows.
- Confirm toast notifications appear and disappear smoothly.

## Language Switcher

- Confirm default language is English.
- Confirm `C:\ProgramData\ZenIT\Config\appsettings.json` contains `Language: "en"` or `"ar"`.
- Confirm the header shows the compact `EN | ع` language switcher.
- Switch to Arabic and confirm RTL layout.
- Confirm sidebar items, Home title/subtitle, employee workflow titles/descriptions, card status messages, IT Mode, Logs, Reports, My Device, and About are localized.
- Confirm workflow cards do not show "Ready to help" before a workflow runs.
- Switch back to English and confirm layout returns to LTR.
- Run `.\scripts\windows\Test-ZenITLocalization.ps1` and confirm no missing keys.

## Employee Workflows

- Run Fix Internet and confirm adapter/IP/gateway/DNS/ping/proxy/VPN checks are logged.
- Run Speed Up Device and confirm safe cleanup completes without touching personal folders; confirm Recycle Bin is not emptied.
- Run Fix Everything and confirm all safe checks complete without admin prompts.
- Run Fix Chrome with Chrome open, minimized, background-only, and closed; confirm ZenIT closes allowlisted Chrome processes automatically, cleans safe cache only, restarts Chrome only if it was running, and never deletes bookmarks/history/cookies/passwords/profile data.
- Run Fix Slack with Slack open, minimized, tray-only, background-only, and closed; confirm ZenIT handles the full lifecycle automatically, cleans safe cache, restarts Slack only if it was running, and never deletes downloads/workspace files/tokens/user data.
- Run Fix Zoom with Zoom open, minimized, background-only, and closed; confirm ZenIT handles the full lifecycle automatically, cleans safe cache, restarts Zoom only if it was running, and never deletes recordings/meeting files/user documents.
- Run Fix Google Drive with Drive running and stopped; confirm ZenIT restarts allowlisted Drive processes only when Drive was running, does not touch synced files, does not list Drive filenames, and does not delete Drive cache.
- Run Create Support Package and confirm TXT, JSON, and HTML are created.
- Run Contact IT and confirm the fixed Slack URL opens.
- Confirm Fix Printer is not visible in Employee Mode.

## Smarter Internet Repair

- Test with a connected network.
- Test with DNS failure if possible.
- Test with Wi-Fi/Ethernet disabled if possible.
- Confirm Employee Mode never runs winsock reset, TCP/IP reset, driver changes, registry edits, or adapter restart commands.
- Confirm disabled adapter result tells the user to contact IT and logs `RequiresAdmin`.

## IT Mode

- Confirm IT Mode requires username and password.
- Confirm failed and successful login attempts are logged without password values.
- Confirm IT Mode remains unlocked until app close or Lock IT Mode.
- Confirm IT workflows show risk and require confirmation.
- Confirm admin-only workflows return a friendly admin-required message when not elevated.

## Logs

- Confirm every workflow writes to `C:\ProgramData\ZenIT\Logs\ZenIT.log`.
- Confirm typed logs are created as needed: `Workflow.log`, `System.log`, `ITMode.log`, and `Errors.log`.
- Confirm logs rotate at 10 MB and keep 5 rotated files.
- Confirm fallback logging works if ProgramData logging is denied.
- Confirm IT Mode Logs refresh and show Time, Workflow, Result, Duration.
- Confirm Contact IT success/failure is logged.

## Reports

- Confirm reports are saved under `C:\ProgramData\ZenIT\Reports`.
- Confirm TXT, JSON, and HTML reports include header, timestamp, ZenIT version, device, user, summary, details, and footer.
- Confirm support packages include latest simplified workflow names.
- Confirm reports do not include passwords, browser history, cookies, tokens, personal file names, documents, downloads, emails, chats, or Google Drive file names.

## Security and Privacy

- Confirm no plaintext IT password exists in config, policy, code, logs, or reports.
- Confirm `C:\ProgramData\ZenIT\Policy\itpolicy.json` exists and Users do not have Modify permission.
- Confirm Employee Mode has no elevation prompts.
- Confirm only allowlisted commands are executed.
- Confirm IT Mode repair commands are registered and confirmed.

## Deployment

- Run `dotnet build .\ZenIT.sln`.
- Run `dotnet build .\ZenIT.sln -c Release`.
- Run `.\scripts\windows\Publish-ZenIT.ps1`.
- Run `.\scripts\windows\Test-ZenITInstall.ps1`.
- Confirm published EXE exists at `C:\ZenIT\publish\win-x64\ZenIT.exe`.
- Confirm installed shortcuts exist on Public Desktop and Start Menu.

## Logo Assets

- Place production logo assets in `C:\ZenIT\assets\logo`.
- Confirm logo files contain no secrets or private metadata before packaging.
- Confirm the app window icon and left sidebar logo use the packaged ZenIT logo asset.
