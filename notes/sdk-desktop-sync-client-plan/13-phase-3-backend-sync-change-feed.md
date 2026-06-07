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
