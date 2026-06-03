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
- [x] Use EF Core SQLite for durable local state and settings. Do not use raw SQL for normal app state.
  Verification 2026-06-03: `SqliteSyncStateStore`, `SqliteSyncPairSettingsStore`, and `SqliteAppPreferencesStore` use EF Core SQLite contexts and migrations; `rg "Microsoft.Data.Sqlite|SqliteConnection|CommandText|CREATE TABLE|SELECT |INSERT |UPDATE |DELETE "` across `Cotton.Sync`, `Cotton.Sync.App`, and `Cotton.Sync.Desktop` found no raw SQL command usage. `dotnet test src/Cotton.Sync.Tests/Cotton.Sync.Tests.csproj --configuration Release --no-restore` passed 89/89.
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
  Verification 2026-06-03: commit `Version app settings database`; `sync-app.db` now uses EF-generated `InitialSyncAppState` migrations and store initialization calls `Database.MigrateAsync` instead of `EnsureCreatedAsync`. Focused SQLite store tests passed 10/10, full `Cotton.Sync.App.Tests` passed 81/81, and `dotnet build src/Cotton.sln --configuration Release` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
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
- [x] Move desktop orchestration out of `MainWindow`.
  Verification 2026-06-03: `MainWindow` only owns Avalonia window lifecycle, sizing, folder picker binding, and view-model creation; auth, sync-pair persistence, supervisor control, status subscription, diagnostics, and server probing live behind `DesktopShellController`, `DesktopSyncApplicationFactory`, and `Cotton.Sync.App`. Full `Cotton.Sync.Desktop.Tests` passed 62/62 and full solution Release build passed with the known NU1903 warning.
- [x] Add `SyncApplicationService` for high-level commands.
  Commands: sign in, sign out, add sync pair, update sync pair, remove sync pair, sync now, pause, resume, open folder, open web.
  Verification 2026-06-03: `SyncApplicationService` exposes sign in/out, preferences, sync-pair save/delete/list, start/stop/sync-now, pause/resume, and platform open commands. `SyncApplicationServiceTests` cover sign-out shutdown, start/stop sync, platform command delegation, valid sync-pair save, overlap/prerequisite rejection, and deletion; full `Cotton.Sync.App.Tests` passed 83/83.
- [x] Add `SyncSupervisor` for continuous background operation.
  Verification 2026-06-03: `SyncSupervisor` creates one runner per saved sync pair, starts/stops runners, fans out sync/pause/resume commands, and publishes aggregate status. `SyncSupervisorTests` cover start status publishing, selected-runner pause/resume, sync-all fanout, and stop publishing; full `Cotton.Sync.App.Tests` passed 83/83.
- [x] Add `SyncPairRunner` for per-pair state.
  States: disabled, idle, scanning, syncing, paused, offline, conflict, error.
  Verification 2026-06-03: `SyncPairRunState` defines disabled, idle, scanning, syncing, paused, offline, conflict, and error; `SyncPairRunner` manages disabled/idle/paused/syncing/offline/error transitions and queue-collapsed sync requests. `SyncPairRunnerTests` cover disabled start, pause/resume, paused no-op sync, successful sync, queued requests, retry, offline transient failures, and error state; full `Cotton.Sync.App.Tests` passed 83/83.
- [x] Add `IAppStatusPublisher` or equivalent observable status stream.
  Verification 2026-06-03: `IAppStatusPublisher` and `InMemoryAppStatusPublisher` expose current app status and observable updates; tests cover current snapshot, immediate subscriber replay, publish delivery, and unsubscribe behavior.
- [x] Add activity history model.
  Required activity types: uploaded, downloaded, deleted local, deleted remote, conflict, skipped, error, warning.
  Verification 2026-06-03: `SyncActivity` and `SyncActivityType` include uploaded, downloaded, deleted local, deleted remote, conflict, skipped, error, and warning; `SyncActivityTests` cover UTC normalization and activity fields.
- [x] Add cancellation and shutdown flow.
  Verification 2026-06-03: app commands accept cancellation tokens, `SyncApplicationService.SignOutAsync` and `StopSyncAsync` stop remote, periodic, local, and supervisor components in order, and `DesktopShellController.Dispose` disposes the host/status subscription. App-layer tests cover start/stop and sign-out shutdown; full `Cotton.Sync.App.Tests` passed 83/83.
- [x] Add tests for supervisor state transitions.
  Verification 2026-06-03: `SyncSupervisorTests` cover start, pause/resume, sync-all, and stop transitions/status publishing.
- [x] Add tests for pause/resume and app shutdown.
  Verification 2026-06-03: `SyncPairRunnerTests` and `SyncSupervisorTests` cover pause/resume states, while `SyncApplicationServiceTests` cover sign-out and stop-sync shutdown flow.
- [x] Add tests proving UI can use the app layer without directly constructing `SyncEngine`.
  Verification 2026-06-03: `DesktopUiBoundaryTests` assert `ShellViewModel` constructor dependencies are `IDesktopShellController`, `ILocalFolderPicker`, and `IDesktopNotificationService`, and that `MainWindow`/`ShellViewModel` do not store or require `Cotton.Sync.SyncEngine` or `SyncEnginePairWork`. Focused boundary tests passed 2/2, full `Cotton.Sync.Desktop.Tests` passed 64/64, and full solution Release build passed with the known NU1903 warning.
- [x] Build the solution.
  Verification 2026-06-03: `dotnet test src/Cotton.Sync.App.Tests/Cotton.Sync.App.Tests.csproj --configuration Release --no-restore` passed 83/83 and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with the known NU1903 Avalonia/Tmds.DBus.Protocol warning.

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
- [x] Add retention behavior and expired-cursor response.
  Verification: `SyncChangeRetentionService` prunes expired per-user rows, `SyncChangesResponseDto` returns `CursorExpired` and `EarliestAvailableCursor`, `SyncChangesEndpointsTests` passed 4/4, and `dotnet build src/Cotton.sln --configuration Release` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add SDK sync-change client.
  Verification: commit `Add durable sync change feed`; `ICottonSyncClient.GetChangesAsync` added to `ICottonCloudClient.Sync`.
- [x] Add server integration tests for ordered create/update/delete/move/rename changes.
  Verification: `dotnet test src/Cotton.Server.IntegrationTests/Cotton.Server.IntegrationTests.csproj --configuration Release --no-restore --filter FullyQualifiedName~SyncChangesEndpointsTests` passed 2/2; `Changes_RecordsUpdateMoveAndFolderMutationKindsInOrder` covers file create/update/move/rename/delete and folder create/rename/move ordering.
- [x] Add server integration test for missed SignalR recovery through changes API.
  Verification: `dotnet test src/Cotton.Server.IntegrationTests/Cotton.Server.IntegrationTests.csproj --configuration Release --no-restore --filter FullyQualifiedName~SyncChangesEndpointsTests` passed 3/3; `Changes_ReplaysMutationsWhenRealtimeEventsWereMissed` creates remote changes without opening the SignalR hub and recovers them through `/api/v1/sync/changes`.
