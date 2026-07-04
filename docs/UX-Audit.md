# ZenIT UX Audit

Date: 2026-07-03
Version reviewed: 1.0.0

## Summary

ZenIT now feels much closer to an internal enterprise product than a technical toolbox. The left navigation, premium cards, ZenHR colors, simplified Home, support package action, Contact IT path, and Arabic/English language switcher are all strong. Phase 28 intentionally made supported app repairs automatic; the main UX risks are now communicating progress clearly, keeping recovery allowlists narrow, and avoiding technical status strings in diagnostics and logs.

## Design Strengths

- The app shell is clear and familiar: sidebar navigation plus flexible content area.
- Employee sidebar is appropriately simple: Home, Quick Fixes, My Device, IT Mode, About.
- Logs are hidden inside IT Mode, which reduces employee complexity.
- The Home page emphasizes recommended actions instead of exposing every workflow immediately.
- Workflow cards use consistent title, description, badge, status, and button layout.
- Colors match the requested ZenHR palette.
- The About page uses brand imagery, privacy, security, and version without overloading employees.
- Arabic/RTL support has a real architecture rather than being an afterthought.

## Page-by-Page UX Review

### Home

Purpose: get employees to the right help quickly.

Strengths:

- Recommended workflows make the app feel like an assistant.
- Recent Activity gives confidence that actions are recorded.
- Contact IT is visible and simple.

Issues:

| Priority | Problem | Impact | Suggested solution |
| --- | --- | --- | --- |
| Medium | Home actions are hardcoded in `UpdateHomeSummary` | Changing recommended actions requires code changes | Drive Home selection from workflow metadata such as `IsRecommended` and a rank |
| Medium | Recent Activity relative time strings are hardcoded English | Arabic mode may show English dates like Today/Yesterday | Localize relative time formatting |
| Low | Device status and support status may repeat similar information | Slight visual noise | Keep only one status summary in Home |

### Quick Fixes

Purpose: full employee workflow catalog.

Strengths:

- Search and categories are valuable for a growing catalog.
- Cards are compact and familiar.
- Status and duration make workflow completion transparent.

Issues:

| Priority | Problem | Impact | Suggested solution |
| --- | --- | --- | --- |
| Medium | Automatic app recovery can surprise employees if progress language is vague | Trust loss or work interruption | Keep progress states explicit: closing app, refreshing cache, restarting, verifying. Keep process recovery allowlisted and logged. |
| Medium | Category filtering uses localized display names | Filtering can become fragile after language switch | Use stable category keys plus localized display names |
| Medium | Diagnostic-only workflows may still feel like "fix" buttons | Users may believe a problem was repaired | Use precise result language: checked, issue detected, contact IT |

### My Device

Purpose: device overview and support package creation.

Strengths:

- Health score is easy to understand.
- Status chips and sections are useful for quick triage.
- Report actions are easy to find.

Issues:

| Priority | Problem | Impact | Suggested solution |
| --- | --- | --- | --- |
| High | Health score uses approximate checks and a pending reboot double-count pattern | Score may appear more precise than it is | Document scoring in-app or simplify to status bands |
| Medium | Many dynamic labels/values are still hardcoded English | Arabic mode and localization audit are incomplete | Move health labels/status values to localization keys |
| Medium | CPU usage uses process count proxy | Misleading performance diagnosis | Use a short CPU counter sample or WMI/performance API |

### IT Mode

Purpose: advanced support-only diagnostics and repair entry point.

Strengths:

- Unlock screen is clear.
- Dashboard, logs, reports, and repairs are separated.
- Confirmation requirement is appropriate.

Issues:

| Priority | Problem | Impact | Suggested solution |
| --- | --- | --- | --- |
| Medium | IT Mode appears in sidebar for all users | Some curiosity/click friction | Keep visible if desired, but add brief "for IT only" explanation |
| Medium | Some IT diagnostics are placeholders | IT may expect deeper data | Label placeholders clearly as "requires managed service" |
| Low | Admin-required behavior is text-only | IT may not know how to rerun elevated | Add deployment/IT instructions outside employee UI |

### Logs

Purpose: IT-only support history.

Strengths:

- Search, filter, sort, timeline, export, open folders.
- Hiding logs from employees is correct.

Issues:

| Priority | Problem | Impact | Suggested solution |
| --- | --- | --- | --- |
| Medium | Result strings are not fully normalized | Filtering can miss variants like "Needs IT" | Store result enum in log and localize display |
| Low | Large log histories are not virtualized | Potential slow UI on older devices | Add virtualization or paging if logs grow |

### About

Purpose: brand, purpose, privacy, and security reassurance.

Strengths:

- Employee-focused after metadata cleanup.
- Privacy/security language is clear.
- Logo treatment is better than earlier circular/cropped variants.

Issues:

| Priority | Problem | Impact | Suggested solution |
| --- | --- | --- | --- |
| Low | Logo image should have accessible name | Screen readers may skip context | Set automation label or adjacent text relationship |

## Accessibility Review

Strengths:

- Buttons have consistent minimum heights.
- Focus style exists in primary button style.
- WPF text uses Segoe UI.
- High-DPI should benefit from WPF layout and image scaling.

Gaps:

- Many controls do not explicitly set `AutomationProperties.Name`.
- Tab order is implicit and should be verified page-by-page.
- Status color is used heavily; text labels should always accompany color.
- Arabic RTL needs visual QA at 1366x768 and 1920x1080 after every page change.
- Icons are text glyphs/codes rather than semantic icon controls, so screen reader meaning may be weak.

## Animation And Responsiveness

Current UX includes hover/pressed states, toast status, async commands, and fixed card status areas. That is good. Future improvements should be subtle:

- 150-200 ms page fade.
- Card hover elevation.
- Non-blocking progress for long IT workflows.
- Avoid `MessageBox` except fatal startup failures.

## Enterprise Product Recommendations

1. Make every potentially disruptive Employee Mode action explicit and reversible where possible.
2. Replace approximate health score details with honest status wording.
3. Finish localization of dynamic diagnostic strings.
4. Add accessibility labels to workflow cards, icon badges, language switcher, logo, and status chips.
5. Split the monolithic XAML into page controls to make visual QA easier.
