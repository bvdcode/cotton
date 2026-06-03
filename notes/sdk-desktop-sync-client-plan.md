# Cotton Sync Desktop Release Plan

This is the ground-up release plan for the Cotton Cloud desktop synchronization client. It replaces the previous prototype/foundation plan and should be treated as the source of truth for building a real releasable application, not a demo.

All implementation checkboxes start unchecked. A task can be marked done only after its verification item is completed and recorded in this file or in the linked task notes.

## Product Target

Cotton Sync Desktop is a polished Windows/Linux desktop application that behaves like a real cloud-folder sync client:

- [ ] The user can install and launch the application without developer tools.
- [ ] The user can sign in with Cotton credentials and TOTP when required.
- [ ] The user can configure one or more sync pairs: local folder to remote Cotton folder.
- [ ] The application can start with the operating system.
- [ ] The application runs continuously in the background and is controlled from the tray.
- [ ] Local changes are uploaded automatically.
- [ ] Remote changes are downloaded automatically.
- [ ] Conflicts preserve both versions and are visible to the user.
- [ ] Dangerous deletes are blocked before data loss.
- [ ] The application exposes clear status, progress, diagnostics, and logs.
- [ ] Release packages are built and smoke-tested on clean Windows and Linux machines.

## Non-Negotiable Decisions

- [ ] Use the existing .NET stack: `Cotton.Sync`, `Cotton.Sdk`, EF Core SQLite, and Avalonia.
- [ ] Keep synchronization logic out of desktop UI code.
- [ ] Use EF Core SQLite for durable local state and settings. Do not use raw SQL for normal app state.
- [ ] Treat WebDAV as interoperability, not as the native desktop sync engine.
- [ ] Treat SignalR as a wake-up signal, not as the durable source of remote truth.
- [ ] Implement full-mirror folder sync first. Virtual files/placeholders are a later platform feature.
- [ ] Support multiple sync pairs in the product model from the start.
- [ ] Do not support overlapping local sync roots.
- [ ] Do not ship insecure token storage in the final release.
- [ ] Do not mark release readiness until clean-machine install and sync smoke tests pass.

## Architecture Overview

Target layers:

- [ ] `Cotton.Sdk`: typed API client, auth, token refresh, chunks, files, nodes, sync changes, SignalR event connection.
- [ ] `Cotton.Sync`: headless sync core, state store, local scanner/writer, remote crawler, reconciler, conflict handling, delete guards.
- [ ] `Cotton.Sync.App`: application layer for sync-pair settings, auth session, continuous supervisor, status/activity streams, platform command orchestration.
- [ ] `Cotton.Sync.Desktop`: Avalonia shell only. It displays state and sends commands to `Cotton.Sync.App`.
- [ ] `Cotton.Sync.Cli`: headless validation and recovery tool using the same app/core services.
- [ ] Platform adapters: autostart, tray, notifications, open folder/browser, secure token store, single instance.

## Phase 0 - Ground Truth And Branch Hygiene

- [x] Start from `develop` and create a dedicated feature branch.
  Verification 2026-06-03: branch `feature/desktop-sync-client`; `git status --short --branch` reported `## feature/desktop-sync-client...origin/feature/desktop-sync-client [ahead 7]`.
- [ ] Review the current `Cotton.Sync`, `Cotton.Sdk`, `Cotton.Sync.Cli`, and `Cotton.Sync.Desktop` code.
  Verification: record the existing classes that will be reused and the classes that must be replaced.
- [ ] Review current server file/node/chunk/auth/SignalR endpoints.
  Verification: record concrete endpoint names and DTOs. Do not guess model fields.
- [ ] Review existing tests before changing architecture.
  Verification: record current passing/failing test baseline.
- [ ] Confirm local dev server startup path and test credentials strategy.
  Verification: record the command and whether public-instance auto-create is used.

## Phase 1 - Release-Grade App Model

- [x] Add a `SyncPairSettings` domain model.
  Required fields: id, display name, local root path, remote root node id, remote display path, enabled flag, sync mode, created/updated timestamps.
  Verification 2026-06-03: implemented in `src/Cotton.Sync.App/SyncPairs/SyncPairSettings.cs`; covered by `Cotton.Sync.App.Tests`.
- [x] Add sync mode values.
  Required modes: full mirror now, virtual files placeholder reserved for later.
  Verification 2026-06-03: implemented in `src/Cotton.Sync.App/SyncPairs/SyncPairMode.cs`; unsupported placeholder mode is rejected by tests.