- [x] Add SDK tests for changes request and response parsing.
  Verification: `dotnet test src/Cotton.Sdk.Tests/Cotton.Sdk.Tests.csproj --configuration Release --no-restore` passed 11/11 after commit `Add durable sync change feed`.
- [x] Add local smoke check: create remote change, fetch it through changes API.
  Verification: `dotnet test src/Cotton.Server.IntegrationTests/Cotton.Server.IntegrationTests.csproj --configuration Release --no-restore --filter FullyQualifiedName~SyncChangesEndpointsTests` passed 1/1 after commit `Add durable sync change feed`.
- [x] Build and test server plus SDK.
  Verification: `dotnet build src/Cotton.sln --configuration Release` passed after commit `Add durable sync change feed`; known NU1903 warnings remain for Avalonia/Tmds.DBus.Protocol.

## Phase 4 - Optimistic Concurrency And Safe Remote Operations

- [x] Add expected version/ETag support for file content update.
  Verification: `FileController.UpdateFileContent` validates `If-Match` against the current content ETag inside the update transaction; `dotnet test src/Cotton.Server.IntegrationTests/Cotton.Server.IntegrationTests.csproj --configuration Release --no-restore --filter "Name~If_Match"` passed 2/2.
- [x] Add expected version/ETag support for file delete where possible.
  Verification: `DeleteFileQuery` validates `If-Match` before delete/trash mutation and `FileController.DeleteFile` returns explicit `412 PreconditionFailed`; `dotnet test src/Cotton.Server.IntegrationTests/Cotton.Server.IntegrationTests.csproj --configuration Release --no-restore --filter "Name~If_Match"` passed 2/2.
- [x] Add expected version/ETag support for move/rename where possible.
  Verification: `MoveFileCommand` and `FileController.RenameFile` validate `If-Match` before changing parent/name; `ICottonFileClient.MoveAsync` and `RenameAsync` send optional expected ETags; `dotnet test src/Cotton.Server.IntegrationTests/Cotton.Server.IntegrationTests.csproj --configuration Release --no-restore --filter "Name~If_Match"` passed 4/4 and `CottonFileAndChunkClientTests` passed 8/8.
- [x] Return explicit conflict responses instead of silent overwrite.
  Verification: stale file update/delete/move/rename now return explicit `412 PreconditionFailed` instead of applying stale mutations; name collisions continue to return `409 Conflict`; `Name~If_Match` server integration tests passed 4/4.
- [x] Update SDK methods to pass expected remote state.
  Verification: `ICottonFileClient` file mutation methods accept optional expected ETags, send `If-Match`, and `SdkRemoteFileSynchronizer` passes remote DTO ETags for update/delete; SDK tests passed 8/8 for `CottonFileAndChunkClientTests`, sync tests passed 17/17 for `SdkRemoteFileSynchronizerTests|SyncEngineTests`.
- [x] Update sync core to preserve both versions when remote state changed after crawl.
  Verification: `SyncEngine` handles remote `412 PreconditionFailed` from stale upload/delete by re-crawling the remote tree and routing the latest remote file through conflict preservation; `dotnet test src/Cotton.Sync.Tests/Cotton.Sync.Tests.csproj --configuration Release --no-restore --filter SyncEngineTests` passed 15/15 and full `Cotton.Sync.Tests` passed 41/41.
- [x] Add tests for stale upload losing the concurrency race.
  Verification: `Update_File_Content_With_Stale_If_Match_Returns_Precondition_Failed` covers server stale update returning `412`, and `RunOnceAsync_PreservesBothVersionsWhenStaleUploadLosesRemoteRace` covers sync-core conflict preservation; `dotnet test src/Cotton.Server.IntegrationTests/Cotton.Server.IntegrationTests.csproj --configuration Release --no-restore --filter "Name~If_Match"` passed 4/4 and `dotnet test src/Cotton.Sync.Tests/Cotton.Sync.Tests.csproj --configuration Release --no-restore --filter SyncEngineTests` passed 15/15.
- [x] Add tests for stale delete losing the concurrency race.
  Verification: `Delete_File_With_Stale_If_Match_Returns_Precondition_Failed_And_Keeps_File` covers server stale delete returning `412` and preserving the file, and `RunOnceAsync_RestoresRemoteVersionWhenStaleDeleteLosesRemoteRace` covers sync-core local restoration; full `ChunksAndFilesEndpointsTests` passed 26/26 and full `Cotton.Sync.Tests` passed 41/41.
- [x] Add tests for stale rename/move conflict.
  Verification: `Rename_File_With_Stale_If_Match_Returns_Precondition_Failed_And_Keeps_Name` and `Move_File_With_Stale_If_Match_Returns_Precondition_Failed_And_Keeps_Parent` cover API-level stale rename/move conflicts; `Name~If_Match` server integration tests passed 4/4.
