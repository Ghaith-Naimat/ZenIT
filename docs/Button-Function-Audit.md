# ZenIT Button Function Audit

ZenIT only runs registered workflows. Employee workflows stay non-elevated and avoid destructive repair. IT workflows require IT Mode and confirmation; admin-only actions stop safely when ZenIT is not elevated.

The startup workflow validator logs registry inconsistencies before users run any workflow. Workflow IDs are not accepted from user input outside the registered catalog.

| Visible button | Workflow ID | Tier | Category | User sees | Checks | Fixes / commands | Admin? | Risk | Logs? | Reports? | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Fix Internet | InternetNotWorking | Employee | Connectivity | Internet repair completed, or clear contact-IT message | Wi-Fi/Ethernet state, disabled adapters, network, IP, gateway, DNS, ping, proxy, VPN hint | `ipconfig /flushdns`, `/release`, `/renew`; no winsock/TCP reset | No | Low | Yes | No | Real fix |
| Fix Everything | FixEverythingSafe | Employee | Recommended | Device optimization completed successfully | Health, internet, DNS, updates hint, disk, Chrome state, Drive state, audio, camera/mic, security | Safe health checks, DNS flush, temp cleanup, automatic Chrome repair, Drive status check | No | Low | Yes | No | Real fix |
| Speed Up Device | ImproveDevicePerformance | Employee | Performance | Optimization complete with recovered space | CPU, RAM, disk, uptime, pending reboot | Clears `%TEMP%`, user temp, accessible Windows temp, Delivery Optimization cache, WER temp; skips Recycle Bin; Explorer refresh | No | Low | Yes | No | Real fix |
| Fix Chrome | ChromeNotWorking | Employee | Productivity | Chrome refresh completed or clear IT-support message | Chrome visible/hidden/background process state, responsiveness, network availability | Gracefully closes Chrome, terminates only allowlisted Chrome remnants if needed, clears safe current-user Chrome cache, restarts Chrome if it was running, verifies restart | No | Low | Yes | No | Real fix |
| Fix Camera & Mic | CameraOrMicrophoneNotWorking | Employee | Meetings | Meeting device check completed | Privacy state, camera/mic hints, Zoom/Chrome running | None | No | Low | Yes | No | Diagnostic only |
| Fix Sound | SoundNotWorking | Employee | Meetings | Sound check completed | Windows Audio process, audio endpoint hints | None | No | Low | Yes | No | Diagnostic only |
| Fix Slack | SlackNotWorking | Employee | Productivity | Slack refresh completed or clear IT-support message | Slack visible/hidden/background process state, internet | Gracefully closes Slack, terminates only allowlisted Slack remnants if needed, clears safe Slack cache, restarts Slack if it was running, verifies restart | No | Low | Yes | No | Real fix |
| Fix Zoom | ZoomNotWorking | Employee | Meetings | Zoom refresh completed or clear IT-support message | Zoom visible/hidden/background process state, camera/mic hints, internet | Gracefully closes Zoom, terminates only allowlisted Zoom remnants if needed, clears safe Zoom cache, restarts Zoom if it was running, verifies restart | No | Low | Yes | No | Real fix |
| Fix Google Drive | GoogleDriveNotSyncing | Employee | Productivity | Google Drive check completed or clear IT-support message | Drive visible/hidden/background process state, internet, disk | Gracefully closes Drive, terminates only allowlisted Drive remnants if needed, restarts Drive if it was running, verifies restart; no Drive cache/file deletion | No | Low | Yes | No | Diagnostic + safe restart |
| Security Check | SecurityCheck | Employee | Security | Security check completed | Kaspersky, firewall, BitLocker/Defender placeholders, JumpCloud hint | None | No | Low | Yes | No | Diagnostic only |
| Check My Device | DeviceHealthCheck | Employee | My Device | Device health check completed | DeviceHealthService values | Creates small health summary | No | Low | Yes | TXT | Real fix |
| Create Support Package | CollectITReport | Employee | Support | Support package created | Device, network, security, performance, failed services summary, critical event summary, latest ZenIT activity | Generates TXT, JSON, HTML | No | Low | Yes | TXT/JSON/HTML | Real fix |
| Contact IT | ContactIT | Employee | Support | Opens Slack support or manual-contact message | Fixed Slack URL only | Safe URL launch to `https://zenhr.slack.com/team/U09CGMUGV6K` | No | Low | Yes | No | Real fix |
| Full Windows Repair Check | FullWindowsRepairCheck | IT | Diagnostics | Windows repair check completed | Pending reboot, repair availability, failed services, critical events | None | No | Medium | Yes | No | Diagnostic only |
| Flush DNS | ItFlushDns | IT | Network Repair | DNS cache flushed | Command result | `ipconfig /flushdns` | No | Low | Yes | No | IT repair |
| Release & Renew IP | ItReleaseRenewIp | IT | Network Repair | IP address refreshed | Command results | `ipconfig /release`, `/renew` | No | Medium | Yes | No | IT repair |
| Restart Network Adapter | RestartNetworkAdapter | IT | Network Repair | Requires admin | Adapter restart availability | Placeholder until managed elevated service | Yes | High | Yes | No | Needs future service |
| DNS Repair | DnsRepair | IT | Network Repair | DNS repair check completed | DNS servers, resolution | Fallback DNS guidance only | Yes for changes | Medium | Yes | No | Diagnostic only |
| SFC Scan | SfcScan | IT | Windows Repair | Requires admin or completed | Elevation | `sfc /scannow` | Yes | High | Yes | No | IT-only repair |
| DISM ScanHealth | DismScanHealth | IT | Windows Repair | Requires admin or completed | Elevation | `DISM /Online /Cleanup-Image /ScanHealth` | Yes | Medium | Yes | No | IT-only repair |
| DISM Health Restore | DismHealthRestore | IT | Windows Repair | Requires admin or completed | Elevation | `DISM /Online /Cleanup-Image /RestoreHealth` | Yes | High | Yes | No | IT-only repair |
| Windows Update Repair | WindowsUpdateRepair | IT | Updates | Diagnostic report created | Update repair availability | Diagnostic only | Future admin | Medium | Yes | No | Diagnostic only |
| Temp Cleanup | ItTempCleanup | IT | Performance | Temp cleanup completed | Same as Speed Up Device | Safe temp cleanup | No | Medium | Yes | No | Real fix |
| Startup Analysis | StartupAnalysis | IT | Performance | Startup analysis completed | Startup registry counts, process count | None | No | Low | Yes | No | Diagnostic only |
| Winget Upgrade All | WingetUpgradeAll | IT | Updates | Upgrade check completed | Winget availability/output | `winget upgrade`, `winget upgrade --all --accept-package-agreements --accept-source-agreements` | Maybe | High | Yes | No | IT-only repair |
| Restart Print Spooler | RestartPrintSpooler | IT | Services | Requires admin or restarted | Elevation | `net stop/start spooler` | Yes | High | Yes | No | IT-only repair |
| Restart Windows Audio | RestartAudioServices | IT | Services | Requires admin or restarted | Elevation | `net stop/start AudioEndpointBuilder`, `net stop/start Audiosrv` | Yes | High | Yes | No | IT-only repair |
| Restart Windows Update | RestartWindowsUpdate | IT | Services | Requires admin or restarted | Elevation | `net stop/start wuauserv` | Yes | High | Yes | No | IT-only repair |
| Restart BITS | RestartBits | IT | Services | Requires admin or restarted | Elevation | `net stop/start bits` | Yes | High | Yes | No | IT-only repair |
| Network Stack Reset | NetworkStackReset | IT | Network Repair | Requires admin; restart may be needed | Elevation | `netsh int ip reset` | Yes | High | Yes | No | IT-only repair |
| Winsock Reset | WinsockReset | IT | Network Repair | Requires admin; restart required | Elevation | `netsh winsock reset` | Yes | High | Yes | No | IT-only repair |
| Advanced Event Report | AdvancedEventReport | IT | Reports | Event report exported | Critical/warning event summaries | Generates report | No | Low | Yes | TXT/JSON/HTML | Diagnostic only |
| Service Health Repair | ServiceHealthRepair | IT | Services | Service health check completed | Failed/stopped automatic services placeholder | None | No | Medium | Yes | No | Needs future service |
| Export Advanced Diagnostic Package | ExportAdvancedDiagnosticPackage | IT | Reports | Advanced diagnostic package exported | Non-private advanced diagnostics | Generates report | No | Medium | Yes | TXT/JSON/HTML | Diagnostic only |

## Privacy Boundaries

Reports and logs must not include passwords, browser history, cookies, tokens, personal document names, Downloads/Desktop contents, email content, chat content, or Google Drive file names.

## Branding

The official ZenIT logo source is `assets/logo/logo1.png`. The packaged WPF app embeds that logo at `src/ZenIT.App/Assets/logo.png`, uses `src/ZenIT.App/Assets/logo-display.png` for in-app display, and generates `src/ZenIT.App/Assets/ZenIT.ico` from the same official logo for the Windows application icon.