- [x] Add sync pair validation.
  Required checks: local path exists or can be created, local roots do not overlap, remote target is resolvable, mode is supported.
  Verification 2026-06-03: structural validator plus async prerequisite validator implemented; tests cover overlap, unsupported mode, local root creation/unavailable path, remote prerequisite errors, and save rejection without persistence.
- [x] Add a settings store through EF Core SQLite.
  Required stores: sync-pair settings, app preferences, remembered server URL, startup preferences.
  Verification 2026-06-03: `SqliteSyncPairSettingsStore` and `SqliteAppPreferencesStore` use EF Core SQLite through shared `SyncAppDbContext`; tests cover sync-pair and app-preferences roundtrip.
- [x] Keep token storage behind `ITokenStore`; do not couple it to app settings.
  Verification 2026-06-03: auth uses SDK `ICottonTokenStore` through `ICottonAuthClient`; app preferences store has no token fields.
- [x] Add tests for settings roundtrip, pair enable/disable, and pair deletion.
  Verification 2026-06-03: `SqliteSyncPairSettingsStoreTests` cover roundtrip, update with `IsEnabled = false`, and deletion.
- [x] Add tests for overlapping local roots on Windows-style and Linux-style paths.
  Verification 2026-06-03: `SyncPairSettingsValidatorTests` cover Windows case-insensitive nested/equal roots and Unix nested roots.
- [x] Add tests for unsupported virtual-file mode returning a deliberate not-implemented result.
  Verification 2026-06-03: `Validate_RejectsVirtualFilesPlaceholderMode` passes.
- [x] Build the solution.
  Verification: `dotnet build src/Cotton.sln --configuration Release`.
  Verification 2026-06-03: command passed with 0 warnings and 0 errors after Phase 1 app-model slices.

## Phase 2 - Application Layer

- [x] Create `Cotton.Sync.App` or an equivalent application-layer project.
  Verification 2026-06-03: `src/Cotton.Sync.App/Cotton.Sync.App.csproj` and `src/Cotton.Sync.App.Tests/Cotton.Sync.App.Tests.csproj` are in the solution; Release build passes.
- [ ] Move desktop orchestration out of `MainWindow`.
- [ ] Add `SyncApplicationService` for high-level commands.
  Commands: sign in, sign out, add sync pair, update sync pair, remove sync pair, sync now, pause, resume, open folder, open web.
- [ ] Add `SyncSupervisor` for continuous background operation.
- [ ] Add `SyncPairRunner` for per-pair state.
  States: disabled, idle, scanning, syncing, paused, offline, conflict, error.
- [ ] Add `IAppStatusPublisher` or equivalent observable status stream.
- [ ] Add activity history model.
  Required activity types: uploaded, downloaded, deleted local, deleted remote, conflict, skipped, error, warning.
- [ ] Add cancellation and shutdown flow.
- [ ] Add tests for supervisor state transitions.
- [ ] Add tests for pause/resume and app shutdown.
- [ ] Add tests proving UI can use the app layer without directly constructing `SyncEngine`.
- [ ] Build the solution.

## Phase 3 - Backend Sync Change Feed

This phase is required for release-grade remote sync. SignalR alone is not enough because events can be missed while the client is offline.

- [x] Add a durable server change cursor/revision model.
  Verification: commit `Add durable sync change feed`; EF migration `20260603165614_AddSyncChanges` adds `sync_changes` with monotonic `revision`.
- [x] Record file and folder mutations in order.
  Required events: file created, file content updated, file renamed, file moved, file deleted, file restored, folder created, folder renamed, folder moved, folder deleted, folder restored.
  Verification: commit `Add durable sync change feed`; `EventNotificationService` records the required event kinds through `ISyncChangeRecorder`. Focused integration coverage exists for folder create, file create, file rename, and file delete ordering; the broader per-kind matrix remains open below.
- [x] Add `GET /api/v1/sync/changes?since=<cursor>`.
  Verification: commit `Add durable sync change feed`; endpoint implemented in `SyncController` and exercised by `SyncChangesEndpointsTests`.
- [x] Include enough data for the client to update or invalidate remote state.
  Required data: event id/revision, event kind, entity id, parent id, path or enough identifiers to resolve path, file manifest id, content hash, ETag/version when relevant, deletion/restoration markers.
  Verification: commit `Add durable sync change feed`; `SyncChangeDto` returns cursor, kind, layout/node/file ids, current/previous parent ids, manifest id, original file id, name, content hash, ETag, size, and created timestamp.