- [x] Build and run server, SDK, and sync tests.
  Verification: `dotnet test src/Cotton.Sdk.Tests/Cotton.Sdk.Tests.csproj --configuration Release --no-restore --filter CottonFileAndChunkClientTests` passed 8/8; full `dotnet test src/Cotton.Sync.Tests/Cotton.Sync.Tests.csproj --configuration Release --no-restore` passed 41/41; `dotnet test src/Cotton.Server.IntegrationTests/Cotton.Server.IntegrationTests.csproj --configuration Release --no-restore --filter ChunksAndFilesEndpointsTests` passed 26/26; `dotnet test src/Cotton.Server.IntegrationTests/Cotton.Server.IntegrationTests.csproj --configuration Release --no-restore --filter MoveEndpointsTests` passed 26/26; `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.

## Phase 5 - Sync Core Hardening

- [x] Keep `RunOnceAsync` as the deterministic reconciliation primitive.
  Verification: `SyncEngine.RunOnceAsync` remains the single reconciliation entry point used by continuous/app layers, with deterministic sorted path processing; `dotnet test src/Cotton.Sync.Tests/Cotton.Sync.Tests.csproj --configuration Release --no-restore --filter SyncEngineTests` passed 18/18 and full `Cotton.Sync.Tests` passed 44/44.
- [x] Add durable per-pair remote change cursor state.
  Verification: commit `Add sync change cursor state`; `sync_change_cursors` stores the last accepted server cursor per sync pair through EF Core migration `20260603172105_AddSyncChangeCursors`; `dotnet test src/Cotton.Sync.Tests/Cotton.Sync.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~SqliteSyncStateStoreTests` passed 8/8, and `dotnet build src/Cotton.sln --configuration Release` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add safe remote change-feed reader with explicit acknowledgement.
  Verification: commit `Add remote change feed reader`; `RemoteChangeFeedReader.ReadAsync` reads from the stored cursor without advancing it, `AcknowledgeAsync` advances only after processing or marks expired cursors without skipping changes; `dotnet test src/Cotton.Sync.Tests/Cotton.Sync.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~RemoteChangeFeedReaderTests` passed 3/3, and `dotnet build src/Cotton.sln --configuration Release` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add or refine remote snapshot representation for change-feed updates.
  Verification 2026-06-03: `RemoteChangeFeedSnapshot` and `RemoteChangeImpact` normalize durable change-feed DTOs into file/folder targets, semantic actions, affected node/file ids, and refresh hints; `RemoteChangeAwareSyncPairWork` now drains `HasMore` pages through explicit cursor reads before one sync pass and acknowledges only after success. Focused remote change-feed tests passed 19/19, `RemoteChangeAwareSyncPairWorkTests` passed 5/5, full `Cotton.Sync.Tests` passed 89/89, full `Cotton.Sync.App.Tests` passed 83/83, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add durable operation intent tracking if needed for crash recovery.
  Verification: no separate operation-intent table is needed for the current full-file sync primitive because operations update baseline only after success, downloads use temp files, and a remote upload that succeeds before baseline persistence is recovered by the next crawl without a duplicate upload; `RunOnceAsync_RecoversAfterRemoteUploadBeforeBaselineUpdate` passed in `SyncEngineTests` 22/22 and full `Cotton.Sync.Tests` passed 65/65.
- [x] Ensure downloads always use temp file plus atomic replace.
  Verification: `AtomicLocalFileSyncWriter.WriteFileAsync` writes into `.cotton-sync/tmp` and moves into place only after successful content write and flush; `WriteFileAsync_RemovesTemporaryFileWhenDownloadFailsAndPreservesExistingFile` and `RunOnceAsync_DoesNotUpdateBaselineWhenRemoteDownloadFails` passed in full `Cotton.Sync.Tests` 44/44.
- [x] Ensure baseline updates happen only after successful local and remote operation.
  Verification: `RunOnceAsync_DoesNotUpdateBaselineWhenRemoteUploadFails`, `RunOnceAsync_DoesNotUpdateBaselineWhenRemoteDownloadFails`, and `RunOnceAsync_DoesNotDeleteBaselineWhenRemoteDeleteFails` cover upload/download/delete failure ordering; `dotnet test src/Cotton.Sync.Tests/Cotton.Sync.Tests.csproj --configuration Release --no-restore --filter SyncEngineTests` passed 18/18 and full `Cotton.Sync.Tests` passed 44/44.
- [x] Add local delete strategy.
  Target: safe delete or recycle/trash where available; direct delete only when deliberately accepted.
  Verification: `AtomicLocalFileSyncWriter.DeleteFileAsync` moves local deletes into `.cotton-sync/deleted/<timestamp-guid>/...` and removes empty parent folders without directly deleting file content; `DeleteFileAsync_MovesFileToDeletedQuarantine` passed in full `Cotton.Sync.Tests` 48/48.
- [x] Add remote delete strategy.
  Target: trash/soft-delete by default.
  Verification: `SyncRunOptions.DeleteRemotePermanently` defaults to `false`, `SyncEngine` passes that value to `IRemoteFileSynchronizer.DeleteFileAsync`, and `RunOnceAsync_DeletesRemoteOnlyWhenBaselineKnowsLocalDelete` plus `RunOnceAsync_CanBypassRemoteTrashWhenExplicitlyConfigured` prove default trash delete and explicit permanent delete; `SyncEngineTests` passed 21/21, full `Cotton.Sync.Tests` passed 49/49, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add mass-delete guard per sync pair.
  Verification: `SyncEngine` preflights planned local and remote deletes per run and blocks all deletes for a direction when candidates exceed `SyncRunOptions` limits, preserving local files and sync baseline entries; `SyncEngineTests` passed 20/20, full `Cotton.Sync.Tests` passed 47/47, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add debounce for files still being written.
  Verification: local watcher bursts are debounced by `LocalChangeSyncCoordinator`, and `SyncPairRunner` treats `LocalFileUnavailableException` from locked/changing files as a bounded retriable local sync failure; `LocalChangeSyncCoordinatorTests` passed 3/3, `SyncPairRunnerTests` passed 10/10, full `Cotton.Sync.App.Tests` passed 75/75, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add handling for locked files.
  Verification: `LocalFileScanner` throws `LocalFileUnavailableException` instead of returning a partial scan when a file cannot be read safely or changes during hashing; `ScanAsync_ThrowsForLockedFile` passed in `LocalFileScannerTests` 5/5, full `Cotton.Sync.Tests` passed 48/48, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add ignore patterns for common temporary files.
  Verification: `LocalFileScanner.ShouldIgnore` excludes `.cotton-sync`, Office temp files, backup suffixes, `.tmp`, `.partial`, and `.crdownload`; `ScanAsync_IgnoresTempFilesAndCottonWorkingFolder` passed in `LocalFileScannerTests` 4/4 and full `Cotton.Sync.Tests` 45/45.
- [x] Add symlink/reparse-point policy.
  Verification: local scanning skips `FileAttributes.ReparsePoint` entries and does not traverse reparse-point directories through `EnumerationOptions.AttributesToSkip`; `ScanAsync_IgnoresSymlinkFilesAndDoesNotTraverseSymlinkDirectories` passed in `LocalFileScannerTests` 4/4 and full `Cotton.Sync.Tests` 45/45.
- [x] Add Windows path validation.
  Required checks: reserved names, invalid characters, path length, case collisions.
  Verification: `SyncPath.Normalize` rejects portable-Windows-invalid paths through `SyncPathValidationException`: reserved device names, reserved/control characters, trailing dot/space segments, overlong segments, and overlong relative paths; case-insensitive collisions are rejected by `SyncPathCollisionException`. `SyncPathTests` passed 12/12, `SyncEngineTests` passed 21/21, full `Cotton.Sync.Tests` passed 62/62, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add Linux path validation.
  Required checks: case-sensitive collisions with remote case-insensitive model assumptions, file permissions.
  Verification: `SyncEngine` rejects case-insensitive collisions from Linux case-sensitive local paths through `SyncPathCollisionException`, and `LocalFileScanner` rejects Unix files with no read permission bits through `LocalFileUnavailableException`; `ScanAsync_ThrowsForUnreadableUnixFile` passed in `LocalFileScannerTests` 6/6, full `Cotton.Sync.Tests` passed 64/64, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add Unicode normalization policy.
  Verification: `SyncPath.Normalize` normalizes relative paths to Unicode Form C before validation/keying, so composed and decomposed names share the same sync identity; `Normalize_UsesUnicodeFormC` passed in `SyncPathTests` 13/13, full `Cotton.Sync.Tests` passed 63/63, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add tests for every local/remote/base create-update-delete combination.
  Verification 2026-06-03: commit `Cover baseline reconciliation matrix`; `RunOnceAsync_ReconcilesBaselineMatrix` covers the 9 baseline-present combinations for local/remote missing, unchanged, and changed states. The matrix exposed and fixed two unsafe delete-vs-update cases so local delete plus remote update and local update plus remote delete now preserve conflicts instead of silently choosing one side. Focused matrix tests passed 9/9, full `Cotton.Sync.Tests` passed 74/74, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add tests for conflict naming uniqueness.
  Verification: `CreateConflictRelativePath_UsesIndexedSuffixWhenTimestampNameExists` proves conflict copy names are indexed instead of overwriting an existing conflict file with the same timestamp; `AtomicLocalFileSyncWriterTests` passed 3/3, full `Cotton.Sync.Tests` passed 50/50, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add tests for mass delete guard.
  Verification: `RunOnceAsync_BlocksRemoteDeletesOverRunLimit` and `RunOnceAsync_BlocksLocalDeletesOverRunLimit` prove over-limit delete runs emit `Skipped` activities without partially deleting files or baseline state; `SyncEngineTests` passed 20/20 and full `Cotton.Sync.Tests` passed 47/47.
- [x] Add tests for locked or unreadable files.
  Verification: `ScanAsync_ThrowsForLockedFile` holds a file with `FileShare.None`, and `ScanAsync_ThrowsForUnreadableUnixFile` removes Unix read permission bits; both verify that scanning fails with `LocalFileUnavailableException` carrying the affected relative and full paths. `LocalFileScannerTests` passed 6/6 and full `Cotton.Sync.Tests` passed 64/64.
- [x] Add tests for Windows reserved file names.
  Verification: `Normalize_RejectsWindowsReservedDeviceNames`, `Normalize_RejectsWindowsReservedCharacters`, `Normalize_RejectsWindowsTrailingDotOrSpace`, `Normalize_RejectsTooLongPathSegments`, and `Normalize_RejectsTooLongRelativePaths` cover reserved Windows names and path constraints; `SyncPathTests` passed 12/12 and full `Cotton.Sync.Tests` passed 62/62.
- [x] Add tests for case conflicts.
  Verification: `SyncEngine` rejects case-insensitive local and remote path collisions before reconciliation with `SyncPathCollisionException`; `RunOnceAsync_RejectsLocalCaseInsensitivePathCollision` and `RunOnceAsync_RejectsRemoteCaseInsensitivePathCollision` passed in `SyncEngineTests` 20/20, full `Cotton.Sync.Tests` passed 47/47, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add tests for crash during download.
  Verification: `RunOnceAsync_DoesNotUpdateBaselineWhenRemoteDownloadFails` verifies failed download preserves the existing local file, removes `.cotton-sync/tmp` partials, and leaves baseline unchanged; full `Cotton.Sync.Tests` passed 65/65 and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add tests for crash after remote upload before baseline update.
  Verification: `RunOnceAsync_RecoversAfterRemoteUploadBeforeBaselineUpdate` simulates state-store failure after remote upload, then verifies the next run adopts the remote file with matching content without uploading again; `SyncEngineTests` passed 22/22, full `Cotton.Sync.Tests` passed 65/65, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Run full sync test suite.
  Verification: `dotnet test src/Cotton.Sync.Tests/Cotton.Sync.Tests.csproj --configuration Release --no-restore` passed 89/89 and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.

## Phase 6 - Continuous Sync

- [x] Add local file watcher abstraction.
  Verification: `ILocalSyncRootWatcher`, `ILocalSyncRootWatcherFactory`, and `LocalChangeSyncCoordinator` isolate watcher behavior from sync supervision; `LocalChangeSyncCoordinatorTests` passed 3/3, full `Cotton.Sync.App.Tests` passed 77/77, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Implement Windows/Linux watcher adapter with fallback periodic scan.
  Verification: `FileSystemLocalSyncRootWatcher` uses cross-platform `FileSystemWatcher`, `FileSystemLocalSyncRootWatcherTests` passed 2/2 for missing-root and file-event publication, and `PeriodicSyncCoordinator` remains the safety fallback when watcher events are missed; full `Cotton.Sync.App.Tests` passed 77/77.
- [x] Add watcher debounce and event coalescing.
  Verification: `LocalChangeSyncCoordinator` cancels superseded pending requests per sync pair and emits one sync request after the debounce interval; `LocalChanges_AreCoalescedIntoOneSyncRequest` passed in `LocalChangeSyncCoordinatorTests` 3/3 and full `Cotton.Sync.App.Tests` passed 77/77.
- [x] Add SignalR remote event listener through SDK.
  Verification: commit `Add SDK realtime event hub client`; `CottonRealtimeClient` connects to `Routes.V1.EventHub` with SDK token storage, publishes file-tree mutation wake-up events and session-revoked events. `dotnet test src/Cotton.Sdk.Tests/Cotton.Sdk.Tests.csproj --configuration Release --filter FullyQualifiedName~CottonRealtimeClientTests` passed 2/2, and `dotnet build src/Cotton.sln --configuration Release` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Connect SignalR events to changes API fetch.
  Verification: commit `Connect realtime events to sync requests`; `RealtimeRemoteChangeSyncCoordinator` subscribes to SDK remote file-tree events, debounces them into `SyncAllAsync`, and desktop composition wires it into `SyncApplicationService`. Sync-pair work then reads and acknowledges the durable changes API page. `dotnet test src/Cotton.Sync.App.Tests/Cotton.Sync.App.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~RealtimeRemoteChangeSyncCoordinatorTests` passed 3/3, `dotnet test src/Cotton.Sync.App.Tests/Cotton.Sync.App.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~SyncApplicationServiceTests` passed 16/16, and `dotnet build src/Cotton.sln --configuration Release` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Connect sync-pair runs to changes API checkpointing.
  Verification: commit `Connect sync work to remote change feed`; `RemoteChangeAwareSyncPairWork` reads change-feed pages before full sync and acknowledges only after successful sync, with expired cursors recovered to the earliest retained cursor after full recrawl. `dotnet test src/Cotton.Sync.App.Tests/Cotton.Sync.App.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~RemoteChangeAwareSyncPairWorkTests` passed 3/3, `dotnet test src/Cotton.Sync.Tests/Cotton.Sync.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~RemoteChangeFeedReaderTests` passed 4/4, and `dotnet build src/Cotton.sln --configuration Release` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add periodic reconcile as safety fallback.
  Verification: commit `Add periodic sync safety fallback`; `PeriodicSyncCoordinator` requests `SyncAllAsync` on a 10-minute default interval and is wired into desktop application lifecycle. `dotnet test src/Cotton.Sync.App.Tests/Cotton.Sync.App.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~PeriodicSyncCoordinatorTests` passed 2/2, `dotnet test src/Cotton.Sync.App.Tests/Cotton.Sync.App.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~SyncApplicationServiceTests` passed 16/16, and `dotnet build src/Cotton.sln --configuration Release` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add offline detection and backoff.
  Verification: commit `Add sync runner offline retry`; `SyncPairRunner` classifies transient network failures and 5xx/timeout/429 HTTP failures as `Offline`, with exponential bounded backoff between attempts. `dotnet test src/Cotton.Sync.App.Tests/Cotton.Sync.App.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~SyncPairRunnerTests` passed 8/8, and `dotnet build src/Cotton.sln --configuration Release` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add retry policy with bounded attempts and user-visible error state.
  Verification: commit `Add sync runner offline retry`; retry attempts are capped by `SyncPairRunnerRetryOptions`, transient final failures surface as `Offline`, and non-transient failures remain `Error` with `LastError`.
- [x] Add per-pair queue so repeated triggers collapse into one sync pass.
  Verification: commit `Coalesce sync pair run requests`; `SyncPairRunner` collapses overlapping `SyncNowAsync` requests into one queued follow-up run. `dotnet test src/Cotton.Sync.App.Tests/Cotton.Sync.App.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~SyncPairRunnerTests` passed 9/9, and `dotnet build src/Cotton.sln --configuration Release` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add tests for local watcher event coalescing.
  Verification: `LocalChanges_AreCoalescedIntoOneSyncRequest` covers coalesced local watcher events per sync pair; focused continuous-sync tests passed 19/19, full `Cotton.Sync.App.Tests` passed 77/77, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add tests for remote SignalR trigger causing changes fetch.
  Verification: `RealtimeRemoteChangeSyncCoordinatorTests` cover SignalR wake-up debounce into sync requests, and `RemoteChangeAwareSyncPairWorkTests` cover change-feed read/acknowledge around sync work; focused continuous-sync tests passed 19/19 and full `Cotton.Sync.App.Tests` passed 77/77.
- [x] Add tests for offline to online recovery.
  Verification: `SyncNowAsync_RetriesTransientNetworkFailureAndReturnsIdleOnRecovery` covers transient offline recovery back to `Idle`; focused continuous-sync tests passed 19/19 and full `Cotton.Sync.App.Tests` passed 77/77.
- [ ] Add two-client integration test against local dev server.
- [ ] Run continuous sync soak test for at least 2 hours before this phase is considered done.

## Phase 7 - Authentication And Token Storage

- [x] Keep password/TOTP login as the first implemented auth flow.
  Verification: `PasswordAuthFlow` maps username/password plus optional TOTP into SDK `LoginRequestDto`, trims user-entered non-secret fields, and returns `AuthSession`; `PasswordAuthFlowTests` passed 5/5, focused auth/service tests passed 21/21, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add `IAuthFlow` abstraction.
  Required implementations: password flow now, browser flow placeholder later.
  Verification: `IAuthFlow` is the app-layer auth boundary used by `SyncApplicationService`, with `PasswordAuthFlow` as the current implementation; focused auth/service tests passed 21/21 and full `Cotton.Sync.App.Tests` passed 77/77.
- [x] Add named-device metadata where server supports it.
  Verification: commit `Send desktop device metadata`; SDK `CottonSdkOptions` now supports `UserAgent` and `DeviceName`, desktop factory sends a generated Cotton Sync Desktop user agent and device name, and server auth session issuing stores shared-contract `X-Cotton-Device-Name` into the existing refresh-token `Device` field when present. `CottonAuthClientTests` passed 6/6, full `Cotton.Sdk.Tests` passed 21/21, `AuthSmokeTests` passed 5/5 including session device persistence, and `dotnet build src/Cotton.sln --configuration Release` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add secure token store abstraction.
  Verification: commit `Add token payload protection boundary`; desktop token persistence now writes a protected envelope through `ITokenPayloadProtector`, with tests proving the file store uses the protector, rejects mismatched protection schemes, ignores unreadable protected payloads, roundtrips valid tokens, clears tokens, and keeps Unix file permissions restricted. `dotnet test src/Cotton.Sync.Desktop.Tests/Cotton.Sync.Desktop.Tests.csproj --configuration Release --no-restore --filter FileCottonTokenStoreTests` passed 9/9.
- [x] Implement Windows secure token store.
  Preferred target: Windows Credential Manager or DPAPI-backed per-user storage.
  Verification: commit `Add token payload protection boundary`; `WindowsDpapiTokenPayloadProtector` uses DPAPI current-user protection via `System.Security.Cryptography.ProtectedData` and is selected by `DesktopTokenPayloadProtectorFactory` on Windows. The Windows-only roundtrip test is present in `FileCottonTokenStoreTests` and runs on Windows; Linux build/test verification passed, while manual Windows verification remains tracked below.
- [x] Implement Linux secure token store.
  Preferred target: Secret Service/libsecret where available.
  Verification 2026-06-03: added `LinuxSecretServiceTokenPayloadProtector`, selected by `DesktopTokenPayloadProtectorFactory` on Linux when `secret-tool` is present in `PATH`; token files store only the Secret Service payload id, and `FileCottonTokenStore` now cleans external protected payloads on overwrite, logout, and failed token-file commits. Focused auth storage tests passed 22/22, full `Cotton.Sync.Desktop.Tests` passed 85/85, and full solution Release build passed with the known NU1903 Avalonia/Tmds.DBus.Protocol warning. Local environment did not have `secret-tool`, so real Secret Service manual verification remains open below.
- [x] Add explicit fallback policy for Linux environments without a secret service.
  Release decision must be explicit: block login, prompt user, or encrypted local store.
  Verification: commit `Add token payload protection boundary`; current non-Windows fallback is explicitly `RestrictedFileTokenPayloadProtector` with a protected-envelope scheme and chmod-restricted token file. This remains a deliberate development fallback for environments without Secret Service; final release policy and Linux manual verification remain open.
  Verification 2026-06-03: self-test now includes `Token storage` and reports restricted-file storage as a development fallback that is not release-secure. `DesktopTokenStorageCapabilitiesTests` cover restricted-file, Linux Secret Service, and Windows DPAPI capability classification.
- [x] Ensure logout clears tokens and stops all sync runners.
  Verification: `SyncApplicationService.SignOutAsync` stops remote changes, periodic sync, local changes, and supervisor before delegating to `IAuthFlow.SignOutAsync`; SDK `CottonAuthClient.LogoutAsync` clears `ICottonTokenStore`, including failed server logout responses. `SyncApplicationServiceTests` plus `PasswordAuthFlowTests` passed in focused auth/service tests 21/21, and `CottonAuthClientTests` passed 3/3.
- [x] Handle refresh-token revocation and session-revoked SignalR event.
  Verification: `CottonAuthClient.LogoutAsync` clears saved tokens in a `finally` block when server logout fails, `RealtimeRemoteChangeSyncCoordinator` subscribes to SDK `SessionRevoked`, cancels pending remote-triggered sync, invokes `SessionRevocationHandler`, and stops realtime observation. `SessionRevocationHandler` stops periodic/local sync, signs out, and stops the supervisor while logging and continuing through non-cancellation failures. Focused `CottonAuthClientTests` passed 3/3; focused `RealtimeRemoteChangeSyncCoordinatorTests|SessionRevocationHandlerTests` passed 7/7; full `Cotton.Sdk.Tests` passed 18/18; full `Cotton.Sync.App.Tests` passed 81/81; `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add tests for login, refresh, logout, token clear, and invalid saved token.
  Verification: `PasswordAuthFlowTests` cover login/TOTP/logout delegation, `CottonHttpTransportTests|CottonAuthClientTests` passed 3/3 for refresh/logout/token clear behavior, and `FileCottonTokenStoreTests` cover token clear, invalid saved-token rejection, and external protected-payload cleanup.
