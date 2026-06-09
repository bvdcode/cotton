# Cotton Sync Desktop 0.1.0-dev Draft Release Notes

Draft status: not release-ready. This document records the current desktop-sync release story and the verification still required before a public or internal release can be called complete.

## Highlights

- Desktop sync client built on the existing .NET stack: `Cotton.Sdk`, `Cotton.Sync`, `Cotton.Sync.App`, EF Core SQLite, and Avalonia.
- Full-mirror sync for one or more local-to-remote sync pairs.
- First-run setup flow with Cotton server probing, browser app-code approval sign-in, password/TOTP sign-in as an explicit alternate, remembered server URL and username, and an explicit add-folder wizard for choosing the first sync pair.
- First-run windows default to the dark Cotton theme, with System/Light/Dark theme switching still available in Settings.
- Dashboard with global status, per-folder status/current operation, current progress, activity history, action-required errors, conflict list, and direct sync-folder management.
- Action-required sync failures use a consistent dashboard state and preserve the concrete reason in the error panel instead of mixing a generic failure state with an up-to-date progress message; add-folder/settings overlays hide background dashboard chrome so wizard errors stay readable, and missing desktop sync API errors block add-folder actions until the server check is resolved.
- Direct desktop command failures from Cotton API quota and upload-limit responses are normalized into action-required messages instead of raw HTTP failure text.
- Self-test detects missing desktop sync API capability even when another failed check is shown first.
- Diagnostics export no longer hides an existing action-required server capability error or re-enables add-folder actions against an unsupported backend.
- Diagnostics self-test failure rows use normalized user-readable details instead of raw local database/exception text, while trace logs keep the technical exception.
- Disk-full and local file-permission sync failures are normalized into readable dashboard and notification messages instead of raw OS exception text.
- Local sync-state SQLite table failures are normalized into a readable diagnostics/restart prompt instead of showing raw `no such table` errors in setup or dashboard flows.
- Folder management supports add, rename, enable/disable, open local folder, and remove with explicit confirmation.
- Folder management controls expand inline inside the folder row instead of duplicating the selected folder as a second editor row.
- Expanded folder controls keep enough room for the next configured folder in the compact dashboard instead of clipping the list immediately.
- Settings uses readable compact tabs: Account, Startup, Options, and Diagnostics.
- Continuous sync uses local filesystem watcher triggers, SignalR wake-up events, durable change-feed catch-up, and periodic reconciliation as a safety fallback.
- Conflict handling preserves both versions and exposes conflict entries in the desktop UI.
- Local sync state and desktop settings use EF Core SQLite. Normal app state does not use raw SQL commands.
- Sync-state and app-settings SQLite migrations are serialized per database path so multiple startup runners do not race the same database.
- Stale partial download cleanup leaves trace warnings when locked or permission-denied `.download` files cannot be removed, keeping diagnostics useful after crash recovery.
- Token storage is abstracted and release-gated: Windows DPAPI and Linux Secret Service are treated as release-secure; restricted-file storage fails self-test.
- Diagnostics include structured logging, log rotation, self-test, sync-state cursor-store verification with the concrete database path, support bundle export, and secret redaction.
- Notifications and recent activity use the same user-readable action-required error messages as the dashboard instead of leaking raw backend/JSON parser failures, and repeated unchanged status errors are deduplicated in Activity.
- CLI recovery support includes state summary, one-shot sync commands for headless validation, app-code browser approval login for console sessions, and shared server URL normalization for absolute URLs and bare Cotton hosts.
- Tray lifecycle is implemented for Windows. Linux currently uses normal window lifecycle because tray support varies by desktop environment.
- Single-instance startup now raises the existing desktop window when the app is launched again.
- Windows installer/uninstaller detection now uses an Inno Setup `AppMutex` backed by a Windows-only runtime mutex held by the desktop app, so install/uninstall can detect that Cotton Sync is still running before replacing or removing installed files.
- Windows installer Start Menu group includes both launch and uninstall shortcuts; launch shortcuts carry the same AppUserModelID as the running desktop process, CI verifies shortcut identity after install and upgrade, and removes them after uninstall.
- Linux `.deb` uninstall now removes stale Cotton Sync XDG autostart entries that point at the packaged `/opt/cotton-sync/Cotton.Sync.Desktop` executable.

## Artifacts

- Windows installer: `cotton-sync-desktop-win-x64-setup.exe`.
- Windows portable archive: `cotton-sync-desktop-win-x64.zip`.
- Windows portable archive fallback: `cotton-sync-desktop-win-x64.tar.gz`.
- Linux portable archive: `cotton-sync-desktop-linux-x64.tar.gz`.
- Linux Debian package: `cotton-sync-desktop-linux-x64.deb`.
- Release asset checksums: `release-artifact-checksums.sha256`.
- Checksums are generated and verified for desktop publish/package outputs, and the release workflow generates uploaded release asset checksums; final release artifact checksum verification must be rerun before release.