- [ ] Add retention behavior and expired-cursor response.
- [x] Add SDK sync-change client.
  Verification: commit `Add durable sync change feed`; `ICottonSyncClient.GetChangesAsync` added to `ICottonCloudClient.Sync`.
- [ ] Add server integration tests for ordered create/update/delete/move/rename changes.
- [ ] Add server integration test for missed SignalR recovery through changes API.
- [x] Add SDK tests for changes request and response parsing.
  Verification: `dotnet test src/Cotton.Sdk.Tests/Cotton.Sdk.Tests.csproj --configuration Release --no-restore` passed 11/11 after commit `Add durable sync change feed`.
- [x] Add local smoke check: create remote change, fetch it through changes API.
  Verification: `dotnet test src/Cotton.Server.IntegrationTests/Cotton.Server.IntegrationTests.csproj --configuration Release --no-restore --filter FullyQualifiedName~SyncChangesEndpointsTests` passed 1/1 after commit `Add durable sync change feed`.
- [x] Build and test server plus SDK.
  Verification: `dotnet build src/Cotton.sln --configuration Release` passed after commit `Add durable sync change feed`; known NU1903 warnings remain for Avalonia/Tmds.DBus.Protocol.

## Phase 4 - Optimistic Concurrency And Safe Remote Operations

- [ ] Add expected version/ETag support for file content update.
- [ ] Add expected version/ETag support for file delete where possible.
- [ ] Add expected version/ETag support for move/rename where possible.
- [ ] Return explicit conflict responses instead of silent overwrite.
- [ ] Update SDK methods to pass expected remote state.
- [ ] Update sync core to preserve both versions when remote state changed after crawl.
- [ ] Add tests for stale upload losing the concurrency race.
- [ ] Add tests for stale delete losing the concurrency race.
- [ ] Add tests for stale rename/move conflict.
- [ ] Build and run server, SDK, and sync tests.

## Phase 5 - Sync Core Hardening

- [ ] Keep `RunOnceAsync` as the deterministic reconciliation primitive.
- [ ] Add or refine remote snapshot representation for change-feed updates.
- [ ] Add durable operation intent tracking if needed for crash recovery.
- [ ] Ensure downloads always use temp file plus atomic replace.
- [ ] Ensure baseline updates happen only after successful local and remote operation.
- [ ] Add local delete strategy.
  Target: safe delete or recycle/trash where available; direct delete only when deliberately accepted.
- [ ] Add remote delete strategy.
  Target: trash/soft-delete by default.
- [ ] Add mass-delete guard per sync pair.
- [ ] Add debounce for files still being written.
- [ ] Add handling for locked files.
- [ ] Add ignore patterns for common temporary files.
- [ ] Add symlink/reparse-point policy.
- [ ] Add Windows path validation.
  Required checks: reserved names, invalid characters, path length, case collisions.
- [ ] Add Linux path validation.
  Required checks: case-sensitive collisions with remote case-insensitive model assumptions, file permissions.
- [ ] Add Unicode normalization policy.
- [ ] Add tests for every local/remote/base create-update-delete combination.
- [ ] Add tests for conflict naming uniqueness.
- [ ] Add tests for mass delete guard.
- [ ] Add tests for locked or unreadable files.
- [ ] Add tests for Windows reserved file names.
- [ ] Add tests for case conflicts.
- [ ] Add tests for crash during download.
- [ ] Add tests for crash after remote upload before baseline update.
- [ ] Run full sync test suite.

## Phase 6 - Continuous Sync

- [ ] Add local file watcher abstraction.
- [ ] Implement Windows/Linux watcher adapter with fallback periodic scan.
- [ ] Add watcher debounce and event coalescing.
- [ ] Add SignalR remote event listener through SDK.
- [ ] Connect SignalR events to changes API fetch.
- [ ] Add periodic reconcile as safety fallback.
- [ ] Add offline detection and backoff.
- [ ] Add retry policy with bounded attempts and user-visible error state.
- [ ] Add per-pair queue so repeated triggers collapse into one sync pass.
- [ ] Add tests for local watcher event coalescing.
- [ ] Add tests for remote SignalR trigger causing changes fetch.
- [ ] Add tests for offline to online recovery.
- [ ] Add two-client integration test against local dev server.
- [ ] Run continuous sync soak test for at least 2 hours before this phase is considered done.

## Phase 7 - Authentication And Token Storage