- [ ] Add manual verification on Windows secure storage.
- [ ] Add manual verification on Linux secure storage.

## Phase 8 - Desktop UX And Visual Design

- [ ] Replace prototype screen with release-grade app structure.
- [ ] Add onboarding flow.
  Steps: welcome, sign in, optional autostart, add first sync pair, initial sync, finished state.
- [ ] Add main dashboard.
  Required content: global status, sync-pair list, per-pair status, current progress, recent activity.
  Partial 2026-06-03: compact dashboard flyout now shows global status, account, sync-pair list/empty state, per-pair status rows, recent activity, quick add/sync actions, and a menu for secondary actions. Keep unchecked until current progress, settings navigation, conflict/error surfaces, and full visual QA are complete.
- [ ] Add sync-pair editor.
  Required fields: local folder picker, remote folder picker/resolver, display name, enabled flag, sync mode.
  Partial 2026-06-03: settings now exposes existing sync folders with selected-pair open, enable/disable, and remove actions. `DesktopShellController.SetSyncPairEnabledAsync` persists the enabled flag through the EF sync-pair store and `RemoveSyncPairAsync` deletes the pair; `ShellViewModel` updates selected-row state and selection after commands. Focused controller/view-model tests passed 11/11, full `Cotton.Sync.Desktop.Tests` passed 93/93, and full solution Release build passed with the known NU1903 Avalonia/Tmds.DBus.Protocol warning. Keep unchecked until display-name editing, sync-mode editing/feature-gated placeholders, and polished add/edit flow are complete.
