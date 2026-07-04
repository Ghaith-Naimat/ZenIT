# ZenIT Architecture Deep Dive

Date: 2026-07-03
Version reviewed: 1.0.0

## Architectural Intent

ZenIT is designed as a local-first Windows support assistant:

- employee-safe workflows run without elevation
- IT workflows are gated behind IT Mode
- workflows are centrally registered
- reports and logs stay local
- deployment works through JumpCloud or local admin install
- future elevated repairs are expected to move into `ZenIT.Service`

## Solution Structure

```text
src\ZenIT.App
  App.xaml / App.xaml.cs
  MainWindow.xaml / MainWindow.xaml.cs
  Assets
  ViewModels

src\ZenIT.Core
  Actions
  Configuration
  Execution
  Localization
  Logging
  Maintenance
  Models
  Reports
  Security
  Services
  Workflows

src\ZenIT.Service
  README.md
```

## Project Dependencies

```mermaid
flowchart TD
    App[ZenIT.App] --> Core[ZenIT.Core]
    Scripts[scripts/windows] --> AppExe[publish/win-x64/ZenIT.exe]
    Scripts --> ProgramFiles[C:\Program Files\ZenIT]
    Scripts --> ProgramData[C:\ProgramData\ZenIT]
```

`ZenIT.Core` has no dependency on `ZenIT.App`. This is correct and should be preserved.

## App Startup Flow

```mermaid
sequenceDiagram
    participant Windows
    participant App
    participant Validator
    participant MainWindow
    participant VM as MainViewModel
    Windows->>App: Start ZenIT.exe
    App->>App: Acquire per-user Mutex
    alt duplicate instance
        App->>Windows: Bring existing window to front
        App->>App: Exit silently
    else first instance
        App->>Validator: Validate assets/resources/config/workflows
        App->>MainWindow: Create and Show
        MainWindow->>VM: Create DataContext
        VM->>VM: Load settings, health, logs
        VM->>VM: Start maintenance and Health Guardian
    end
```

Startup crash handling writes rich diagnostics to `StartupCrash.log`.

## WPF Shell

`MainWindow.xaml` is a single-shell XAML file that contains:

- global converters and visual styles
- sidebar
- header and language switcher
- Home
- Quick Fixes
- My Device
- IT Mode unlock/dashboard/logs/reports/advanced repairs
- About
- confirmation overlay
- toast display

This gives one consistent design system, but the file is too large for long-term maintainability.

Recommended split:

```text
Views\HomeView.xaml
Views\QuickFixesView.xaml
Views\MyDeviceView.xaml
Views\ItModeView.xaml
Views\AboutView.xaml
Controls\WorkflowCard.xaml
Controls\StatusChip.xaml
Controls\LanguageSwitcher.xaml
```

## ViewModel Composition

`MainViewModel` currently owns:

- dependency creation
- navigation state
- commands
- workflow card collections
- health metrics
- log parsing display
- IT Mode authentication
- diagnostics
- language switching
- toast and confirmation overlay state
- startup maintenance

Recommended split:

```text
ShellViewModel
HomeViewModel
QuickFixesViewModel
MyDeviceViewModel
ItModeViewModel
LogsViewModel
AboutViewModel
LocalizationViewModel or LanguageService
```

## Workflow Architecture

```mermaid
flowchart LR
    Registry[WorkflowRegistry] --> Definition[WorkflowDefinition]
    Definition --> Card[WorkflowCardViewModel]
    Card --> Request[WorkflowExecutionRequest]
    Request --> Queue[ImmediateWorkflowExecutionQueue]
    Queue --> Executor[LocalWorkflowExecutor]
    Executor --> Result[WorkflowExecutionResult]
    Result --> CardStatus[Card status/toast]
    Result --> Log[LogService]
```

Core model:

- `WorkflowId`: stable enum identity.
- `WorkflowDefinition`: display/risk/tier metadata.
- `WorkflowAccessTier`: Employee or IT.
- `WorkflowRiskLevel`: Low, Medium, High.
- `WorkflowOutcome`: Success, RepairAttempted, NeedsIT, CannotVerify.
- `WorkflowStepResult`: per-step technical result.

The queue is currently immediate. The abstraction is useful for future remote execution, JumpCloud orchestration, or service-backed execution.

## Workflow Integrity

`WorkflowIntegrityValidator` checks:

- duplicate IDs
- enum values not registered
- empty titles
- invalid timeouts
- employee workflows requiring IT Mode
- unapproved categories
- IT workflows not requiring confirmation
- admin workflows marked low risk

This should become a unit test in addition to startup validation.

## Local Workflow Executor

`LocalWorkflowExecutor` is the largest Core class. It handles:

- employee workflows
- IT workflows
- process execution
- registry hints
- cache cleanup
- report generation
- device/system diagnostics
- command validation for IT workflows

Strength: all local execution behavior is easy to find.

Weakness: one class has too many responsibilities. Suggested decomposition:

```text
NetworkWorkflowHandler
PerformanceWorkflowHandler
AppRepairWorkflowHandler
MeetingDeviceWorkflowHandler
SecurityWorkflowHandler
ReportWorkflowHandler
ItRepairWorkflowHandler
WindowsDiagnosticsService
SafeCleanupService
KnownApplicationService
```

