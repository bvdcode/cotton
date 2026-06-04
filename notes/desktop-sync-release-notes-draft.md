# Cotton Sync Desktop 0.1.0-dev Draft Release Notes

Draft status: not release-ready. This document records the current desktop-sync release story and the verification still required before a public or internal release can be called complete.

## Highlights

- Desktop sync client built on the existing .NET stack: `Cotton.Sdk`, `Cotton.Sync`, `Cotton.Sync.App`, EF Core SQLite, and Avalonia.
- Full-mirror sync for one or more local-to-remote sync pairs.
- First-run setup flow with Cotton server probing, password/TOTP sign-in, remembered server URL and username, and automatic add-folder wizard for the first sync pair.
- First-run windows default to the dark Cotton theme, with System/Light/Dark theme switching still available in Settings.
- Dashboard with global status, per-folder status, current progress, activity history, action-required errors, conflict list, and direct sync-folder management.
- Action-required sync failures use a consistent dashboard state and preserve the concrete reason in the error panel instead of mixing a generic failure state with an up-to-date progress message.
- Folder management supports add, rename, enable/disable, open local folder, and remove with explicit confirmation.
- Continuous sync uses local filesystem watcher triggers, SignalR wake-up events, durable change-feed catch-up, and periodic reconciliation as a safety fallback.
- Conflict handling preserves both versions and exposes conflict entries in the desktop UI.
- Local sync state and desktop settings use EF Core SQLite. Normal app state does not use raw SQL commands.
- Sync-state and app-settings SQLite migrations are serialized per database path so multiple startup runners do not race the same database.
- Token storage is abstracted and release-gated: Windows DPAPI and Linux Secret Service are treated as release-secure; restricted-file storage fails self-test.
- Diagnostics include structured logging, log rotation, self-test, support bundle export, and secret redaction.
- CLI recovery support includes state summary and one-shot sync commands for headless validation, including shared server URL normalization for absolute URLs and bare Cotton hosts.
- Tray lifecycle is implemented for Windows. Linux currently uses normal window lifecycle because tray support varies by desktop environment.
- Single-instance startup now raises the existing desktop window when the app is launched again.

## Artifacts

- Windows installer: `cotton-sync-desktop-win-x64-setup.exe`.
- Windows portable archive: `cotton-sync-desktop-win-x64.zip`.
- Windows portable archive fallback: `cotton-sync-desktop-win-x64.tar.gz`.
- Linux portable archive: `cotton-sync-desktop-linux-x64.tar.gz`.
- Linux Debian package: `cotton-sync-desktop-linux-x64.deb`.
- Release asset checksums: `release-artifact-checksums.sha256`.
- Checksums are generated and verified for desktop publish/package outputs, and the release workflow generates uploaded release asset checksums; final release artifact checksum verification must be rerun before release.

## Verification Already Exercised

- Full local `dotnet test src/Cotton.sln --configuration Release --no-restore` has passed after current `develop` integration, including desktop 254/254 and server integration 365/365.
- Desktop tests have passed locally, most recently `Cotton.Sync.Desktop.Tests` 255/255.
- Server integration tests have passed locally, most recently `Cotton.Server.IntegrationTests` 365/365.
- CLI one-shot sync has been smoke-tested against the integration-test server and covered in CLI tests with fake Cotton HTTP responses, verifying SDK file/folder upload requests and SQLite baseline creation.
- Desktop packaging metadata tests cover publish profiles, clean publish-directory behavior, app icon metadata, Linux `.desktop` metadata, `.deb` packaging script, reusable Linux/Windows diagnostics export smoke scripts, Linux package smoke wiring, reusable Linux GUI screenshot matrix smoke with deterministic sign-in-error/add-folder/dashboard/settings/settings-diagnostics/error/conflict visual-smoke states, Linux archive/installed diagnostics export smoke wiring, Linux `.deb` install/upgrade smoke wiring, Windows CI smoke, Windows `.zip` artifact upload/self-test/diagnostics smoke, Windows installer script/install/diagnostics/upgrade smoke wiring, and release artifact checksum generation.
- Local Linux publish succeeded.
- Local Windows publish succeeded from Linux cross-publish.
- Local `.deb` build succeeded through `dpkg-deb`; extracted package layout starts the desktop apphost and runs self-test up to the expected local token-storage failure.
- Local Windows `.zip` build succeeded through Python `zipfile`; archive contains `Cotton.Sync.Desktop.exe` and desktop icon assets.
- CI contains Linux publish self-test, Linux GUI screenshot capture under Xvfb with auto-detected or explicitly overridden capture size plus PNG dimension and nonblank-frame checks for first-run plus sign-in-error/add-folder/dashboard/settings/settings-diagnostics/error/conflict visual-smoke states, including visible diagnostics self-test result rows, Linux archive/deb extraction self-tests, Linux archive and installed/upgrade-installed `.deb` diagnostics export smoke, Linux `.deb` install/uninstall/upgrade self-tests with Secret Service setup, Windows apphost self-test, associated-icon verification for published/zip/installed/upgraded apphosts, Windows zip extraction self-test plus diagnostics export smoke, Windows installer install/uninstall/diagnostics/upgrade self-tests, and Windows installer build/upload on `windows-latest`.

## Known Release Gates Still Open

- Clean Windows VM: install/launch, portable archive smoke, taskbar icon, tray behavior, autostart after reboot, notifications, DPAPI token storage, sign-in, sync, diagnostics export.
- Clean Linux VM: `.deb` install, portable archive smoke, Secret Service token storage, autostart after login, notifications, normal-window lifecycle, tested desktop environment record.
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
- Browser-based OAuth/PKCE desktop login.
- Auto-update implementation.
- File manager overlay icons.
- macOS and mobile support.