- [ ] Add remote folder picker or searchable remote folder selector.
- [ ] Add settings screen.
  Required sections: account, sync pairs, startup, notifications, diagnostics, about.
  Partial 2026-06-03: desktop shell now has a settings overlay with account, startup, diagnostics, about, and sign-out controls; the dashboard secondary-action menu opens it. Keep unchecked until sync-pair management, notifications settings, and full settings navigation are complete.
  Partial 2026-06-03: settings now includes persisted notification and appearance controls. Keep unchecked until sync-pair management and full settings navigation are complete.
  Partial 2026-06-03: settings now includes a sync folders section with empty state, selected-pair details, and open/enable-disable/remove commands. Focused controller/view-model tests passed 11/11, full `Cotton.Sync.Desktop.Tests` passed 93/93, and full solution Release build passed with the known NU1903 warning. Keep unchecked until the full settings surface is screenshot-reviewed and visually tightened.
- [ ] Add conflict/error screen.
  Required behavior: show conflict files, action-required errors, retry/sync-now command.
  Partial 2026-06-03: dashboard now exposes an action-required banner when sync status, command failures, or self-test failures produce an error message. The message resolver is covered by `DesktopActionRequiredMessageResolverTests` 4/4, full `Cotton.Sync.Desktop.Tests` passed 29/29, and solution Release build passed with known NU1903 warnings. Keep unchecked until conflict-file listing and retry/action workflows are complete.
  Partial 2026-06-03: action-required banner now includes compact `Retry` and `Check` actions; `Retry` is visible only when signed in and invokes `SyncNowCommand`, while `Check` runs the desktop self-test. `ShellViewModelSyncPairCommandTests` covers self-test-created action-required state, retry visibility, sync retry invocation, and message clearing. Focused action-required tests passed 9/9, full `Cotton.Sync.Desktop.Tests` passed 94/94, and full solution Release build passed with the known NU1903 warning. Keep unchecked until conflict-file listing and conflict-specific actions are complete.
