# ZenIT Technical Debt Register

Date: 2026-07-03
Version reviewed: 1.0.0

## Priority Legend

| Priority | Meaning |
| --- | --- |
| P0 | Release blocker or serious safety issue |
| P1 | Should fix before broad company rollout |
| P2 | Should fix in next minor release |
| P3 | Backlog improvement |

## Debt Items

| ID | Priority | Title | Description | Impact | Difficulty | Estimate | Recommended solution |
| --- | --- | --- | --- | --- | --- | --- | --- |
| TD-001 | Done | Application process recovery consistency | Chrome, Slack, Zoom, and Drive workflows needed a safe automatic lifecycle instead of manual close guidance. | Users could be asked to use Task Manager or rerun workflows. | Medium | Completed in Phase 28 | `ApplicationProcessManager` now performs allowlisted diagnose, graceful close, force cleanup, restart, and verification. |
| TD-002 | Done | Recycle Bin emptied in Speed Up Device | Performance workflow called Recycle Bin emptying. | Possible deletion of user-recoverable files. | Low | Completed in Phase 26 | Employee Mode now skips Recycle Bin cleanup. |
| TD-003 | Done | User-writable IT Mode config | Config folder had Users Modify and contained IT hash. | Local tampering risk. | Medium | Completed in Phase 26 | IT credentials moved to protected policy file with Users read-only ACL. |
| TD-004 | Done | No automated test suite | Validation scripts existed, but no unit tests. | Regression risk as workflows grow. | Medium | Completed in Phase 26 | Added `src/ZenIT.Tests` with workflow, allowlist, privacy, localization, config, and log parsing tests. |
| TD-005 | Partial | No code signing | EXE/scripts are not signed by default. | Enterprise trust and tamper resistance gap. | Medium | Signing hook added; certificate pending | `Publish-ZenIT.ps1` now supports optional signing. Broad rollout still requires a real certificate. |
| TD-006 | P2 | Monolithic `MainViewModel` | 1600+ lines and many responsibilities. | Hard to test and maintain. | High | 5-10 days | Split into Shell/Home/QuickFixes/MyDevice/ITMode/Logs/About ViewModels. |
| TD-007 | P2 | Monolithic `MainWindow.xaml` | 1700+ lines containing all pages. | Visual changes are risky and hard to review. | Medium | 3-7 days | Extract views and reusable controls. |
| TD-008 | P2 | Monolithic `LocalWorkflowExecutor` | 1400+ lines covering all workflows and helper logic. | Workflow changes can regress unrelated actions. | High | 5-10 days | Split by workflow domain and shared services. |
| TD-009 | P2 | Legacy Action system remains | `ActionRegistry`, `LocalActionExecutor`, `SupportActionViewModel` remain after workflow migration. | Confusion and accidental reuse. | Low | 1 day | Remove if unused or mark/namespace as legacy with tests. |
| TD-010 | P2 | Incomplete dynamic localization | Health/diagnostic/status values in ViewModel are hardcoded English. | Arabic UI can show mixed language. | Medium | 2-5 days | Introduce localization keys for status item labels/values and result strings. |
| TD-011 | P2 | Localization validator only checks key parity | It does not scan visible strings in XAML/ViewModels. | False confidence in localization completeness. | Medium | 2-4 days | Add static scan for quoted visible strings and binding coverage. |
| TD-012 | P2 | Health score approximation | CPU uses process count; pending reboot contributes twice. | Score can be misleading. | Low | 1-2 days | Use real CPU/RAM counters and review score weights. |
| TD-013 | P2 | Health Guardian is in-process | It only runs while ZenIT is open. | May not match enterprise background-monitor expectation. | Medium | 3-8 days | Move monitoring to future Windows service or start app at login with clear policy. |
| TD-014 | P2 | Typed logs not fully surfaced | Logs UI parses combined candidates. | Future typed-only logs may not appear. | Low | 1-2 days | Parse `Workflow.log`, `ITMode.log`, `System.log`, and `HealthGuardian.log` directly. |
| TD-015 | P2 | Config writes are not atomic | Settings save uses direct write. | Power loss can corrupt config. | Low | 1 day | Write temp file and atomic replace. |
| TD-016 | P2 | Scripts do not enforce signature/source trust | Deployment copies source EXE path as configured. | Tampered EXE could be deployed if source is compromised. | Medium | 2-4 days | Validate file hash/signature before copy. |
| TD-017 | P3 | Theme system is placeholder | Settings support Light/HighContrast, UI resources are dark-only. | Future accessibility work blocked. | Medium | 3-7 days | Introduce merged theme resource dictionaries. |
| TD-018 | P3 | Accessibility labels are incomplete | Icons/images/status chips lack explicit automation labels. | Screen reader experience weaker. | Medium | 2-5 days | Add `AutomationProperties.Name` and keyboard QA checklist. |
| TD-019 | P3 | Report content is English-only | UI is bilingual but report body is not. | Arabic-speaking IT/user handoff less polished. | Medium | 3-5 days | Localize report templates or keep technical reports English by policy. |
| TD-020 | P3 | No central telemetry | Logs are local only. | Fleet support requires manual collection. | High | 1-2 sprints | Add approved central log forwarding or JumpCloud collection workflow. |

## First Five Improvements If Inheriting The Project

1. Keep automatic application recovery constrained to explicit per-application allowlists and continue excluding personal data cleanup.
2. Harden IT credential storage and sign deployment artifacts.
3. Add Core unit tests for workflow registry, command allowlists, report privacy, logging, and config.
4. Split `MainViewModel`, `MainWindow.xaml`, and `LocalWorkflowExecutor` into smaller units.
5. Complete runtime localization and accessibility coverage.