## Verification Already Exercised

- Current branch split audit against local `develop` shows no backend/shared/main-web delta; `develop..HEAD` under `src/Cotton.Server`, `src/Cotton.Shared`, `src/cotton.client`, `.github/workflows`, and `src/Cotton.sln` is limited to the desktop sync workflow and solution wiring.
- Previous full local solution Release test/build passes have covered the whole repository after desktop packaging, UI hardening, sync-state, and backend sync-endpoint integration. Keep this as historical evidence only; the final release gate still needs one batched clean Release run.
- Current 2026-06-09 release-gate refreshes: SDK Release tests passed 52/52, sync core Release tests passed 181/181, focused server sync endpoint integration tests passed 25/25, desktop Release build passed with 0 warnings, and CLI Release build passed with 0 warnings.
- App-code browser authentication is implemented through shared DTOs, SDK, app layer, CLI, and desktop UI. Focused Release evidence covers transient polling retry, transient current-user lookup retry after approval, CLI browser-login slices, and desktop browser sign-in/cancel ViewModel slices. Manual Windows desktop approval remains open.
- Desktop packaging metadata tests cover publish profiles, clean publish-directory behavior, app icon metadata, Linux `.desktop` metadata, `.deb` packaging script, reusable Linux/Windows diagnostics export smoke scripts, Linux package smoke wiring, reusable Linux GUI screenshot matrix smoke with deterministic first-run/sign-in-error/empty-dashboard/add-folder/dashboard/folder-controls/progress/settings/settings-diagnostics/error/conflict visual-smoke states, Linux archive/installed diagnostics export smoke wiring, Linux `.deb` install/upgrade smoke wiring, Windows CI smoke, Windows `.zip` artifact upload/self-test/diagnostics smoke, Windows installer script/install/diagnostics/upgrade smoke wiring, Windows shortcut AppUserModelID verification, running-app install/uninstall detection metadata, and release artifact checksum generation.
- Current 2026-06-09 Linux publish smoke from the Release `linux-x64` publish output passed checksum verification and published apphost self-test against `app.cottoncloud.dev`, including release-secure Linux Secret Service through `secret-tool`, `notify-send` support with app name `Cotton Sync`, server identity `Cotton Cloud`, file watcher availability, and desktop sync change-feed capability status.
- Current 2026-06-09 Linux diagnostics export smoke passed from the published apphost and verified the diagnostics zip plus `diagnostics.json` data-path metadata for app data, preferences DB, sync-state DB, and token-store paths.
- Current 2026-06-09 Linux screenshot matrix capture passed from the published apphost under Xvfb/DBus for first-run, sign-in-error, empty-dashboard, add-folder, dashboard, folder-controls, progress, settings, settings-diagnostics, error, and conflict states. The script verified app-window dimensions, nonblank frames, and runtime health; manual visual review and Windows screenshots remain open.
- Sync core scale evidence includes explicit Linux manual no-op smokes up to 50k matching files and initial-upload smokes up to 30k small files, plus memory-pressure reductions in activity retention, baseline loading, scan lookups, reconciliation keys, delete planning, progress sampling, and dashboard snapshot loading. Windows packaged GUI 30k/50k runs remain open.
- CLI soak tooling records elapsed time, CPU usage, process and managed memory growth/peaks, iteration timing, sync errors, activity totals, and final convergence. Actual 24-hour one-client and two-client soak runs remain open.

## Known Release Gates Still Open

- Clean Windows VM: install/launch, portable archive smoke, taskbar icon, tray behavior, autostart after reboot, notifications, DPAPI token storage, browser approval sign-in, password/TOTP sign-in, sync, diagnostics export.
- Clean Linux VM: `.deb` install, portable archive smoke, Secret Service token storage, browser approval sign-in, autostart after login, notifications, normal-window lifecycle, tested desktop environment record.
- Full screenshot review: onboarding, dashboard, settings, conflict state, and error/action-required state.
- Long-running soak: at least 24 hours with one client and 24 hours with two clients.
- True clean-machine package uninstall and upgrade behavior.
- Final end-to-end release matrix rerun with the current backend and desktop artifact.
- Final release branch diff review and final review of `release-artifact-checksums.sha256` against uploaded artifacts.

## Explicit Non-Goals For This Release

- Virtual files/placeholders.
- Selective sync.
- Bandwidth limits.
- Multiple accounts.
- External-provider OAuth/PKCE login beyond the Cotton app-code browser approval flow.
- Auto-update implementation.
- File manager overlay icons.
- macOS and mobile support.