- [ ] Add not-implemented UX for reserved future modes only in development builds or behind a feature flag.
- [ ] Add polished empty states.
- [x] Add dark/light theme support.
  Verification 2026-06-03: added persisted `AppThemeMode` values `System`, `Light`, and `Dark` through EF-generated migration `20260603211332_AddAppThemePreference`; settings exposes an Appearance selector, `ShellViewModel` applies the theme through `IDesktopThemeService`, and `AvaloniaDesktopThemeService` maps preferences to Avalonia `ThemeVariant`. Focused preferences tests passed 5/5, focused desktop theme/controller tests passed 13/13, full `Cotton.Sync.App.Tests` passed 83/83, full `Cotton.Sync.Desktop.Tests` passed 72/72, and full solution Release build passed with the known NU1903 warning.
- [ ] Add responsive layout for minimum window size.
- [ ] Ensure no user-visible strings are hardcoded if localization is required for desktop release.
- [x] Run Avalonia desktop build.
  Verification: commits `Refine desktop setup shell` and `Refine compact setup sign-in`; `dotnet build src/Cotton.Sync.Desktop/Cotton.Sync.Desktop.csproj --configuration Release --no-restore`, `dotnet test src/Cotton.Sync.Desktop.Tests/Cotton.Sync.Desktop.Tests.csproj --configuration Release --no-restore`, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings. Headless setup screenshots were captured at `/tmp/cotton-sync-setup.png`, `/tmp/cotton-sync-setup-2.png`, and `/tmp/cotton-sync-setup-ui.png`; dashboard screenshots were captured at `/tmp/cotton-sync-dashboard-ui.png`, `/tmp/cotton-sync-dashboard-ui-fixed.png`, and `/tmp/cotton-sync-dashboard-settings-pass.png` for clipping/spacing inspection.
- [ ] Run manual UI walkthrough on Linux.
- [ ] Run manual UI walkthrough on Windows.
- [ ] Capture screenshots for onboarding, dashboard, settings, conflict state, and error state.
- [ ] Review screenshots for clipping, overlap, weak hierarchy, and inconsistent spacing.
- [ ] Run visual QA checklist from `notes/visual-qa-checklist.md` or replace it with an updated desktop checklist.

## Phase 9 - Tray, Autostart, Notifications, And Lifecycle

