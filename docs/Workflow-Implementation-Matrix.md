# ZenIT Workflow Implementation Matrix

ZenIT separates employee-safe workflows from IT Mode workflows. Employee workflows are non-destructive and do not require elevation. IT workflows are visible only after IT Mode unlock, require confirmation, and stop safely when administrator permissions are required but unavailable.

Workflow integrity is validated at startup by `WorkflowIntegrityValidator`. Any duplicate ID, missing registration, category mismatch, tier mismatch, or risk/admin inconsistency is written to the system log.

| User button | Tier | Real checks performed | Real fixes performed | Needs admin service later? | Safety notes | Current status |
| --- | --- | --- | --- | --- | --- | --- |
| Fix Internet | Employee | Adapter, IP, gateway, internet ping, DNS resolution, proxy, VPN process hints | `ipconfig /flushdns`, `/release`, `/renew` | Yes, for adapter resets and deeper stack repair | No network stack reset or adapter toggle in Employee Mode | Real fix |
| Speed Up Device | Employee | CPU, RAM, disk, uptime, pending reboot | Clears current-user temp, accessible Windows temp, Delivery Optimization cache, and WER temp. Recycle Bin is not emptied. | Yes, for deeper repair | Does not delete Downloads, Documents, Desktop, Pictures, browser history, cookies, or run SFC/DISM/CHKDSK | Real fix |
| Fix Chrome | Employee | Chrome visible/hidden/background process state, responsiveness, network availability | Automatically closes Chrome gracefully, terminates allowlisted Chrome background remnants when needed, clears safe Chrome cache, restarts Chrome only if it was running, and verifies the process | Maybe, for managed browser repair | Only allowlisted Chrome processes are terminated. Does not delete bookmarks, history, cookies, passwords, profiles, extensions, or downloads | Real fix |
| Fix Camera & Mic | Employee | Privacy state, camera hints, microphone hints, Zoom/Chrome state | None | Yes, for services/drivers/default devices | Read-only diagnostics only | Diagnostic only |
| Fix Sound | Employee | Audio engine hint and audio device registry hints | None | Yes, for service restart | Does not restart services in Employee Mode | Diagnostic only |
| Fix Slack | Employee | Slack visible/hidden/background process state and network state | Automatically closes Slack gracefully, terminates allowlisted Slack remnants when needed, clears safe Slack cache, restarts Slack only if it was running, and verifies restart | Maybe, for managed app repair | Only allowlisted Slack processes are terminated. Does not delete downloads, workspace files, tokens, or user files | Real fix |
| Fix Zoom | Employee | Zoom visible/hidden/background process state, network state, meeting-device guidance | Automatically closes Zoom gracefully, terminates allowlisted Zoom remnants when needed, clears safe Zoom cache, restarts Zoom only if it was running, and verifies restart | Maybe, for managed app repair | Only allowlisted Zoom processes are terminated. Does not delete recordings, meeting files, or user documents | Real fix |
| Fix Google Drive | Employee | Drive visible/hidden/background process state, network state, disk free space | Automatically closes and restarts allowlisted Google Drive processes when Drive was already running, then verifies sync process presence | Maybe, for managed Drive repair | Does not touch Drive cache, synced files, account state, or file names | Diagnostic + safe restart |
| Security Check | Employee | Kaspersky, Firewall, BitLocker placeholder, JumpCloud, Defender placeholder | None | Yes, for richer endpoint/security repair | Does not modify security settings | Diagnostic only |
| Device Health Check | Employee | DeviceHealthService values | Creates a small device summary | No for current scope | No private content collection | Real fix |
| Create Support Package | Employee | Device, network, performance, security, Windows health, latest ZenIT logs | Generates TXT, JSON, HTML package | Yes, for richer Event Viewer/service inventory | Excludes private files, browser data, cookies, passwords, tokens, chats, email, and Drive file names | Real fix |
| Contact IT | Employee | None beyond launch/log attempt | Opens IT Support Slack profile | No | Uses a fixed support URL only | Real fix |
| Full Windows Repair Check | IT | Pending reboot, repair readiness, failed-service/event placeholders | None | Yes, for deeper Windows diagnostics | Diagnostic-only sequence | Diagnostic only |
| SFC Scan | IT | Admin check | `sfc /scannow` when elevated and confirmed | No | IT Mode only, confirmation required | IT repair |
| DISM Health Restore | IT | Admin check | `DISM /Online /Cleanup-Image /RestoreHealth` when elevated and confirmed | No | IT Mode only, confirmation required | IT repair |
| Windows Update Repair | IT | Admin/update diagnostic availability | Diagnostic report only in current version | Yes | No destructive reset in current version | Diagnostic only |
| Restart Print Spooler | IT | Admin check | `net stop/start spooler` when elevated and confirmed | No | IT Mode only, confirmation required | IT repair |
| Restart Audio Services | IT | Admin check | Restarts audio endpoint and Windows Audio services when elevated and confirmed | No | IT Mode only, confirmation required | IT repair |
| Network Stack Reset | IT | Admin check | `netsh int ip reset` when elevated and confirmed | No | Warns restart may be required | IT repair |
| Winsock Reset | IT | Admin check | `netsh winsock reset` when elevated and confirmed | No | Warns restart required | IT repair |
| Winget Upgrade All | IT | Checks winget upgrade output | `winget upgrade --all` after confirmation | Maybe, depending app installers | No agreement auto-accept flags are used | IT repair |
| Advanced Event Report | IT | Critical/warning event summary placeholder | Exports TXT/JSON/HTML report | Yes, for richer event API coverage | No private data | Diagnostic only |
| Service Health Repair | IT | Failed/stopped automatic service placeholder | None | Yes, for managed service repair | Does not restart all services automatically | Diagnostic only |
| Export Advanced Diagnostic Package | IT | Advanced non-private diagnostic summary | Exports TXT/JSON/HTML package | Yes, for deeper inventory | No private data | Diagnostic only |

## Logging And Reports

- Every workflow result includes start time, finish time, duration, user message, technical message, and step results.
- `LogService` writes the combined `ZenIT.log` plus typed logs: `Workflow.log`, `System.log`, `ITMode.log`, and `Errors.log`.
- Logs rotate at 10 MB and keep 5 rotated files.
- Support package reports use reusable report exporters for TXT, JSON, and HTML.
