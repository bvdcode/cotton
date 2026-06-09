## Non-Negotiable Decisions

- [x] Use the existing .NET stack: `Cotton.Sync`, `Cotton.Sdk`, EF Core SQLite, and Avalonia.
  Verification 2026-06-09: the release branch contains the intended .NET stack projects (`Cotton.Sdk`, `Cotton.Sync`, `Cotton.Sync.App`, `Cotton.Sync.Desktop`, and `Cotton.Sync.Cli`), EF Core SQLite state/settings stores, and Avalonia desktop shell. Recent batched Release builds for sync core, app layer, and desktop all passed with 0 warnings.
- [x] Keep synchronization logic out of desktop UI code.
  Verification 2026-06-09: sync reconciliation remains in `Cotton.Sync`, application orchestration remains in `Cotton.Sync.App`, and `Cotton.Sync.Desktop` uses shell/controller/view-model commands instead of constructing `SyncEngine` directly. Phase 2 and the current work-order record focused tests proving UI can use the app layer without directly constructing the sync engine.
- [x] Use EF Core SQLite for durable local state and settings. Do not use raw SQL for normal app state.
  Verification 2026-06-03: `SqliteSyncStateStore`, `SqliteSyncPairSettingsStore`, and `SqliteAppPreferencesStore` use EF Core SQLite contexts and migrations; `rg "Microsoft.Data.Sqlite|SqliteConnection|CommandText|CREATE TABLE|SELECT |INSERT |UPDATE |DELETE "` across `Cotton.Sync`, `Cotton.Sync.App`, and `Cotton.Sync.Desktop` found no raw SQL command usage. `dotnet test src/Cotton.Sync.Tests/Cotton.Sync.Tests.csproj --configuration Release --no-restore` passed 89/89.
- [x] Treat WebDAV as interoperability, not as the native desktop sync engine.
  Verification 2026-06-09: desktop and CLI sync paths compose `Cotton.Sdk` remote file/node/chunk clients, `SdkRemoteFileSynchronizer`, `SdkRemoteDirectorySynchronizer`, change-feed reading, and SignalR wakeups; no desktop sync path is based on WebDAV.
- [x] Treat SignalR as a wake-up signal, not as the durable source of remote truth.
  Verification 2026-06-09: `RealtimeRemoteChangeSyncCoordinator` uses realtime events to trigger work, while `RemoteChangeFeedReader` reads durable `/sync/changes` pages and acknowledges cursors only after successful processing. Phase 3/6 verification covers missed SignalR recovery through the changes API.
- [x] Implement full-mirror folder sync first. Virtual files/placeholders are a later platform feature.
  Verification 2026-06-09: implemented sync modes and product flow are full-mirror sync pairs; Windows virtual files/placeholders remain explicitly open in `01-windows-only-work.md` and `30-future-platform-features.md`, not hidden in the first release path.
- [x] Support multiple sync pairs in the product model from the start.
  Verification 2026-06-09: `SyncPairSettings`, EF settings storage, supervisor runners, dashboard rows, global progress aggregation, and pause/disable tests all operate over multiple sync pairs. Windows manual two-pair behavior remains separately tracked.
- [x] Do not support overlapping local sync roots.
  Verification 2026-06-09: `SyncPairSettingsValidator` rejects equal or nested local roots through normalized path comparisons, with Windows-style and Linux-style overlap tests recorded in Phase 1.
- [x] Do not ship insecure token storage in the final release.
  Verification 2026-06-09: desktop sign-in/session restore/self-test require a release-secure token protector; Linux Secret Service and Windows DPAPI are the release-secure paths, while restricted-file storage is classified as non-release-secure and fails the release self-test. Final clean-machine token-storage gates remain open.
- [x] Do not mark release readiness until clean-machine install and sync smoke tests pass.
  Verification 2026-06-09: Phase 14 release readiness remains intentionally unchecked for clean Windows/Linux package smoke, final screenshot review, lifecycle verification, secure token storage verification, diagnostics export verification, and final release diff. This rule is being enforced rather than closed by assumption.