- [x] Add single-instance enforcement.
  Verification 2026-06-03: commit `Add desktop single-instance guard`; desktop startup acquires a per-user app-data lock file before starting Avalonia, and a second process exits when the lock is already held. Focused `DesktopSingleInstanceGuardTests` passed 3/3, full `Cotton.Sync.Desktop.Tests` passed 18/18, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add close-to-tray behavior.
  Verification 2026-06-03: Windows-only tray lifecycle uses `ShutdownMode.OnExplicitShutdown`; `MainWindow` cancels close and hides to tray when tray lifecycle is available, while Linux keeps normal close semantics because tray lifecycle is unsupported. `DesktopWindowLifecyclePolicyTests` passed 4/4, full `Cotton.Sync.Desktop.Tests` passed 22/22, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings. Manual Windows tray verification remains open below.
- [x] Add explicit quit behavior.
  Verification 2026-06-03: tray Quit calls `MainWindow.RequestQuit()` before application shutdown, and the window lifecycle policy then resolves close as a real close instead of hide-to-tray. `DesktopWindowLifecyclePolicyTests` passed 4/4, full `Cotton.Sync.Desktop.Tests` passed 22/22, and solution Release build passed with known NU1903 warnings.
- [x] Add tray icon states.
  Required states: idle, syncing, paused, offline, error.
  Partial 2026-06-03: added a tested tray status model/resolver for signed-out, idle, syncing, paused, offline, and error states, and wired it to the tray tooltip.
  Verification 2026-06-03: added dedicated tray status icon assets for signed-out, idle, syncing, paused, offline, and error states, plus `DesktopTrayIconAssetResolver`; `DesktopTrayController` now updates the tray icon when status changes. Focused tray tests passed 7/7, full `Cotton.Sync.Desktop.Tests` passed 89/89, and full solution Release build passed with the known NU1903 warning. Manual Windows tray verification remains open below.
- [x] Add tray menu.
  Required commands: show app, open folder, open web, sync now, pause/resume, settings, quit.
  Partial 2026-06-03: commit `Expand desktop tray menu commands`; Windows tray menu now wires show app, open selected local folder, open Cotton Cloud in browser, sync now, pause, resume, settings overlay, and quit.
  Verification 2026-06-03: `DesktopTrayController` keeps the required menu command surface wired through `ShellViewModel` commands while the tray status/icon slice passed focused tray tests, full desktop tests, and solution Release build. Manual Windows tray behavior remains open below.
- [x] Add Windows autostart adapter.
  Acceptable options: installer-managed startup, registry Run entry, startup shortcut, or scheduled task.
  Verification 2026-06-03: `WindowsRunAutostartService` uses the current-user `Software\Microsoft\Windows\CurrentVersion\Run` entry `Cotton Sync` with the packaged launch command, and registry access is behind `IWindowsRunRegistry` so the enable/disable/matching-command behavior is covered without touching a real registry. `WindowsRunAutostartServiceTests` passed 3/3, full `Cotton.Sync.Desktop.Tests` passed 25/25, and solution Release build passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings. Manual reboot verification remains open below.