- [ ] Keep password/TOTP login as the first implemented auth flow.
- [ ] Add `IAuthFlow` abstraction.
  Required implementations: password flow now, browser flow placeholder later.
- [ ] Add named-device metadata where server supports it.
- [ ] Add secure token store abstraction.
- [ ] Implement Windows secure token store.
  Preferred target: Windows Credential Manager or DPAPI-backed per-user storage.
- [ ] Implement Linux secure token store.
  Preferred target: Secret Service/libsecret where available.
- [ ] Add explicit fallback policy for Linux environments without a secret service.
  Release decision must be explicit: block login, prompt user, or encrypted local store.
- [ ] Ensure logout clears tokens and stops all sync runners.
- [ ] Handle refresh-token revocation and session-revoked SignalR event.
- [ ] Add tests for login, refresh, logout, token clear, and invalid saved token.
- [ ] Add manual verification on Windows secure storage.
- [ ] Add manual verification on Linux secure storage.

## Phase 8 - Desktop UX And Visual Design

- [ ] Replace prototype screen with release-grade app structure.
- [ ] Add onboarding flow.
  Steps: welcome, sign in, optional autostart, add first sync pair, initial sync, finished state.
- [ ] Add main dashboard.
  Required content: global status, sync-pair list, per-pair status, current progress, recent activity.
- [ ] Add sync-pair editor.
  Required fields: local folder picker, remote folder picker/resolver, display name, enabled flag, sync mode.
- [ ] Add remote folder picker or searchable remote folder selector.
- [ ] Add settings screen.
  Required sections: account, sync pairs, startup, notifications, diagnostics, about.
- [ ] Add conflict/error screen.
  Required behavior: show conflict files, action-required errors, retry/sync-now command.
- [ ] Add not-implemented UX for reserved future modes only in development builds or behind a feature flag.
- [ ] Add polished empty states.
- [ ] Add dark/light theme support.
- [ ] Add responsive layout for minimum window size.
- [ ] Ensure no user-visible strings are hardcoded if localization is required for desktop release.
- [ ] Run Avalonia desktop build.
- [ ] Run manual UI walkthrough on Linux.
- [ ] Run manual UI walkthrough on Windows.
- [ ] Capture screenshots for onboarding, dashboard, settings, conflict state, and error state.
- [ ] Review screenshots for clipping, overlap, weak hierarchy, and inconsistent spacing.
- [ ] Run visual QA checklist from `notes/visual-qa-checklist.md` or replace it with an updated desktop checklist.

## Phase 9 - Tray, Autostart, Notifications, And Lifecycle

- [ ] Add single-instance enforcement.
- [ ] Add close-to-tray behavior.
- [ ] Add explicit quit behavior.
- [ ] Add tray icon states.
  Required states: idle, syncing, paused, offline, error.
- [ ] Add tray menu.
  Required commands: show app, open folder, open web, sync now, pause/resume, settings, quit.
- [ ] Add Windows autostart adapter.
  Acceptable options: installer-managed startup, registry Run entry, startup shortcut, or scheduled task.
- [ ] Add Linux autostart adapter.
  Target: XDG autostart `.desktop`.
- [ ] Add notification adapter.
  Required notifications: initial sync complete, conflict created, action-required error.
- [ ] Add tests for single-instance lock where practical.
- [ ] Add manual Windows verification: autostart after reboot, tray behavior, notifications.
- [ ] Add manual Linux verification: autostart after login, tray behavior, notification behavior.
- [ ] Document Linux tray limitations and actual tested desktop environments.

## Phase 10 - Diagnostics And Supportability

- [ ] Add structured logging for app, sync, SDK, and platform adapters.
- [ ] Add log rotation.
- [ ] Add diagnostics screen.
  Required fields: app version, server URL, account, sync pair ids, local paths, remote ids, last sync time, current cursor, last error.
- [ ] Add export diagnostics bundle command.
- [ ] Redact tokens and secrets from logs and diagnostics.
- [ ] Add self-test command.
  Required checks: server reachability, auth state, local folder access, remote folder access, SQLite state access, watcher availability.
- [ ] Add tests for redaction.
- [ ] Add manual diagnostics export verification.

## Phase 11 - Packaging And Installers

- [ ] Define release artifact matrix.
  Required artifacts: Windows installer, Windows portable archive, Linux AppImage or archive, Linux `.deb` if feasible.
