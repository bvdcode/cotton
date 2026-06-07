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
