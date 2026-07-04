# ZenIT Expanded Security Audit

Date: 2026-07-03
Version reviewed: 1.0.0

## Summary

ZenIT shows strong security intent: no plaintext password storage, no arbitrary employee command execution, app-level IT Mode gating, local-only reports, scoped ProgramData permissions, and privacy-aware support packages. The main security risks are not obvious credential leaks; they are enterprise hardening gaps around configuration tampering, app signing, and potentially surprising employee-mode actions.

## Severity Ratings

| Severity | Meaning |
| --- | --- |
| Critical | Direct compromise, credential exposure, destructive data loss, or remote code execution likely |
| High | Significant enterprise risk or user trust issue requiring fix before broad rollout |
| Medium | Important control gap or reliability/security concern |
| Low | Defense-in-depth or maintainability concern |
| Informational | Observation, acceptable current behavior, or future improvement |

## Findings

| ID | Severity | Finding | Evidence | Impact | Recommendation |
| --- | --- | --- | --- | --- | --- |
| SEC-001 | Mitigated | IT Mode hash was stored in a user-modifiable config directory | IT policy now lives in `C:\ProgramData\ZenIT\Policy\itpolicy.json`; install/deploy scripts grant Users read-only access | User-writable config can no longer override IT Mode credentials | Keep validating policy ACLs during deployment. |
| SEC-002 | Mitigated | Application workflows needed safe automatic process recovery | Employee workflows now use `ApplicationProcessManager` with explicit per-app process allowlists, graceful close first, bounded force cleanup only for supported app remnants, restart when appropriate, and verification | Removes Task Manager/manual-close burden while avoiding arbitrary process termination | Keep allowlists reviewed before adding new supported applications. |
| SEC-003 | Mitigated | Speed Up Device emptied Recycle Bin | Employee performance workflow now skips Recycle Bin cleanup | Avoids permanent deletion of user-recoverable files | Recycle Bin cleanup should remain IT-only or explicit-confirmation only. |
| SEC-004 | Medium | Application and scripts are not signed by default | `Publish-ZenIT.ps1` supports optional signing, but unsigned builds still publish | JumpCloud deployment can work, but enterprise trust and tamper resistance are weaker until a real certificate is used | Sign `ZenIT.exe` and deployment scripts with an approved certificate. Validate signature in deployment script. |
| SEC-005 | Medium | IT Mode is an app-level control, not an OS security boundary | Local app checks username/password and admin commands run if process is elevated | If the app runs elevated and IT Mode is bypassed by local tampering, IT repairs become available. | Treat IT Mode as support UX, not authorization. Move privileged repair execution to a signed Windows service with stronger authz. |
| SEC-006 | Medium | SHA256 without salt/KDF is used for a shared IT password | `PasswordHashService` hashes raw password with SHA256 | Acceptable against accidental disclosure, weaker against offline cracking if config is copied. | Use PBKDF2/Argon2 with salt, or certificate/device trust for IT Mode. |
| SEC-007 | Medium | Health Guardian can restart apps without user prompt | In-process monitor restarts observed apps silently | Could restart apps at inconvenient times after crash/update; may conflict with software updates. | Add update detection/backoff and admin-configurable opt-out. Log reason and throttle attempts. |
| SEC-008 | Low | Config writes are not atomic | `File.WriteAllText` in settings service/scripts | Interrupted writes could corrupt settings. | Write to temp file and replace atomically. Keep backup copy. |
| SEC-009 | Low | Some catch-all blocks suppress useful diagnostics | Startup maintenance and logging intentionally swallow some errors | Good for UX, but can hide recurring issues. | Log suppressed errors to `Errors.log` where safe. |
| SEC-010 | Low | Legacy ActionExecutor code remains | `Actions`, `LocalActionExecutor`, `MockActionExecutor` remain after workflow migration | Future engineer could wire old paths accidentally. | Remove legacy action system or move to a `Legacy` namespace with clear tests. |
| SEC-011 | Informational | Employee command execution is allowlisted | `ProcessRunner` permits only `ipconfig` selected args and `explorer.exe` known folders | Good control. | Keep exact argument allowlist. Add tests. |
| SEC-012 | Informational | Support packages are privacy-conscious | Report footer and data collection exclude private content | Good privacy posture. | Add automated report privacy regression tests. |

## Credential Storage

The app stores:

- `ITModeUsername` as plaintext in protected policy.
- `ITModePasswordHash` as SHA256 hex in protected policy.

The app does not store:

- plaintext password
- admin credentials
- tokens
- secrets

Password handling:

- PasswordBox value is bridged to ViewModel because WPF binding does not support secure password binding directly.
- Password string is cleared after attempts.
- Login logs include attempted username and result only.

Security note: the password hash is now separated from user preferences and stored in the protected policy folder.

## Authorization And Elevation

Employee Mode:

- Does not elevate.
- Does not prompt for admin credentials.
- Uses registered workflows only.

IT Mode:

- Requires app-level login.
- Requires confirmation for IT workflows.
- Admin workflows check whether process is elevated and stop safely when not elevated.

Recommendation: future privileged operations should move to `ZenIT.Service`, running signed and audited workflows only.

## Command Execution

Employee command execution:

- `ipconfig /flushdns`
- `ipconfig /release`
- `ipconfig /renew`
- `ipconfig /registerdns` only in elevated condition inside internet workflow
- `explorer.exe` only for known Logs/Reports folders

IT command execution:

- exact allowlist in `ValidateControlledCommand`
- includes SFC, DISM, net service restarts, netsh reset, winget upgrade

No arbitrary command text is accepted from UI.

## File System Safety

Safe practices:

- Reports/logs stay under `C:\ProgramData\ZenIT`.
- Retention cleanup checks paths remain under ZenIT root.
- Report filenames are sanitized.
- Logs fall back to LocalAppData on permission failure.

Concerns:

- Recycle Bin emptying in Employee Mode.
- ProgramData Config write permission for Users.
- Cache cleanup and temp cleanup should have automated path boundary tests.

## Report Privacy

Collected:

- device name
- user name
- serial/manufacturer/model
- Windows version/build
- uptime
- IP/gateway/DNS/MAC
- CPU/RAM/disk/battery
- firewall/BitLocker/Kaspersky/JumpCloud hints
- failed services placeholder
- critical event placeholder
- latest ZenIT friendly logs

Not collected by design:

- browser history
- cookies
- passwords
- tokens
- documents
- downloads
- personal file names
- emails
- chat messages
- Google Drive filenames
- full installed software list

Recommendation: add automated tests that inspect generated TXT/JSON/HTML reports for forbidden tokens and path patterns.

## Installer And JumpCloud Security

Strengths:

- Requires admin/SYSTEM.
- Grants Users Modify only on Config, Logs, Reports.
- Does not modify Program Files permissions.
- Creates shortcuts with EXE icon.
- Stops app before replacement.
- Keeps ProgramData by default on uninstall.

Risks:

- Scripts are not signed.
- `JumpCloud-Deploy-ZenIT.ps1` may force-stop ZenIT after 10 seconds. That is acceptable for managed deployment but should be scheduled carefully.
- Config directory permissions are too broad for IT Mode policy.

## Production Security Recommendation

Before full rollout:

1. Remove or confirm Recycle Bin cleanup in Employee Mode.
2. Maintain strict allowlists for automatic application process recovery.
3. Protect IT credential config separately from user-writable preferences.
4. Code-sign EXE and scripts.
5. Add automated tests for command allowlist, report privacy, path safety, workflow tiering, and config normalization.