## Logging Architecture

```mermaid
flowchart TD
    Workflow[Workflow result] --> LogAction[LogService.LogActionAsync]
    LogAction --> Combined[ZenIT.log]
    LogAction --> Typed{Type}
    Typed --> WorkflowLog[Workflow.log]
    Typed --> SystemLog[System.log]
    Typed --> ITModeLog[ITMode.log]
    Typed --> GuardianLog[HealthGuardian.log]
    Typed --> Errors[Errors.log]
    LogAction --> Fallback[%LocalAppData% fallback if needed]
```

Parsing:

- Reads primary, fallback, and active log path.
- Parses key-value entries.
- Caches parsed summaries until file signatures change.

## Report Architecture

`ReportDocument` is a common model. `ReportExporter` writes:

- TXT
- JSON
- HTML

The HTML report is self-contained and uses ZenHR colors. Report body content is currently English.

## Localization Architecture

`LocalizedStrings` is a static dictionary provider:

```text
language code -> key -> string
```

The app stores selected language in config and applies:

- navigation labels
- workflow card labels
- button labels
- page headings
- `FlowDirection`

Current validator checks:

- en/ar key existence
- missing keys
- duplicate keys beyond expected en/ar pairs
- required page/workflow labels

Future recommendation:

- move to `.resx` or JSON resource files
- add tooling that scans XAML and ViewModel visible strings
- localize dynamic status values

## Theme Architecture

`ThemeManager` normalizes theme values, but the actual visual system is mostly in `App.xaml` and `MainWindow.xaml`. `Light` and `HighContrast` are setting-level placeholders.

Future theme architecture:

```text
Themes\Dark.xaml
Themes\Light.xaml
Themes\HighContrast.xaml
ThemeManager applies merged dictionary
```

## IT Mode Architecture

```mermaid
flowchart LR
    User[IT user] --> Unlock[Username + PasswordBox]
    Unlock --> Hash[PasswordHashService]
    Hash --> Config[ITModePasswordHash]
    Unlock --> Session[IsItModeUnlocked]
    Session --> ITWorkflows[WorkflowRegistry.ITWorkflows]
    ITWorkflows --> Confirmation[Confirmation modal]
    Confirmation --> Executor[LocalWorkflowExecutor]
    Executor --> AdminCheck{Requires admin?}
    AdminCheck -->|not elevated| Stop[Return admin-required result]
    AdminCheck -->|elevated| Command[Run allowlisted command]
```

IT Mode is a UX and workflow gate. It is not a substitute for OS-level authorization.

## Health Guardian Architecture

```mermaid
flowchart TD
    Start[MainViewModel constructor] --> Guardian[HealthGuardianService.Start]
    Guardian --> Timer[60 second loop]
    Timer --> Check[Check configured process names]
    Check --> Observed{Was previously running?}
    Observed -->|No| Ignore
    Observed -->|Yes and stopped| Restart[Start known executable candidate]
    Restart --> Verify[Verify process running]
    Verify --> Log[HealthGuardian.log]
```

The guardian is not a Windows service. It is an in-process app background task.

## Deployment Architecture

```mermaid
flowchart LR
    Source[Source repo] --> Publish[Publish-ZenIT.ps1]
    Publish --> Exe[publish\win-x64\ZenIT.exe]
    Exe --> Install[Install-ZenIT.ps1]
    Exe --> JC[JumpCloud-Deploy-ZenIT.ps1]
    Install --> PF[C:\Program Files\ZenIT\ZenIT.exe]
    JC --> PF
    Install --> PD[C:\ProgramData\ZenIT]
    JC --> PD
    Install --> Shortcuts[Desktop + Start Menu shortcuts]
    JC --> Shortcuts
```

## Main Architectural Recommendations

1. Split `MainWindow.xaml` and `MainViewModel.cs` into page-level components.
2. Split `LocalWorkflowExecutor` into workflow handlers and shared diagnostic services.
3. Add a small dependency injection container or composition root.
4. Move IT credential policy to a protected admin-writable location.
5. Add unit tests for workflow integrity, command allowlists, report privacy, config normalization, and log parsing.
6. Replace static dictionary localization with resource files and scanner tooling.
7. Implement a real managed Windows service for elevated repairs.

## Phase 26 Architecture Addendum

Phase 26 introduced a protected IT policy split:

```text
C:\ProgramData\ZenIT\Config\appsettings.json
  user/app preferences such as language, theme, update channel, and retention

C:\ProgramData\ZenIT\Policy\itpolicy.json
  EnableITMode, ITModeUsername, ITModePasswordHash, AllowITCredentialChanges, ContactITUrl
```

Install and JumpCloud deployment scripts grant Users Modify only to Config, Logs, and Reports. The Policy folder is admin/SYSTEM writable and Users read-only.

The app now uses `ITPolicyService` for IT Mode authentication and Contact IT URL policy. User-writable appsettings cannot override IT credentials.

Phase 26 also added `src/ZenIT.Tests`, making workflow registration, command allowlists, report privacy, localization, config/policy normalization, and log parsing executable checks rather than documentation-only expectations.
