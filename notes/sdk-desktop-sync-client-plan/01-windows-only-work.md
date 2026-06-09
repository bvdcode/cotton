## Windows-Only Work

Do not try to implement or verify these items on Linux. Keep them open until a Windows agent or a real Windows machine is used.

Windows entry rule:

- Start here when the current platform is Windows. Do not start with the main plan or broad backend/server diffs.
- Prefer verification before implementation: reproduce the exact UX or platform issue from the current desktop branch, capture the failure, then patch only the affected desktop/platform code.
- If the current platform is Linux, do not implement these items except for plan text or cross-platform code that is already proven by a non-Windows test and does not pretend to satisfy the Windows gate.
- Evidence required for closing any item here is a Windows run, screenshot, log, CI `windows-latest` result, or installed-app smoke. Linux screenshots and tests can support the item but cannot close it.

- [ ] Windows Explorer virtual files/placeholders through Cloud Files API.
  Required behavior: design and implement only on Windows; do not build Linux-side placeholder code for this item.
  Windows agent note: do not attempt this in the Linux agent. This needs Cloud Files API research/adapter work, Explorer-visible placeholders, hydration/dehydration behavior, and manual Explorer verification.
  Verification: Windows Explorer manual test plus focused platform-adapter tests where possible.
- [ ] Windows shell integration.
  Required behavior: installer/start-menu registration, application identity, notification identity, taskbar/window icon, uninstall cleanup, and autostart behavior are verified from an installed app, not from Visual Studio debug launch.
  Windows agent note: verify installed app identity separately from Visual Studio/debug launch because debug launch may legitimately show raw process identity or generic shell metadata.
  Linux-side partial 2026-06-08: `.cotton-sync` creation now applies the Windows `Hidden` file attribute through `SyncMetadataDirectory`, and existing metadata folders are re-hidden when a sync root is probed. Windows Explorer verification is still required before closing shell integration.
  Verification: clean Windows install/uninstall smoke.
- [ ] Windows tray behavior.
  Required behavior: tray menu labels are short and unambiguous, pause/resume remains available during active sync, icon changes reflect syncing/paused/error only, and the normal Cotton icon is used when healthy/idle.
  Windows agent note: run this with at least two sync pairs, one active transfer, global pause/resume, disabling one pair while globally paused, and app restart while paused. This is the handoff for the current work-order sync-controls and tray-polish items closed on Linux.
  Verification: Windows manual check with active upload/download and paused sync.
- [ ] Windows large-tree and large-file desktop smoke.
  Required behavior: run the packaged or debug Windows desktop app against a suitable public/dedicated backend account with enough quota; cover many small files, at least 100 MiB, and a larger multi-GiB transfer where quota allows.
  Windows agent note: record both UI behavior and sync correctness. The desktop must stay responsive, keep progress moving, preserve pause/resume availability, converge server/local/baseline state after completion, keep the top progress card global instead of per-file, keep speed/ETA visually stable, and keep folder rows/actions reachable in a small window.
  Verification: record scan latency, first-progress latency, throughput, ETA stability, UI responsiveness, final server convergence, final SQLite baseline state, and screenshots/video for dashboard and folder-list layout.

- [ ] Windows startup and browser-login flow.
  Required behavior: saved sessions show Connecting/restoring state instead of the login form, server probing completes against the real Cotton server, Enter submits password login, app-code browser login can approve and return, and auth errors stay readable without pushing the form offscreen.
  Windows agent note: test both Visual Studio/debug launch and installed/packaged launch because identity and token-store behavior can differ. This is the handoff for the current work-order startup/auth item closed on Linux.
  Verification: Windows manual restart/sign-in/browser-approval check with screenshots or logs.
