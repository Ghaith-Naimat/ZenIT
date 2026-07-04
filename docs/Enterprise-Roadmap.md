# ZenIT Enterprise Roadmap

Date: 2026-07-03
Version reviewed: 1.0.0

## Current Maturity

ZenIT v1.0.0 is a strong production pilot candidate. It has the right product shape, local privacy posture, support package generation, IT Mode, deployment scripts, validation scripts, and bilingual UI foundation. It should be rolled out gradually, with monitoring and feedback from IT and employees.

## v1.1

### Must Have

- Maintain the Phase 28 application recovery policy: automatic process recovery is allowed only for supported apps with explicit allowlists, bounded retries, logging, and verification. Recycle Bin cleanup remains excluded from Employee Mode.
- Maintain protected IT policy storage and ACL validation.
- Expand automated tests beyond Core into UI smoke tests.
- Sign `ZenIT.exe` and PowerShell deployment scripts with a production certificate.
- Localize dynamic device-health, IT diagnostic, and recent-activity strings.

### Should Have

- Parse typed logs directly in the IT Logs page.
- Add a stable workflow/category key model independent of localized display text.
- Add startup/performance duration logs.
- Add accessibility labels and tab-order QA.
- Add deploy-time signature/hash validation.

### Nice To Have

- Add "copy support package path" for IT Mode.
- Add report preview in IT Mode.
- Add a safe "open latest report" action.

## v1.2

### Must Have

- Split app UI into page controls and page ViewModels.
- Split workflow execution into domain handlers.
- Add dependency injection composition root.
- Add UI regression QA process for 1366x768, 1920x1080, 2560x1440, and 125/150/200 percent scaling.

### Should Have

- Improve health score with real CPU/RAM sampling.
- Add proper Windows service status APIs where available.
- Add richer Event Viewer summaries with privacy boundaries.
- Add localized report templates or formally document English-only technical reports.

### Nice To Have

- Add a "what ZenIT did" employee-friendly post-action summary.
- Add IT Mode filters/search for advanced workflows.
- Add guided troubleshooting copy for diagnostic-only workflows.

## v2.0

### Must Have

- Implement `ZenIT.Service` as a signed managed Windows service for elevated repairs.
- Move privileged workflow execution out of the WPF process.
- Add strong authorization for IT workflows, such as certificate/device trust or MDM-backed policy.
- Add centralized fleet observability approved by Security and IT.
- Add update mechanism or managed package lifecycle.

### Should Have

- Remote action queue integration with JumpCloud or another endpoint management platform.
- Full theme architecture with dark, light, and high contrast.
- Full bilingual reports and docs.
- Enterprise policy templates for allowed workflows per group.

### Nice To Have

- AI-assisted support summary generation using approved private-data controls.
- Endpoint trend dashboard for IT.
- Employee feedback flow integrated with IT ticketing.

## Long-Term Vision

ZenIT can become ZenHR's endpoint self-healing layer:

- employees get simple problem-based fixes
- IT gets auditable diagnostics and controlled repair tools
- JumpCloud handles deployment and policy
- a signed service handles elevated repairs
- reports stay privacy-preserving
- logs can be collected centrally under approved policy

The current app is a good foundation, but the next maturity jump depends on separating UI, policy, and privileged execution.

## Prioritized Implementation Plan

1. Safety alignment: allowlisted process recovery and Recycle Bin policies.
2. Security hardening: config ACL split, signing, hash validation.
3. Test foundation: Core unit tests and report privacy tests.
4. Maintainability: split ViewModel/XAML/executor.
5. Localization/accessibility: dynamic strings, screen-reader labels, RTL QA.
6. Fleet operations: central log collection and managed service design.