- [ ] Configure self-contained publish for Windows x64.
- [ ] Configure self-contained publish for Linux x64.
- [ ] Add app icon and metadata for packaged apps.
- [ ] Add version/about metadata.
- [ ] Add checksum generation.
- [ ] Add signing plan.
  Windows code signing can be deferred only with an explicit release risk decision.
- [ ] Add CI workflow for build, test, publish, package, and artifact upload.
- [ ] Add smoke launch command or self-test command for packaged app.
- [ ] Test Windows installer on a clean VM.
- [ ] Test Windows portable archive on a clean VM.
- [ ] Test Linux package/archive on a clean VM.
- [ ] Test uninstall behavior.
- [ ] Test upgrade over previous build.

## Phase 12 - End-To-End Test Matrix

- [ ] One client: local create uploads to remote.
- [ ] One client: local update uploads to remote.
- [ ] One client: local delete moves remote item to trash or configured delete behavior.
- [ ] One client: remote create downloads locally.
- [ ] One client: remote update downloads locally.
- [ ] One client: remote delete applies locally through safe delete behavior.
- [ ] Two clients: local change on client A reaches client B.
- [ ] Two clients: simultaneous edit creates conflict and preserves both versions.
- [ ] Two clients: rename/move propagates correctly.
- [ ] Offline: local changes accumulate and sync after reconnect.
- [ ] Offline: remote changes accumulate and sync after reconnect.
- [ ] Server restart during sync recovers.
- [ ] Client crash during sync recovers.
- [ ] Disk full shows action-required error.
- [ ] Permission denied shows action-required error.
- [ ] Quota exceeded shows action-required error.
- [ ] Large file upload and download complete.
- [ ] Many small files complete.
- [ ] Deep nested paths complete or fail with clear path error.
- [ ] Windows reserved names are blocked or mapped with clear UX.
- [ ] Case conflict is detected and explained.
- [ ] Unicode names sync consistently.
- [ ] Temporary files are ignored.
- [ ] Locked files retry and eventually sync after unlock.

## Phase 13 - Performance And Soak

- [ ] Define performance targets.
  Suggested targets: initial scan throughput, no-op sync time for 1k/10k files, memory ceiling, UI responsiveness.
- [ ] Add benchmark or measured smoke for no-op sync.
- [ ] Add measured smoke for 1,000 small files.
- [ ] Add measured smoke for one large file.
- [ ] Add measured smoke for remote changes catch-up through cursor.
- [ ] Run 24-hour soak test with one client.
- [ ] Run 24-hour soak test with two clients.
- [ ] Record memory growth, CPU usage, sync errors, and final convergence.

## Phase 14 - Release Readiness Gate

Release can be considered only when every item in this section is checked.

- [ ] Full solution Release build passes.
- [ ] SDK tests pass.
- [ ] Sync core tests pass.
- [ ] Server integration tests for sync endpoints pass.
- [ ] Desktop build passes.
- [ ] CLI build passes.
- [ ] Packaged app smoke passes on clean Windows VM.
- [ ] Packaged app smoke passes on clean Linux VM.
- [ ] End-to-end sync matrix passes.
- [ ] UI screenshot review passes.
- [ ] Tray/autostart lifecycle verification passes.
- [ ] Secure token storage verification passes.
- [ ] Diagnostics export verification passes.
- [ ] No known data-loss bugs remain open.
- [ ] No known crash-on-start bugs remain open.
- [ ] No known broken-login bugs remain open.
- [ ] No known package-install failure remains open.
- [ ] Release notes are written.
- [ ] Checksums are generated.
- [ ] Final release branch diff is reviewed.

## Future Platform Features

These are intentionally outside the first releasable full-mirror sync product.

- [ ] Windows virtual files/placeholders through Cloud Files API.
- [ ] Linux virtual files strategy through FUSE or desktop portal research.
- [ ] Selective sync.
- [ ] Bandwidth limits.
- [ ] Multiple accounts.
- [ ] Browser-based OAuth/PKCE desktop login.
- [ ] Auto-update implementation.
- [ ] File manager overlay icons.
- [ ] macOS support.
- [ ] Mobile support.

## Working Rules For This Plan

- [ ] Prefer finishing one phase slice with tests over spreading partial work across many phases.
- [ ] Keep commits small and phase-scoped.
- [ ] Update this plan whenever scope changes.
- [ ] Add a verification note next to any checkbox before marking it complete.
- [ ] Do not remove hard tasks from the plan just because they are uncomfortable.
- [ ] Do not ship prototype-only shortcuts in the final release.
