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
- [ ] Add or refine remote snapshot representation for change-feed updates.
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
- [ ] Add tests for every local/remote/base create-update-delete combination.
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
  Verification: `dotnet test src/Cotton.Sync.Tests/Cotton.Sync.Tests.csproj --configuration Release --no-restore` passed 65/65 and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.

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
- [ ] Add named-device metadata where server supports it.
- [x] Add secure token store abstraction.
  Verification: commit `Add token payload protection boundary`; desktop token persistence now writes a protected envelope through `ITokenPayloadProtector`, with tests proving the file store uses the protector, rejects mismatched protection schemes, ignores unreadable protected payloads, roundtrips valid tokens, clears tokens, and keeps Unix file permissions restricted. `dotnet test src/Cotton.Sync.Desktop.Tests/Cotton.Sync.Desktop.Tests.csproj --configuration Release --no-restore --filter FileCottonTokenStoreTests` passed 9/9.
- [x] Implement Windows secure token store.
  Preferred target: Windows Credential Manager or DPAPI-backed per-user storage.
  Verification: commit `Add token payload protection boundary`; `WindowsDpapiTokenPayloadProtector` uses DPAPI current-user protection via `System.Security.Cryptography.ProtectedData` and is selected by `DesktopTokenPayloadProtectorFactory` on Windows. The Windows-only roundtrip test is present in `FileCottonTokenStoreTests` and runs on Windows; Linux build/test verification passed, while manual Windows verification remains tracked below.
- [ ] Implement Linux secure token store.
  Preferred target: Secret Service/libsecret where available.
- [x] Add explicit fallback policy for Linux environments without a secret service.
  Release decision must be explicit: block login, prompt user, or encrypted local store.
  Verification: commit `Add token payload protection boundary`; current non-Windows fallback is explicitly `RestrictedFileTokenPayloadProtector` with a protected-envelope scheme and chmod-restricted token file. This is a deliberate development fallback, not a completed Linux secure-store implementation; `Implement Linux secure token store` and Linux manual verification remain open.
- [x] Ensure logout clears tokens and stops all sync runners.
  Verification: `SyncApplicationService.SignOutAsync` stops remote changes, periodic sync, local changes, and supervisor before delegating to `IAuthFlow.SignOutAsync`; SDK `CottonAuthClient.LogoutAsync` clears `ICottonTokenStore`, including failed server logout responses. `SyncApplicationServiceTests` plus `PasswordAuthFlowTests` passed in focused auth/service tests 21/21, and `CottonAuthClientTests` passed 3/3.
- [x] Handle refresh-token revocation and session-revoked SignalR event.
  Verification: `CottonAuthClient.LogoutAsync` clears saved tokens in a `finally` block when server logout fails, `RealtimeRemoteChangeSyncCoordinator` subscribes to SDK `SessionRevoked`, cancels pending remote-triggered sync, invokes `SessionRevocationHandler`, and stops realtime observation. `SessionRevocationHandler` stops periodic/local sync, signs out, and stops the supervisor while logging and continuing through non-cancellation failures. Focused `CottonAuthClientTests` passed 3/3; focused `RealtimeRemoteChangeSyncCoordinatorTests|SessionRevocationHandlerTests` passed 7/7; full `Cotton.Sdk.Tests` passed 18/18; full `Cotton.Sync.App.Tests` passed 81/81; `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings.
- [x] Add tests for login, refresh, logout, token clear, and invalid saved token.
  Verification: `PasswordAuthFlowTests` cover login/TOTP/logout delegation, `CottonHttpTransportTests|CottonAuthClientTests` passed 3/3 for refresh/logout/token clear behavior, and `FileCottonTokenStoreTests` passed 9/9 including incomplete saved-token rejection.
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
- [x] Run Avalonia desktop build.
  Verification: commits `Refine desktop setup shell` and `Refine compact setup sign-in`; `dotnet build src/Cotton.Sync.Desktop/Cotton.Sync.Desktop.csproj --configuration Release --no-restore`, `dotnet test src/Cotton.Sync.Desktop.Tests/Cotton.Sync.Desktop.Tests.csproj --configuration Release --no-restore`, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with known NU1903 Avalonia/Tmds.DBus.Protocol warnings. Headless setup screenshots were captured at `/tmp/cotton-sync-setup.png` and `/tmp/cotton-sync-setup-2.png` for visual inspection.
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
