## Phase 1 - Release-Grade App Model

- [x] Add a `SyncPairSettings` domain model.
  Required fields: id, display name, local root path, remote root node id, remote display path, enabled flag, sync mode, created/updated timestamps.
  Verification 2026-06-03: implemented in `src/Cotton.Sync.App/SyncPairs/SyncPairSettings.cs`; covered by `Cotton.Sync.App.Tests`.
- [x] Add sync mode values.
  Required modes: full mirror now, virtual files placeholder reserved for later.
  Verification 2026-06-03: implemented in `src/Cotton.Sync.App/SyncPairs/SyncPairMode.cs`; unsupported placeholder mode is rejected by tests.
  Verification 2026-06-05: `SyncPairMode` now reserves `Unknown = 0`, keeps `SyncPairSettings.Mode` explicitly defaulted to `FullMirror`, and validator coverage rejects unknown modes so default enum values cannot silently become a real sync configuration. Focused app/desktop mode tests passed 137/137, and full solution Release build passed with 0 warnings.
- [x] Add sync pair validation.
  Required checks: local path exists or can be created, local roots do not overlap, remote target is resolvable, mode is supported.
  Verification 2026-06-03: structural validator plus async prerequisite validator implemented; tests cover overlap, unsupported mode, local root creation/unavailable path, remote prerequisite errors, and save rejection without persistence.
  Verification 2026-06-04: sync-pair update flow now has regression coverage proving an existing pair is replaced in the validation set before overlap checks, so editing the same pair does not self-reject as an overlapping local root. Focused `SyncApplicationServiceTests` passed 19/19, full `Cotton.Sync.App.Tests` passed 107/107, and `dotnet build src/Cotton.sln --configuration Release` passed with 0 warnings.
  Verification 2026-06-04: Windows UNC sync roots are now covered by overlap validation tests, including case-insensitive nested network-share paths. Focused `SyncPairSettingsValidatorTests` passed 9/9, full `Cotton.Sync.App.Tests` passed 108/108, and `dotnet build src/Cotton.sln --configuration Release` passed with 0 warnings.
  Verification 2026-06-04: the desktop add-folder wizard now applies the existing overlap validator immediately after local folder selection, so selecting an already configured or nested local root stays on the local step and does not fetch remote folders or surface a later unrelated server error. The wizard also clears that local error when the next selected folder is valid or the wizard is closed. Focused add-folder local-selection tests passed 7/7, full `Cotton.Sync.Desktop.Tests` passed 283/283, and `dotnet build src/Cotton.sln --configuration Release --no-restore` passed with 0 warnings.
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
