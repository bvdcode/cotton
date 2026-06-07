## Windows-Only Work

Do not try to implement or verify these items on Linux. Keep them open until a Windows agent or a real Windows machine is used.

- [ ] Windows Explorer virtual files/placeholders through Cloud Files API.
  Required behavior: design and implement only on Windows; do not build Linux-side placeholder code for this item.
  Verification: Windows Explorer manual test plus focused platform-adapter tests where possible.
- [ ] Windows shell integration.
  Required behavior: installer/start-menu registration, application identity, notification identity, taskbar/window icon, uninstall cleanup, and autostart behavior are verified from an installed app, not from Visual Studio debug launch.
  Verification: clean Windows install/uninstall smoke.
- [ ] Windows tray behavior.
  Required behavior: tray menu labels are short and unambiguous, pause/resume remains available during active sync, icon changes reflect syncing/paused/error only, and the normal Cotton icon is used when healthy/idle.
  Verification: Windows manual check with active upload/download and paused sync.
- [ ] Windows large-tree and large-file desktop smoke.
  Required behavior: run the packaged or debug Windows desktop app against a suitable public/dedicated backend account with enough quota; cover many small files, at least 100 MiB, and a larger multi-GiB transfer where quota allows.
  Verification: record scan latency, first-progress latency, throughput, ETA stability, UI responsiveness, final server convergence, and final SQLite baseline state.
