# ZenIT Security Audit

Date: 2026-07-03
Version: 1.0.0

## Scope

Reviewed WPF app startup, workflow registry, local executor, process execution, IT Mode authentication, logging, reports, configuration, deployment scripts, and privacy boundaries.

## Findings And Mitigations

| Area | Finding | Mitigation |
| --- | --- | --- |
| Credentials | IT Mode uses username plus password but must not store plaintext. | Config stores only `ITModePasswordHash` as SHA256. PasswordBox values are cleared after login attempts. Logs never include password values. |
| Credential changes | End users must not change IT credentials. | `AllowITCredentialChanges` is forced false by app normalization and deployment scripts. No change-password UI is exposed. |
| Command execution | Arbitrary command execution would be unsafe. | Employee workflows call fixed code paths only. `ProcessRunner` and IT command execution use allowlisted executable/argument pairs. |
| Elevation | Employee workflows must not elevate. | No elevation is attempted. Admin workflows are IT Mode only, confirmation-gated, and return a friendly admin-required result if not elevated. |
| Logs | ProgramData permissions can fail for normal users. | Logging is fail-safe and falls back to `%LocalAppData%\ZenIT\Logs`. Installer grants Users Modify only on ProgramData Config, Logs, and Reports. |
| Log growth | Unbounded logs could fill disk. | Typed and combined logs rotate at 10 MB, keeping 5 rotated files. |
| Reports | Support packages could accidentally collect private data. | Report generation is allowlist-based and excludes browser history, cookies, saved passwords, tokens, personal files, document/download names, email, chat content, and Google Drive file names. |
| URL launch | Contact IT must not launch arbitrary input. | Contact IT uses a fixed Slack URL. |
| Config parsing | Invalid config values could cause inconsistent behavior. | `AppSettingsService` normalizes values and `AppSettingsValidator` logs configuration problems at startup. |
| Path traversal | Report/log filenames use device/user names. | Report exporter sanitizes filenames with invalid filename character replacement. |

## Remaining Risk

- IT Mode advanced repair commands depend on the local Windows admin context and should be used only by ZenHR IT.
- Full Windows service repair is intentionally deferred until a managed service can enforce stronger authorization and auditing.
- SHA256-only password hashing is acceptable for the current local shared IT credential model but should move to a stronger salted KDF or certificate-backed IT authorization in a later release.

## Safety Scan Notes

Expected IT-only strings such as `netsh winsock reset`, `netsh int ip reset`, `sfc`, `DISM`, and service restarts are present only in IT Mode controlled workflows and documentation. Employee workflows do not run those repairs.