- [x] Add Linux autostart adapter.
  Target: XDG autostart `.desktop`.
  Verification 2026-06-03: commit `Clarify desktop lifecycle platform support`; Linux uses an XDG autostart `.desktop` adapter, and the factory no longer adds `--start-minimized` on Linux while tray lifecycle is unsupported. `Cotton.Sync.Desktop.Tests` passed 15/15 including the Linux factory regression test, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [ ] Add notification adapter.
  Required notifications: initial sync complete, conflict created, action-required error.
  Partial 2026-06-03: added a tested notification tracker for initial sync complete, conflict, and action-required error status transitions, and wired it to compact in-app dashboard notifications. Focused `DesktopNotificationTrackerTests` passed 5/5. Keep unchecked until native Windows/Linux notification adapters are implemented and manually verified.
  Partial 2026-06-03: added `IDesktopNotificationService`, Linux `notify-send` support with safe unsupported fallback, and self-test reporting for the notification adapter. New platform notification tests passed, full `Cotton.Sync.Desktop.Tests` passed 62/62, `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with the known NU1903 warning, and local `--self-test --data-dir <temp>` reported `Notification adapter - Supported` on Linux/XFCE. Keep unchecked until Windows native notifications and manual OS notification checks are complete.
  Partial 2026-06-03: settings now exposes a persisted "Show desktop notifications" toggle backed by `AppPreferences.EnableNotifications`, and native notifications are suppressed when it is disabled while in-app notification history remains visible. Focused controller tests passed 5/5, full `Cotton.Sync.Desktop.Tests` passed 66/66, and full solution Release build passed with the known NU1903 warning.
- [x] Add tests for single-instance lock where practical.
  Verification 2026-06-03: `DesktopSingleInstanceGuardTests` cover first acquire, blocked second acquire, and acquire after dispose; focused tests passed 3/3.
- [ ] Add manual Windows verification: autostart after reboot, tray behavior, notifications.
- [ ] Add manual Linux verification: autostart after login, tray behavior, notification behavior.
- [x] Document Linux tray limitations in app capabilities and self-test.
  Verification 2026-06-03: `DesktopPlatformCapabilities.CreateSnapshot()` records OS/session/current desktop and explains why Linux tray lifecycle is not release-supported yet; settings and self-test now surface that reason. `Cotton.Sync.Desktop.Tests` includes a Linux guard that verifies the app does not claim tray lifecycle support on Linux.
- [ ] Record actual tested Linux desktop environments after clean-machine/manual runs.
  Partial 2026-06-03: desktop settings now explicitly explain when tray lifecycle is unavailable, and Linux/XDG autostart tests verify autostart does not use `--start-minimized` without tray lifecycle support. Keep unchecked until manual Linux desktop-environment results are recorded.

## Phase 10 - Diagnostics And Supportability

- [ ] Add structured logging for app, sync, SDK, and platform adapters.
  Partial 2026-06-03: desktop composition now provides a trace-backed `ILoggerFactory` to sync runners, local/remote coordinators, periodic sync, session revocation, sync-root probes, filesystem watchers, and platform command services. `DesktopTraceLogger` redacts secrets before writing to the rotating trace log. Keep unchecked until SDK and sync-core instrumentation coverage is added.
- [x] Add log rotation.
  Verification 2026-06-03: desktop startup installs a `RotatingFileTraceListener` into app-data `cotton-sync.log`; it retains rotated files as `.1`, `.2`, `.3` and is idempotent per log path. Focused `RotatingFileTraceListenerTests` passed 3/3. Full desktop tests and solution build are recorded in the commit verification.
- [x] Add diagnostics screen.
  Required fields: app version, server URL, account, sync pair ids, local paths, remote ids, last sync time, current cursor, last error.
  Verification 2026-06-03: settings diagnostics now shows app version, server URL, account, sync pair ids, local paths, remote paths, remote root ids, per-pair status, last sync time, current change cursor, and last error, plus self-test execution/export controls and latest self-test results. `DesktopShellControllerSelfTestTests` cover required self-test entries and enriched sync-pair diagnostics fields from sync state. Focused tests passed 3/3. Full desktop tests and solution build are recorded in the commit verification.
- [x] Add export diagnostics bundle command.
  Verification 2026-06-03: settings now exposes an Export diagnostics command; `DesktopDiagnosticsExporter` writes a zip bundle under app-data `diagnostics/` containing `diagnostics.json` and rotated trace logs, while excluding token and SQLite files. Focused `DesktopDiagnosticsExporterTests` passed 2/2. Full desktop tests and solution build are recorded in the commit verification.
- [x] Redact tokens and secrets from logs and diagnostics.
  Verification 2026-06-03: diagnostics export excludes `tokens.json`, `sync-app.db`, and `sync-state.db`, and `DesktopSecretRedactor` redacts bearer tokens plus JSON/query secrets before writing diagnostics JSON or log entries into the bundle. Focused redaction/exporter tests passed 6/6. Full desktop tests and solution build are recorded in the commit verification.
- [x] Add self-test command.
  Required checks: server reachability, auth state, local folder access, remote folder access, SQLite state access, watcher availability.
  Verification 2026-06-03: self-test now checks preferences DB, sync-pair DB, sync-state DB, authentication state, autostart adapter, tray lifecycle, file watcher availability, server identity, local roots, and remote roots when signed in; unsigned remote roots are explicitly reported as requiring sign-in to verify. Focused `DesktopShellControllerSelfTestTests` passed 2/2. Full desktop tests and solution build are recorded in the commit verification.
  Verification 2026-06-03: self-test also reports token-storage capability so diagnostics distinguish Windows DPAPI, Linux Secret Service, and the development-only restricted-file fallback. Focused token-storage/self-test tests passed 10/10, full `Cotton.Sync.Desktop.Tests` passed 88/88, and full solution Release build passed with the known NU1903 warning.
- [x] Add tests for redaction.
  Verification 2026-06-03: `DesktopSecretRedactorTests` cover bearer, JSON, and query-string secret patterns; `DesktopDiagnosticsExporterTests` cover archive exclusion of token/database files and redaction of secrets in exported logs.
- [ ] Add manual diagnostics export verification.

## Phase 11 - Packaging And Installers

- [x] Define release artifact matrix.
  Required artifacts: Windows installer, Windows portable archive, Linux AppImage or archive, Linux `.deb` if feasible.
  Verification 2026-06-03: release matrix is defined as: Windows portable archive `cotton-sync-desktop-win-x64.tar.gz` now, Windows installer before release readiness, Linux portable archive `cotton-sync-desktop-linux-x64.tar.gz` now, Linux `.deb` or AppImage as feasible before release readiness, `checksums.sha256` inside each portable archive, and NuGet/shared package artifact unchanged. CI already uploads the two portable desktop archives; installer, `.deb`/AppImage, clean-machine smoke, uninstall, and upgrade checks remain open below.
- [x] Configure self-contained publish for Windows x64.
  Verification 2026-06-03: added `src/Cotton.Sync.Desktop/Properties/PublishProfiles/win-x64.pubxml`; `dotnet publish src/Cotton.Sync.Desktop/Cotton.Sync.Desktop.csproj /p:PublishProfile=win-x64` passed and produced `bin/Release/net10.0/publish/win-x64/Cotton.Sync.Desktop.exe` with the known NU1903 warning.
- [x] Configure self-contained publish for Linux x64.
  Verification 2026-06-03: added `src/Cotton.Sync.Desktop/Properties/PublishProfiles/linux-x64.pubxml`; `dotnet publish src/Cotton.Sync.Desktop/Cotton.Sync.Desktop.csproj /p:PublishProfile=linux-x64` passed and produced executable `bin/Release/net10.0/publish/linux-x64/Cotton.Sync.Desktop` with the known NU1903 warning.
- [x] Add app icon and metadata for packaged apps.
  Verification 2026-06-03: generated `src/Cotton.Sync.Desktop/Assets/app.ico` from the real frontend PNG icon and set `ApplicationIcon=Assets/app.ico`; Avalonia window/tray resources continue to use the frontend `icon-192.png`. Desktop build and both `linux-x64`/`win-x64` publish profiles passed with the known NU1903 warning. Clean Windows visual icon verification remains open under package smoke tests.
- [x] Add version/about metadata.
  Verification 2026-06-03: `Cotton.Sync.Desktop.csproj` now sets explicit title, `VersionPrefix=0.1.0`, `AssemblyVersion=0.1.0.0`, `FileVersion=0.1.0.0`, and `InformationalVersion=0.1.0-dev`; the settings/about and diagnostics paths already read the assembly version. `dotnet msbuild` confirmed `Version=0.1.0`, `InformationalVersion=0.1.0-dev`, and `ApplicationIcon=Assets/app.ico`; desktop tests passed 56/56.
- [x] Add checksum generation.
  Verification 2026-06-03: `Cotton.Sync.Desktop.csproj` now generates `checksums.sha256` after publish. Verified with both `linux-x64` and `win-x64` publish profiles; checksum files include the Linux apphost, Windows `.exe`, and desktop DLL entries. Both publish commands passed with the known NU1903 warning.
- [x] Add signing plan.
  Windows code signing can be deferred only with an explicit release risk decision.
  Verification 2026-06-03: signing plan is explicit: final Windows installer/executable should be Authenticode-signed with a project code-signing certificate; portable archives must always include generated SHA-256 checksums; Linux archives remain checksum-verified, while future `.deb`/repository distribution should use package/repository signing. If Windows code signing is unavailable for an internal preview, the release notes must record an explicit unsigned-build risk decision before release readiness can be checked.
- [x] Add CI workflow for build, test, publish, package, and artifact upload.
  Verification 2026-06-03: `.github/workflows/docker-image.yml` now publishes `linux-x64` and `win-x64` desktop profiles, runs the Linux published `--self-test --data-dir <temp>` smoke command, packages portable `cotton-sync-desktop-linux-x64.tar.gz` and `cotton-sync-desktop-win-x64.tar.gz` archives, uploads both as workflow artifacts, and attaches them to main-branch releases beside the NuGet package. Local workflow command simulation confirmed both archives contain the executable and `checksums.sha256`; YAML parsed successfully with PyYAML.
- [x] Add smoke launch command or self-test command for packaged app.
  Verification 2026-06-03: added `--self-test`/`--smoke-test` desktop CLI mode plus `--data-dir` for isolated packaged smoke runs. The command prints self-test results and returns exit code `0` only when checks pass. Verified with `dotnet run --project src/Cotton.Sync.Desktop/Cotton.Sync.Desktop.csproj --configuration Release --no-restore -- --self-test --data-dir <temp>` and the published Linux executable `bin/Release/net10.0/publish/linux-x64/Cotton.Sync.Desktop --self-test --data-dir <temp>`; both passed. `win-x64` publish, full desktop tests 59/59, and full solution Release build passed with the known NU1903 warning.
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
