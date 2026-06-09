## Product Target

Cotton Sync Desktop is a polished Windows/Linux desktop application that behaves like a real cloud-folder sync client:

- [ ] The user can install and launch the application without developer tools.
- [x] The user can sign in with Cotton credentials and TOTP when required.
  Verification 2026-06-09: password/TOTP auth remains implemented through `PasswordAuthFlow`, with human-readable desktop errors and Enter-submit behavior covered in the current work-order. Browser/app-code login is also integrated, while Windows browser-flow manual verification remains tracked separately.
- [x] The user can configure one or more sync pairs: local folder to remote Cotton folder.
  Verification 2026-06-09: sync-pair settings, validation, EF persistence, remote folder picker, dashboard rows, pair enable/disable/delete, and multiple-pair UI state are implemented and covered by Phase 1/8/current-work-order evidence.
- [x] The application can start with the operating system.
  Verification 2026-06-09: OS autostart is implemented through the Windows Run adapter and Linux XDG autostart adapter, with development-launch protection and packaging cleanup recorded in Phase 9/21. Manual reboot/login verification remains open in the lifecycle and release-gate items.
- [ ] The application runs continuously in the background and is controlled from the tray.
- [x] Local changes are uploaded automatically.
  Verification 2026-06-09: local watcher/coalescing, sync-pair queues, continuous supervisor, SDK upload pipeline, and end-to-end local-create upload evidence are recorded in Phases 6 and 12.
- [x] Remote changes are downloaded automatically.
  Verification 2026-06-09: SignalR wakeups, durable change-feed catch-up, periodic reconcile fallback, remote crawler, and end-to-end remote-create/update download evidence are recorded in Phases 3, 6, and 12.
- [x] Conflicts preserve both versions and are visible to the user.
  Verification 2026-06-09: stale update/delete races and simultaneous edits create conflict copies instead of silent overwrite, with conflict naming uniqueness and desktop conflict/error UI recorded in Phases 4, 5, 8, and 12.
- [x] Dangerous deletes are blocked before data loss.
  Verification 2026-06-09: local deletes use safe quarantine, remote deletes default to trash, and per-run mass-delete guards block over-limit local/remote delete batches before destructive work. Phase 5 and Phase 12 record the corresponding verification.
- [x] The application exposes clear status, progress, diagnostics, and logs.
  Verification 2026-06-09: dashboard status/progress, activity history, diagnostics screen, diagnostics bundle export, log rotation, redaction, self-test, and current Linux visual-smoke evidence are recorded in Phases 8, 10, and the current work-order. Final manual screenshot/diagnostics review remains open in the release gate.
- [ ] Release packages are built and smoke-tested on clean Windows and Linux machines.
