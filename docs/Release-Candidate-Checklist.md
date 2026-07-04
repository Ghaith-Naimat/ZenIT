# ZenIT v1.0.0 Release Candidate Checklist

Use this checklist before approving ZenIT for company-wide deployment.

## UI

- [ ] Home is simplified and employee-focused.
- [ ] Quick Fixes grid spacing, card heights, badges, and buttons are consistent.
- [ ] My Device sections do not overlap at 1366x768, 1920x1080, and 2560x1440.
- [ ] IT Mode dashboard, logs, reports, and advanced repairs remain visually aligned.
- [ ] About page is readable and professional.
- [ ] Card hover, button press, focus states, shadows, borders, and corner radius feel consistent.
- [ ] Toast notifications do not block content.

## Localization

- [ ] English and Arabic language switching works without restart.
- [ ] Arabic uses RTL flow and professional employee-facing wording.
- [ ] No visible mixed-language UI except approved product names: ZenIT, ZenHR, Slack, Zoom, Chrome, Google Drive, Kaspersky, JumpCloud, Windows.
- [ ] IT Mode unlock, confirmation modal, Logs, Reports, My Device, About, workflow cards, buttons, badges, empty states, and status messages are localized.
- [ ] `.\scripts\windows\Test-ZenITLocalization.ps1` passes.

## Accessibility

- [ ] Keyboard tab order reaches navigation, language switcher, workflow buttons, IT Mode inputs, and modal actions.
- [ ] Focus indicators are visible.
- [ ] Buttons meet minimum touch/click size.
- [ ] Text remains readable at 125%, 150%, and 200% scaling.
- [ ] Color contrast is acceptable for normal and highlighted states.
- [ ] Logo and important controls have screen-reader-friendly labels where practical.

## Workflow Validation

- [ ] `WorkflowIntegrityValidator` reports no duplicate or missing workflow registrations.
- [ ] Employee workflows do not require elevation.
- [ ] IT workflows require IT Mode where appropriate.
- [ ] Confirmation-required workflows show the confirmation modal.
- [ ] Diagnostic-only workflows do not claim a repair was completed.
- [ ] Workflow duration, result, technical message, and report path are logged.

## Reports

- [ ] Support package creates TXT, JSON, and HTML files.
- [ ] Reports include header, timestamp, ZenIT version, device, user, summary, details, and footer.
- [ ] HTML report uses clean ZenHR styling and no external assets.
- [ ] Reports exclude personal files, browser history, cookies, passwords, tokens, emails, chats, and Google Drive file names.

## Logging

- [ ] `ZenIT.log` remains available.
- [ ] `Workflow.log`, `System.log`, `ITMode.log`, and `Errors.log` are written as appropriate.
- [ ] Log rotation is enabled at 10 MB with 5 retained files.
- [ ] Logs page sorts, filters, and searches smoothly.
- [ ] Log parsing failures are handled gracefully.

## Security

- [ ] No plaintext IT password exists in config, protected policy, source, logs, reports, or docs.
- [ ] IT Mode stores only SHA256 hash in `C:\ProgramData\ZenIT\Policy\itpolicy.json`.
- [ ] Command execution is allowlisted.
- [ ] Employee Mode does not run Winsock reset, TCP/IP reset, SFC, DISM, CHKDSK, service restarts, or elevation.
- [ ] Contact IT opens only the fixed Slack support URL.
- [ ] ProgramData Modify permissions are limited to Config, Logs, and Reports; Policy is Users read-only.

## Deployment

- [ ] `dotnet build .\ZenIT.sln` passes with 0 warnings and 0 errors.
- [ ] `dotnet build .\ZenIT.sln -c Release` passes with 0 warnings and 0 errors.
- [ ] `.\scripts\windows\Publish-ZenIT.ps1` passes.
- [ ] Published EXE exists at `C:\ZenIT\publish\win-x64\ZenIT.exe`.
- [ ] Install script copies ZenIT to `C:\Program Files\ZenIT\ZenIT.exe` when run as Administrator.
- [ ] Desktop and Start Menu shortcuts exist.
- [ ] `.\scripts\windows\Test-ZenITInstall.ps1` passes.

## JumpCloud Readiness

- [ ] JumpCloud deployment script is idempotent.
- [ ] JumpCloud uninstall script keeps ProgramData by default.
- [ ] Validation script reports clear pass/fail output.
- [ ] IT Mode standard username/hash are enforced by deployment scripts in protected policy.
- [ ] `dotnet test` passes.

## Performance

- [ ] Startup is responsive.
- [ ] Navigation does not flash.
- [ ] Log summaries are cached between unchanged reads.
- [ ] Workflows run asynchronously and do not freeze the UI.
- [ ] Report generation completes within a reasonable time.
- [ ] Language switching does not require restart.

## Version And Documentation

- [ ] App, file, assembly, and product version are `1.0.0`.
- [ ] About page shows version/build/runtime/update channel.
- [ ] `README.md`, `Architecture.md`, `Security-Audit.md`, `Workflow-Implementation-Matrix.md`, `Button-Function-Audit.md`, and deployment guides are current.

## Release Decision

- [ ] Approved for release candidate deployment.
- [ ] Approved for company-wide JumpCloud rollout.
