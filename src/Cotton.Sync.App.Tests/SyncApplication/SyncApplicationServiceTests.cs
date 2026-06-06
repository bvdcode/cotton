// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Auth;
using Cotton.Sync.App.Continuous;
using Cotton.Sync.App.LocalChanges;
using Cotton.Sync.App.Platform;
using Cotton.Sync.App.Preferences;
using Cotton.Sync.App.RemoteChanges;
using Cotton.Sync.App.SyncApplication;
using Cotton.Sync.App.SyncPairs;
using Cotton.Sync.App.Status;
using Cotton.Sync.App.Supervision;
using Cotton.Sync.State;

namespace Cotton.Sync.App.Tests.SyncApplication;

public sealed class SyncApplicationServiceTests
{
    [Test]
    public async Task SignInAsync_DelegatesToAuthFlow()
    {
        var authFlow = new FakeAuthFlow();
        SyncApplicationService service = CreateService(new InMemorySyncPairSettingsStore(), authFlow: authFlow);
        var request = new PasswordSignInRequest
        {
            Username = "vadim",
            Password = "password",
        };

        AuthSession session = await service.SignInAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(authFlow.SignInCallCount, Is.EqualTo(1));
            Assert.That(authFlow.LastSignInRequest, Is.SameAs(request));
            Assert.That(session, Is.SameAs(authFlow.Session));
        });
    }

    [Test]
    public async Task SignOutAsync_SignsOutAndStopsSupervisor()
    {
        var authFlow = new FakeAuthFlow();
        var supervisor = new FakeSyncSupervisor();
        var localChanges = new FakeLocalChangeSyncCoordinator();
        var remoteChanges = new FakeRemoteChangeSyncCoordinator();
        var periodicSync = new FakePeriodicSyncCoordinator();
        SyncApplicationService service = CreateService(
            new InMemorySyncPairSettingsStore(),
            authFlow: authFlow,
            supervisor: supervisor,
            localChanges: localChanges,
            remoteChanges: remoteChanges,
            periodicSync: periodicSync);

        await service.SignOutAsync();

        Assert.Multiple(() =>
        {
            Assert.That(authFlow.SignOutCallCount, Is.EqualTo(1));
            Assert.That(supervisor.StopCallCount, Is.EqualTo(1));
            Assert.That(localChanges.StopCallCount, Is.EqualTo(1));
            Assert.That(remoteChanges.StopCallCount, Is.EqualTo(1));
            Assert.That(periodicSync.StopCallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task RestoreSessionAsync_RestoresAuthAndStartsSupervisor()
    {
        var authFlow = new FakeAuthFlow();
        var supervisor = new FakeSyncSupervisor();
        var localChanges = new FakeLocalChangeSyncCoordinator();
        var remoteChanges = new FakeRemoteChangeSyncCoordinator();
        var periodicSync = new FakePeriodicSyncCoordinator();
        SyncApplicationService service = CreateService(
            new InMemorySyncPairSettingsStore(),
            authFlow: authFlow,
            supervisor: supervisor,
            localChanges: localChanges,
            remoteChanges: remoteChanges,
            periodicSync: periodicSync);

        AuthSession session = await service.RestoreSessionAsync();

        Assert.Multiple(() =>
        {
            Assert.That(authFlow.RestoreSessionCallCount, Is.EqualTo(1));
            Assert.That(supervisor.StartCallCount, Is.EqualTo(1));
            Assert.That(localChanges.StartCallCount, Is.EqualTo(1));
            Assert.That(remoteChanges.StartCallCount, Is.EqualTo(1));
            Assert.That(periodicSync.StartCallCount, Is.EqualTo(1));
            Assert.That(session, Is.SameAs(authFlow.Session));
        });
    }

    [Test]
    public async Task StartSyncAsync_StartsSupervisorAndLocalChanges()
    {
        var supervisor = new FakeSyncSupervisor();
        var localChanges = new FakeLocalChangeSyncCoordinator();
        var remoteChanges = new FakeRemoteChangeSyncCoordinator();
        var periodicSync = new FakePeriodicSyncCoordinator();
        SyncApplicationService service = CreateService(
            new InMemorySyncPairSettingsStore(),
            supervisor: supervisor,
            localChanges: localChanges,
            remoteChanges: remoteChanges,
            periodicSync: periodicSync);

        await service.StartSyncAsync();

        Assert.Multiple(() =>
        {
            Assert.That(supervisor.StartCallCount, Is.EqualTo(1));
            Assert.That(localChanges.StartCallCount, Is.EqualTo(1));
            Assert.That(remoteChanges.StartCallCount, Is.EqualTo(1));
            Assert.That(periodicSync.StartCallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void StartSyncAsync_RollsBackStartedComponentsWhenRemoteStartupFails()
    {
        List<string> calls = [];
        var startupError = new InvalidOperationException("Remote listener failed.");
        var supervisor = new FakeSyncSupervisor(calls);
        var localChanges = new FakeLocalChangeSyncCoordinator(calls);
        var remoteChanges = new FakeRemoteChangeSyncCoordinator(calls)
        {
            StartException = startupError,
        };
        var periodicSync = new FakePeriodicSyncCoordinator(calls);
        SyncApplicationService service = CreateService(
            new InMemorySyncPairSettingsStore(),
            supervisor: supervisor,
            localChanges: localChanges,
            remoteChanges: remoteChanges,
            periodicSync: periodicSync);

        InvalidOperationException error = Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartSyncAsync())!;

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.SameAs(startupError));
            Assert.That(supervisor.StopCallCount, Is.EqualTo(1));
            Assert.That(localChanges.StopCallCount, Is.EqualTo(1));
            Assert.That(remoteChanges.StopCallCount, Is.Zero);
            Assert.That(periodicSync.StartCallCount, Is.Zero);
            Assert.That(periodicSync.StopCallCount, Is.Zero);
            Assert.That(calls, Is.EqualTo(new[]
            {
                "supervisor:start",
                "local:start",
                "remote:start",
                "local:stop",
                "supervisor:stop",
            }));
        });
    }

    [Test]
    public void RestoreSessionAsync_RollsBackStartedComponentsWhenPeriodicStartupFails()
    {
        List<string> calls = [];
        var startupError = new InvalidOperationException("Periodic sync failed.");
        var authFlow = new FakeAuthFlow();
        var supervisor = new FakeSyncSupervisor(calls);
        var localChanges = new FakeLocalChangeSyncCoordinator(calls);
        var remoteChanges = new FakeRemoteChangeSyncCoordinator(calls);
        var periodicSync = new FakePeriodicSyncCoordinator(calls)
        {
            StartException = startupError,
        };
        SyncApplicationService service = CreateService(
            new InMemorySyncPairSettingsStore(),
            authFlow: authFlow,
            supervisor: supervisor,
            localChanges: localChanges,
            remoteChanges: remoteChanges,
            periodicSync: periodicSync);

        InvalidOperationException error = Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RestoreSessionAsync())!;

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.SameAs(startupError));
            Assert.That(authFlow.RestoreSessionCallCount, Is.EqualTo(1));
            Assert.That(remoteChanges.StopCallCount, Is.EqualTo(1));
            Assert.That(localChanges.StopCallCount, Is.EqualTo(1));
            Assert.That(supervisor.StopCallCount, Is.EqualTo(1));
            Assert.That(periodicSync.StopCallCount, Is.Zero);
            Assert.That(calls, Is.EqualTo(new[]
            {
                "supervisor:start",
                "local:start",
                "remote:start",
                "periodic:start",
                "remote:stop",
                "local:stop",
                "supervisor:stop",
            }));
        });
    }

    [Test]
    public async Task StopSyncAsync_StopsLocalChangesAndSupervisor()
    {
        var supervisor = new FakeSyncSupervisor();
        var localChanges = new FakeLocalChangeSyncCoordinator();
        var remoteChanges = new FakeRemoteChangeSyncCoordinator();
        var periodicSync = new FakePeriodicSyncCoordinator();
        SyncApplicationService service = CreateService(
            new InMemorySyncPairSettingsStore(),
            supervisor: supervisor,
            localChanges: localChanges,
            remoteChanges: remoteChanges,
            periodicSync: periodicSync);

        await service.StopSyncAsync();

        Assert.Multiple(() =>
        {
            Assert.That(localChanges.StopCallCount, Is.EqualTo(1));
            Assert.That(remoteChanges.StopCallCount, Is.EqualTo(1));
            Assert.That(periodicSync.StopCallCount, Is.EqualTo(1));
            Assert.That(supervisor.StopCallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetPreferencesAsync_InitializesAndLoadsPreferences()
    {
        var preferencesStore = new FakeAppPreferencesStore();
        SyncApplicationService service = CreateService(
            new InMemorySyncPairSettingsStore(),
            preferences: preferencesStore);

        AppPreferences preferences = await service.GetPreferencesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(preferencesStore.InitializeCallCount, Is.EqualTo(1));
            Assert.That(preferences, Is.SameAs(preferencesStore.Preferences));
        });
    }

    [Test]
    public async Task SavePreferencesAsync_InitializesAndSavesPreferences()
    {
        var preferencesStore = new FakeAppPreferencesStore();
        SyncApplicationService service = CreateService(
            new InMemorySyncPairSettingsStore(),
            preferences: preferencesStore);
        var preferences = new AppPreferences
        {
            RememberedServerUrl = new Uri("https://cotton.example.test/"),
        };

        await service.SavePreferencesAsync(preferences);

        Assert.Multiple(() =>
        {
            Assert.That(preferencesStore.InitializeCallCount, Is.EqualTo(1));
            Assert.That(preferencesStore.SaveCallCount, Is.EqualTo(1));
            Assert.That(preferencesStore.SavedPreferences, Is.SameAs(preferences));
        });
    }

    [Test]
    public async Task SyncNowAsync_DelegatesToSupervisor()
    {
        var supervisor = new FakeSyncSupervisor();
        SyncApplicationService service = CreateService(
            new InMemorySyncPairSettingsStore(),
            supervisor: supervisor);
        Guid syncPairId = Guid.NewGuid();

        await service.SyncNowAsync(syncPairId);

        Assert.Multiple(() =>
        {
            Assert.That(supervisor.SyncNowCallCount, Is.EqualTo(1));
            Assert.That(supervisor.LastSyncNowPairId, Is.EqualTo(syncPairId));
        });
    }

    [Test]
    public async Task OpenFolderAsync_DelegatesToPlatformCommands()
    {
        var platformCommands = new FakePlatformCommandService();
        SyncApplicationService service = CreateService(
            new InMemorySyncPairSettingsStore(),
            platformCommands: platformCommands);

        await service.OpenFolderAsync("/home/user/Cotton");

        Assert.Multiple(() =>
        {
            Assert.That(platformCommands.OpenFolderCallCount, Is.EqualTo(1));
            Assert.That(platformCommands.LastOpenedFolder, Is.EqualTo("/home/user/Cotton"));
        });
    }

    [Test]
    public async Task OpenWebAsync_DelegatesToPlatformCommands()
    {
        var platformCommands = new FakePlatformCommandService();
        SyncApplicationService service = CreateService(
            new InMemorySyncPairSettingsStore(),
            platformCommands: platformCommands);
        var url = new Uri("https://cotton.example.test/");

        await service.OpenWebAsync(url);

        Assert.Multiple(() =>
        {
            Assert.That(platformCommands.OpenWebCallCount, Is.EqualTo(1));
            Assert.That(platformCommands.LastOpenedUrl, Is.EqualTo(url));
        });
    }

    [Test]
    public async Task SaveSyncPairAsync_PersistsValidPair()
    {
        var store = new InMemorySyncPairSettingsStore();
        var supervisor = new FakeSyncSupervisor();
        var localChanges = new FakeLocalChangeSyncCoordinator();
        var remoteChanges = new FakeRemoteChangeSyncCoordinator();
        var periodicSync = new FakePeriodicSyncCoordinator();
        SyncApplicationService service = CreateService(
            store,
            supervisor: supervisor,
            localChanges: localChanges,
            remoteChanges: remoteChanges,
            periodicSync: periodicSync);
        SyncPairSettings syncPair = CreatePair("/home/user/Cotton");

        SyncPairSaveResult result = await service.SaveSyncPairAsync(syncPair);

        SyncPairSettings? saved = await store.GetAsync(syncPair.Id);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSaved, Is.True);
            Assert.That(result.Validation.IsValid, Is.True);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.Id, Is.EqualTo(syncPair.Id));
            Assert.That(supervisor.StartCallCount, Is.Zero);
            Assert.That(localChanges.StartCallCount, Is.Zero);
            Assert.That(remoteChanges.StartCallCount, Is.Zero);
            Assert.That(periodicSync.StartCallCount, Is.Zero);
        });
    }

    [Test]
    public async Task SaveSyncPairAsync_RestartsSyncComponentsWhenSyncCoreIsRunning()
    {
        var store = new InMemorySyncPairSettingsStore();
        var supervisor = new FakeSyncSupervisor();
        var localChanges = new FakeLocalChangeSyncCoordinator();
        var remoteChanges = new FakeRemoteChangeSyncCoordinator();
        var periodicSync = new FakePeriodicSyncCoordinator();
        SyncApplicationService service = CreateService(
            store,
            supervisor: supervisor,
            localChanges: localChanges,
            remoteChanges: remoteChanges,
            periodicSync: periodicSync);
        await service.StartSyncAsync();

        SyncPairSaveResult result = await service.SaveSyncPairAsync(CreatePair("/home/user/Cotton"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSaved, Is.True);
            Assert.That(supervisor.StopCallCount, Is.EqualTo(1));
            Assert.That(localChanges.StopCallCount, Is.EqualTo(1));
            Assert.That(remoteChanges.StopCallCount, Is.EqualTo(1));
            Assert.That(periodicSync.StopCallCount, Is.EqualTo(1));
            Assert.That(supervisor.StartCallCount, Is.EqualTo(2));
            Assert.That(localChanges.StartCallCount, Is.EqualTo(2));
            Assert.That(remoteChanges.StartCallCount, Is.EqualTo(2));
            Assert.That(periodicSync.StartCallCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task SaveSyncPairAsync_DoesNotRestartSyncComponentsWhenValidationFails()
    {
        var store = new InMemorySyncPairSettingsStore();
        var supervisor = new FakeSyncSupervisor();
        var localChanges = new FakeLocalChangeSyncCoordinator();
        var remoteChanges = new FakeRemoteChangeSyncCoordinator();
        var periodicSync = new FakePeriodicSyncCoordinator();
        SyncApplicationService service = CreateService(
            store,
            supervisor: supervisor,
            localChanges: localChanges,
            remoteChanges: remoteChanges,
            periodicSync: periodicSync);
        await service.StartSyncAsync();
        SyncPairSettings syncPair = CreatePair("/home/user/Cotton");
        syncPair.Mode = SyncPairMode.VirtualFilesPlaceholder;

        SyncPairSaveResult result = await service.SaveSyncPairAsync(syncPair);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSaved, Is.False);
            Assert.That(supervisor.StopCallCount, Is.Zero);
            Assert.That(localChanges.StopCallCount, Is.Zero);
            Assert.That(remoteChanges.StopCallCount, Is.Zero);
            Assert.That(periodicSync.StopCallCount, Is.Zero);
            Assert.That(supervisor.StartCallCount, Is.EqualTo(1));
            Assert.That(localChanges.StartCallCount, Is.EqualTo(1));
            Assert.That(remoteChanges.StartCallCount, Is.EqualTo(1));
            Assert.That(periodicSync.StartCallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SaveSyncPairAsync_RejectsOverlappingPairWithoutPersisting()
    {
        var store = new InMemorySyncPairSettingsStore();
        SyncApplicationService service = CreateService(store);
        SyncPairSettings existing = CreatePair("/home/user/Cotton");
        SyncPairSettings overlapping = CreatePair("/home/user/Cotton/Work");
        await service.SaveSyncPairAsync(existing);

        SyncPairSaveResult result = await service.SaveSyncPairAsync(overlapping);

        IReadOnlyList<SyncPairSettings> savedPairs = await store.ListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSaved, Is.False);
            Assert.That(result.Validation.IsValid, Is.False);
            Assert.That(
                result.Validation.Errors.Select(error => error.Issue),
                Does.Contain(SyncPairValidationIssue.OverlappingLocalRoots));
            Assert.That(savedPairs.Select(pair => pair.Id), Is.EqualTo(new[] { existing.Id }));
        });
    }

    [Test]
    public async Task SaveSyncPairAsync_UpdatesExistingPairWithoutSelfOverlap()
    {
        var store = new InMemorySyncPairSettingsStore();
        SyncApplicationService service = CreateService(store);
        SyncPairSettings existing = CreatePair("/home/user/Cotton");
        await service.SaveSyncPairAsync(existing);
        existing.DisplayName = "Cotton Documents";
        existing.LocalRootPath = "/home/user/Cotton/";

        SyncPairSaveResult result = await service.SaveSyncPairAsync(existing);

        SyncPairSettings? saved = await store.GetAsync(existing.Id);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSaved, Is.True);
            Assert.That(result.Validation.IsValid, Is.True);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.DisplayName, Is.EqualTo("Cotton Documents"));
            Assert.That(saved.LocalRootPath, Is.EqualTo("/home/user/Cotton/"));
        });
    }

    [Test]
    public async Task SaveSyncPairAsync_RejectsPrerequisiteFailureWithoutPersisting()
    {
        var store = new InMemorySyncPairSettingsStore();
        SyncPairSettings syncPair = CreatePair("/home/user/Cotton");
        var prerequisites = new FakeSyncPairPrerequisiteValidator([
            new SyncPairValidationError(
                SyncPairValidationIssue.LocalRootUnavailable,
                syncPair.Id,
                null,
                "Local root unavailable."),
        ]);
        SyncApplicationService service = CreateService(store, prerequisites);

        SyncPairSaveResult result = await service.SaveSyncPairAsync(syncPair);

        SyncPairSettings? saved = await store.GetAsync(syncPair.Id);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSaved, Is.False);
            Assert.That(result.Validation.Errors.Select(error => error.Issue), Is.EqualTo(new[]
            {
                SyncPairValidationIssue.LocalRootUnavailable,
            }));
            Assert.That(saved, Is.Null);
        });
    }

    [Test]
    public async Task SaveSyncPairAsync_SkipsPrerequisitesForDisabledPair()
    {
        var store = new InMemorySyncPairSettingsStore();
        SyncPairSettings syncPair = CreatePair("/home/user/Cotton");
        syncPair.IsEnabled = false;
        var prerequisites = new FakeSyncPairPrerequisiteValidator([
            new SyncPairValidationError(
                SyncPairValidationIssue.LocalRootUnavailable,
                syncPair.Id,
                null,
                "Local root unavailable."),
        ]);
        SyncApplicationService service = CreateService(store, prerequisites);

        SyncPairSaveResult result = await service.SaveSyncPairAsync(syncPair);

        SyncPairSettings? saved = await store.GetAsync(syncPair.Id);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSaved, Is.True);
            Assert.That(prerequisites.CallCount, Is.Zero);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.IsEnabled, Is.False);
        });
    }

    [Test]
    public async Task SaveSyncPairAsync_SkipsPrerequisitesWhenOnlyDisplayNameChanges()
    {
        var store = new InMemorySyncPairSettingsStore();
        SyncPairSettings syncPair = CreatePair("/home/user/Cotton");
        await store.UpsertAsync(syncPair);
        var prerequisites = new FakeSyncPairPrerequisiteValidator([
            new SyncPairValidationError(
                SyncPairValidationIssue.LocalRootUnavailable,
                syncPair.Id,
                null,
                "Local root unavailable."),
        ]);
        SyncApplicationService service = CreateService(store, prerequisites);
        SyncPairSettings renamed = CopySyncPair(syncPair);
        renamed.DisplayName = "Renamed documents";
        renamed.UpdatedAtUtc = DateTime.UtcNow;

        SyncPairSaveResult result = await service.SaveSyncPairAsync(renamed);

        SyncPairSettings? saved = await store.GetAsync(syncPair.Id);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSaved, Is.True);
            Assert.That(prerequisites.CallCount, Is.Zero);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.DisplayName, Is.EqualTo("Renamed documents"));
        });
    }

    [Test]
    public async Task SaveSyncPairAsync_DoesNotDeletePersistedSyncStateWhenOnlyDisplayNameChanges()
    {
        var store = new InMemorySyncPairSettingsStore();
        var syncStateStore = new FakeSyncStateStore();
        SyncPairSettings syncPair = CreatePair("/home/user/Cotton");
        await store.UpsertAsync(syncPair);
        SyncApplicationService service = CreateService(store, syncStateStore: syncStateStore);
        SyncPairSettings renamed = CopySyncPair(syncPair);
        renamed.DisplayName = "Renamed documents";
        renamed.UpdatedAtUtc = DateTime.UtcNow;

        SyncPairSaveResult result = await service.SaveSyncPairAsync(renamed);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSaved, Is.True);
            Assert.That(syncStateStore.InitializeCallCount, Is.Zero);
            Assert.That(syncStateStore.DeletedSyncPairIds, Is.Empty);
        });
    }

    [Test]
    public async Task SaveSyncPairAsync_DeletesPersistedSyncStateWhenSyncRootChanges()
    {
        var store = new InMemorySyncPairSettingsStore();
        var syncStateStore = new FakeSyncStateStore();
        SyncPairSettings syncPair = CreatePair("/home/user/Cotton");
        await store.UpsertAsync(syncPair);
        SyncApplicationService service = CreateService(store, syncStateStore: syncStateStore);
        SyncPairSettings moved = CopySyncPair(syncPair);
        moved.LocalRootPath = "/home/user/Cotton Documents";
        moved.UpdatedAtUtc = DateTime.UtcNow;

        SyncPairSaveResult result = await service.SaveSyncPairAsync(moved);

        SyncPairSettings? saved = await store.GetAsync(syncPair.Id);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSaved, Is.True);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.LocalRootPath, Is.EqualTo("/home/user/Cotton Documents"));
            Assert.That(syncStateStore.InitializeCallCount, Is.EqualTo(1));
            Assert.That(syncStateStore.DeletedSyncPairIds, Is.EqualTo(new[] { syncPair.Id.ToString() }));
        });
    }

    [Test]
    public async Task SaveSyncPairAsync_DeletesPersistedSyncStateWhenRemoteRootChanges()
    {
        var store = new InMemorySyncPairSettingsStore();
        var syncStateStore = new FakeSyncStateStore();
        SyncPairSettings syncPair = CreatePair("/home/user/Cotton");
        await store.UpsertAsync(syncPair);
        SyncApplicationService service = CreateService(store, syncStateStore: syncStateStore);
        SyncPairSettings moved = CopySyncPair(syncPair);
        moved.RemoteRootNodeId = Guid.NewGuid();
        moved.RemoteDisplayPath = "/Documents Archive";
        moved.UpdatedAtUtc = DateTime.UtcNow;

        SyncPairSaveResult result = await service.SaveSyncPairAsync(moved);

        SyncPairSettings? saved = await store.GetAsync(syncPair.Id);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSaved, Is.True);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.RemoteDisplayPath, Is.EqualTo("/Documents Archive"));
            Assert.That(syncStateStore.InitializeCallCount, Is.EqualTo(1));
            Assert.That(syncStateStore.DeletedSyncPairIds, Is.EqualTo(new[] { syncPair.Id.ToString() }));
        });
    }

    [Test]
    public async Task SaveSyncPairAsync_ValidatesPrerequisitesWhenDisabledPairIsEnabled()
    {
        var store = new InMemorySyncPairSettingsStore();
        SyncPairSettings syncPair = CreatePair("/home/user/Cotton");
        syncPair.IsEnabled = false;
        await store.UpsertAsync(syncPair);
        var prerequisites = new FakeSyncPairPrerequisiteValidator([
            new SyncPairValidationError(
                SyncPairValidationIssue.LocalRootUnavailable,
                syncPair.Id,
                null,
                "Local root unavailable."),
        ]);
        SyncApplicationService service = CreateService(store, prerequisites);
        SyncPairSettings enabled = CopySyncPair(syncPair);
        enabled.IsEnabled = true;

        SyncPairSaveResult result = await service.SaveSyncPairAsync(enabled);

        SyncPairSettings? saved = await store.GetAsync(syncPair.Id);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSaved, Is.False);
            Assert.That(prerequisites.CallCount, Is.EqualTo(1));
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.IsEnabled, Is.False);
        });
    }

    [Test]
    public async Task SaveSyncPairAsync_SkipsPrerequisitesWhenStructuralValidationFails()
    {
        var store = new InMemorySyncPairSettingsStore();
        var prerequisites = new FakeSyncPairPrerequisiteValidator([]);
        SyncApplicationService service = CreateService(store, prerequisites);
        SyncPairSettings existing = CreatePair("/home/user/Cotton");
        SyncPairSettings overlapping = CreatePair("/home/user/Cotton/Work");
        await service.SaveSyncPairAsync(existing);

        await service.SaveSyncPairAsync(overlapping);

        Assert.That(prerequisites.CallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task DeleteSyncPairAsync_RemovesPair()
    {
        var store = new InMemorySyncPairSettingsStore();
        SyncApplicationService service = CreateService(store);
        SyncPairSettings syncPair = CreatePair("/home/user/Cotton");
        await service.SaveSyncPairAsync(syncPair);

        await service.DeleteSyncPairAsync(syncPair.Id);

        SyncPairSettings? deleted = await store.GetAsync(syncPair.Id);
        Assert.That(deleted, Is.Null);
    }

    [Test]
    public async Task DeleteSyncPairAsync_RestartsSyncComponentsWhenSyncCoreIsRunning()
    {
        var store = new InMemorySyncPairSettingsStore();
        var supervisor = new FakeSyncSupervisor();
        var localChanges = new FakeLocalChangeSyncCoordinator();
        var remoteChanges = new FakeRemoteChangeSyncCoordinator();
        var periodicSync = new FakePeriodicSyncCoordinator();
        SyncApplicationService service = CreateService(
            store,
            supervisor: supervisor,
            localChanges: localChanges,
            remoteChanges: remoteChanges,
            periodicSync: periodicSync);
        SyncPairSettings syncPair = CreatePair("/home/user/Cotton");
        await service.SaveSyncPairAsync(syncPair);
        await service.StartSyncAsync();

        await service.DeleteSyncPairAsync(syncPair.Id);

        Assert.Multiple(() =>
        {
            Assert.That(supervisor.StopCallCount, Is.EqualTo(1));
            Assert.That(localChanges.StopCallCount, Is.EqualTo(1));
            Assert.That(remoteChanges.StopCallCount, Is.EqualTo(1));
            Assert.That(periodicSync.StopCallCount, Is.EqualTo(1));
            Assert.That(supervisor.StartCallCount, Is.EqualTo(2));
            Assert.That(localChanges.StartCallCount, Is.EqualTo(2));
            Assert.That(remoteChanges.StartCallCount, Is.EqualTo(2));
            Assert.That(periodicSync.StartCallCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task DeleteSyncPairAsync_DeletesPersistedSyncState()
    {
        var store = new InMemorySyncPairSettingsStore();
        var syncStateStore = new FakeSyncStateStore();
        SyncApplicationService service = CreateService(store, syncStateStore: syncStateStore);
        SyncPairSettings syncPair = CreatePair("/home/user/Cotton");
        await service.SaveSyncPairAsync(syncPair);

        await service.DeleteSyncPairAsync(syncPair.Id);

        Assert.Multiple(() =>
        {
            Assert.That(syncStateStore.InitializeCallCount, Is.EqualTo(1));
            Assert.That(syncStateStore.DeletedSyncPairIds, Is.EqualTo(new[] { syncPair.Id.ToString() }));
        });
    }

    [Test]
    public async Task ListSyncPairsAsync_InitializesStore()
    {
        var store = new InMemorySyncPairSettingsStore();
        SyncApplicationService service = CreateService(store);

        await service.ListSyncPairsAsync();

        Assert.That(store.InitializeCallCount, Is.EqualTo(1));
    }

    private static SyncApplicationService CreateService(
        ISyncPairSettingsStore store,
        ISyncPairPrerequisiteValidator? prerequisites = null,
        IAppPreferencesStore? preferences = null,
        IAuthFlow? authFlow = null,
        ISyncSupervisor? supervisor = null,
        IPlatformCommandService? platformCommands = null,
        ILocalChangeSyncCoordinator? localChanges = null,
        IRemoteChangeSyncCoordinator? remoteChanges = null,
        IPeriodicSyncCoordinator? periodicSync = null,
        ISyncStateStore? syncStateStore = null)
    {
        return new SyncApplicationService(
            store,
            prerequisites ?? new FakeSyncPairPrerequisiteValidator([]),
            preferences ?? new FakeAppPreferencesStore(),
            authFlow ?? new FakeAuthFlow(),
            supervisor ?? new FakeSyncSupervisor(),
            platformCommands ?? new FakePlatformCommandService(),
            localChanges,
            remoteChanges,
            periodicSync,
            syncStateStore);
    }

    private static SyncPairSettings CreatePair(string localRootPath)
    {
        return new SyncPairSettings
        {
            Id = Guid.NewGuid(),
            DisplayName = "Documents",
            LocalRootPath = localRootPath,
            RemoteRootNodeId = Guid.NewGuid(),
            RemoteDisplayPath = "/Documents",
            IsEnabled = true,
            Mode = SyncPairMode.FullMirror,
            CreatedAtUtc = new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc),
        };
    }

    private static SyncPairSettings CopySyncPair(SyncPairSettings source)
    {
        return new SyncPairSettings
        {
            Id = source.Id,
            DisplayName = source.DisplayName,
            LocalRootPath = source.LocalRootPath,
            RemoteRootNodeId = source.RemoteRootNodeId,
            RemoteDisplayPath = source.RemoteDisplayPath,
            IsEnabled = source.IsEnabled,
            Mode = source.Mode,
            CreatedAtUtc = source.CreatedAtUtc,
            UpdatedAtUtc = source.UpdatedAtUtc,
        };
    }

    private sealed class InMemorySyncPairSettingsStore : ISyncPairSettingsStore
    {
        private readonly Dictionary<Guid, SyncPairSettings> _syncPairs = [];

        public int InitializeCallCount { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            InitializeCallCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SyncPairSettings>> ListAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SyncPairSettings> syncPairs = _syncPairs.Values
                .OrderBy(pair => pair.DisplayName, StringComparer.Ordinal)
                .ToList();
            return Task.FromResult(syncPairs);
        }

        public Task<SyncPairSettings?> GetAsync(Guid syncPairId, CancellationToken cancellationToken = default)
        {
            _syncPairs.TryGetValue(syncPairId, out SyncPairSettings? syncPair);
            return Task.FromResult(syncPair);
        }

        public Task UpsertAsync(SyncPairSettings syncPair, CancellationToken cancellationToken = default)
        {
            _syncPairs[syncPair.Id] = syncPair;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid syncPairId, CancellationToken cancellationToken = default)
        {
            _syncPairs.Remove(syncPairId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSyncStateStore : ISyncStateStore
    {
        public int InitializeCallCount { get; private set; }

        public List<string> DeletedSyncPairIds { get; } = [];

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            InitializeCallCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SyncStateEntry>> LoadPairAsync(
            string syncPairId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<SyncStateEntry>>([]);
        }

        public Task<SyncChangeCursor> GetChangeCursorAsync(
            string syncPairId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SyncChangeCursor { SyncPairId = syncPairId });
        }

        public Task<SyncStateEntry?> GetAsync(
            string syncPairId,
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<SyncStateEntry?>(null);
        }

        public Task UpsertAsync(SyncStateEntry entry, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveChangeCursorAsync(SyncChangeCursor cursor, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            string syncPairId,
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeletePairAsync(string syncPairId, CancellationToken cancellationToken = default)
        {
            DeletedSyncPairIds.Add(syncPairId);
            return Task.CompletedTask;
        }

        public Task ReplacePairAsync(
            string syncPairId,
            IReadOnlyCollection<SyncStateEntry> entries,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSyncPairPrerequisiteValidator : ISyncPairPrerequisiteValidator
    {
        private readonly IReadOnlyList<SyncPairValidationError> _errors;

        public FakeSyncPairPrerequisiteValidator(IReadOnlyList<SyncPairValidationError> errors)
        {
            _errors = errors;
        }

        public int CallCount { get; private set; }

        public Task<IReadOnlyList<SyncPairValidationError>> ValidateAsync(
            SyncPairSettings syncPair,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_errors);
        }
    }

    private sealed class FakeAppPreferencesStore : IAppPreferencesStore
    {
        public AppPreferences Preferences { get; } = new();

        public int InitializeCallCount { get; private set; }

        public int SaveCallCount { get; private set; }

        public AppPreferences? SavedPreferences { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            InitializeCallCount++;
            return Task.CompletedTask;
        }

        public Task<AppPreferences> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Preferences);
        }

        public Task SaveAsync(AppPreferences preferences, CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            SavedPreferences = preferences;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuthFlow : IAuthFlow
    {
        public AuthSession Session { get; } = new(Guid.NewGuid(), "vadim", "vadim@example.test", false);

        public int SignInCallCount { get; private set; }

        public int RestoreSessionCallCount { get; private set; }

        public int SignOutCallCount { get; private set; }

        public PasswordSignInRequest? LastSignInRequest { get; private set; }

        public Task<AuthSession> SignInAsync(
            PasswordSignInRequest request,
            CancellationToken cancellationToken = default)
        {
            SignInCallCount++;
            LastSignInRequest = request;
            return Task.FromResult(Session);
        }

        public Task<AuthSession> RestoreSessionAsync(CancellationToken cancellationToken = default)
        {
            RestoreSessionCallCount++;
            return Task.FromResult(Session);
        }

        public Task SignOutAsync(CancellationToken cancellationToken = default)
        {
            SignOutCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSyncSupervisor : ISyncSupervisor
    {
        private readonly ICollection<string>? _calls;

        public FakeSyncSupervisor(ICollection<string>? calls = null)
        {
            _calls = calls;
        }

        public IReadOnlyList<SyncPairStatus> CurrentStatuses => [];

        public Guid? LastSyncNowPairId { get; private set; }

        public int StartCallCount { get; private set; }

        public int StopCallCount { get; private set; }

        public int SyncNowCallCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCallCount++;
            _calls?.Add("supervisor:start");
            return Task.CompletedTask;
        }

        public Task SyncAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SyncNowAsync(Guid syncPairId, CancellationToken cancellationToken = default)
        {
            SyncNowCallCount++;
            LastSyncNowPairId = syncPairId;
            return Task.CompletedTask;
        }

        public Task PauseAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task PauseAsync(Guid syncPairId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ResumeAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ResumeAsync(Guid syncPairId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCallCount++;
            _calls?.Add("supervisor:stop");
            return Task.CompletedTask;
        }
    }

    private sealed class FakePlatformCommandService : IPlatformCommandService
    {
        public string? LastOpenedFolder { get; private set; }

        public Uri? LastOpenedUrl { get; private set; }

        public int OpenFolderCallCount { get; private set; }

        public int OpenWebCallCount { get; private set; }

        public Task OpenFolderAsync(string localPath, CancellationToken cancellationToken = default)
        {
            OpenFolderCallCount++;
            LastOpenedFolder = localPath;
            return Task.CompletedTask;
        }

        public Task OpenWebAsync(Uri url, CancellationToken cancellationToken = default)
        {
            OpenWebCallCount++;
            LastOpenedUrl = url;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLocalChangeSyncCoordinator : ILocalChangeSyncCoordinator
    {
        private readonly ICollection<string>? _calls;

        public FakeLocalChangeSyncCoordinator(ICollection<string>? calls = null)
        {
            _calls = calls;
        }

        public int StartCallCount { get; private set; }

        public int StopCallCount { get; private set; }

        public Exception? StartException { get; init; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCallCount++;
            _calls?.Add("local:start");
            if (StartException is not null)
            {
                throw StartException;
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCallCount++;
            _calls?.Add("local:stop");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRemoteChangeSyncCoordinator : IRemoteChangeSyncCoordinator
    {
        private readonly ICollection<string>? _calls;

        public FakeRemoteChangeSyncCoordinator(ICollection<string>? calls = null)
        {
            _calls = calls;
        }

        public int StartCallCount { get; private set; }

        public int StopCallCount { get; private set; }

        public Exception? StartException { get; init; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCallCount++;
            _calls?.Add("remote:start");
            if (StartException is not null)
            {
                throw StartException;
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCallCount++;
            _calls?.Add("remote:stop");
            return Task.CompletedTask;
        }
    }

    private sealed class FakePeriodicSyncCoordinator : IPeriodicSyncCoordinator
    {
        private readonly ICollection<string>? _calls;

        public FakePeriodicSyncCoordinator(ICollection<string>? calls = null)
        {
            _calls = calls;
        }

        public int StartCallCount { get; private set; }

        public int StopCallCount { get; private set; }

        public Exception? StartException { get; init; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCallCount++;
            _calls?.Add("periodic:start");
            if (StartException is not null)
            {
                throw StartException;
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCallCount++;
            _calls?.Add("periodic:stop");
            return Task.CompletedTask;
        }
    }
}
