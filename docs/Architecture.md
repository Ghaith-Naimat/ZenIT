# ZenIT Architecture

ZenIT 1.0.0 is a WPF/.NET 8 desktop application with a strict split between employee-safe self-service workflows and IT-only diagnostics/repairs.

## Projects

- `ZenIT.App`: WPF shell, MVVM view models, app startup, single-instance behavior, page navigation, toasts, localization switcher, and IT Mode screens.
- `ZenIT.Core`: workflow definitions, execution contracts, local workflow executor, device health service, config service, logging, report exporters, password hashing, safe process runner, and retention cleanup.
- `ZenIT.Service`: placeholder for a future elevated managed service. No service is installed in v1.0.0.

## Workflow Architecture

`WorkflowRegistry` is the source of truth for visible employee workflows and IT workflows. `WorkflowIntegrityValidator` runs at startup and logs duplicate IDs, missing registrations, category drift, invalid risk/admin combinations, and tier mistakes.

Execution flows through:

`WorkflowDefinition -> WorkflowExecutionRequest -> IWorkflowExecutionQueue -> IWorkflowExecutor -> WorkflowExecutionResult`

The current queue is immediate. This preserves a future path for JumpCloud actions, remote automation, or a managed service without changing the UI contract.

## Logging Architecture

`LogService` writes the legacy combined log `ZenIT.log` and separate typed logs:

- `Workflow.log`
- `System.log`
- `ITMode.log`
- `Errors.log`

Logs are fail-safe, fall back to `%LocalAppData%\ZenIT\Logs`, cache parsed summaries, and rotate at 10 MB with 5 retained files.

## Report Architecture

Support packages use `ReportDocument` plus reusable exporters:

- TXT exporter
- JSON exporter
- HTML exporter

Every report includes a header, timestamp, ZenIT version, device, user, summary, details, and privacy footer.

## Configuration Architecture

`AppSettingsService` creates and normalizes `C:\ProgramData\ZenIT\Config\appsettings.json`. `AppSettingsValidator` logs startup config issues. Settings are logically owned by these areas:

- General: app mode, company, version behavior
- Localization: language
- Theme: current/future theme selection
- Logging: retention
- Reports: retention
- ITMode: username, password hash, credential-change policy
- Support: IT support email and Contact IT URL behavior
- Update: update channel

## Localization Architecture

`LocalizedStrings` provides key-based English/Arabic resources for navigation, Home, Quick Fixes, My Device, IT Mode, Logs, Reports, About, confirmation modals, workflow titles/descriptions, buttons, and status messages. Language is saved immediately to config and changes WPF `FlowDirection` for Arabic without requiring an app restart.

The header uses a compact `EN | ع` language switcher with localized tooltip text. Arabic mode uses right-to-left flow and Arabic employee-facing wording while preserving product names such as ZenIT, ZenHR, Slack, Zoom, Google Drive, Chrome, Kaspersky, and JumpCloud.

Known limitation for v1.0.0: generated diagnostic report body content may remain English, while the surrounding app UI is bilingual.

## Theme Architecture

`ThemeManager` currently normalizes the `Dark` theme while reserving `Light` and `HighContrast` for future resource dictionaries. ZenHR colors remain unchanged in v1.0.0.

## Deployment Architecture

Publishing creates a self-contained Windows x64 single-file EXE. Install and JumpCloud scripts copy it to `C:\Program Files\ZenIT`, create shortcuts, create ProgramData folders, enforce IT Mode config, and grant Users Modify only to Config, Logs, and Reports.

## Roadmap

- Managed elevated service for admin repairs.
- Full resource-dictionary theme switching.
- Complete Arabic localization.
- Central telemetry or SIEM forwarding if approved by IT/security.
- Signed installer/MSIX or Intune/JumpCloud package metadata.
