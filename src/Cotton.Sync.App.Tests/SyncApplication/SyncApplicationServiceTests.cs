// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncApplication;
using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.App.Tests.SyncApplication;

public sealed class SyncApplicationServiceTests
{
    [Test]
    public async Task SaveSyncPairAsync_PersistsValidPair()
    {
        var store = new InMemorySyncPairSettingsStore();
        SyncApplicationService service = CreateService(store);
        SyncPairSettings syncPair = CreatePair("/home/user/Cotton");

        SyncPairSaveResult result = await service.SaveSyncPairAsync(syncPair);

        SyncPairSettings? saved = await store.GetAsync(syncPair.Id);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSaved, Is.True);
            Assert.That(result.Validation.IsValid, Is.True);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.Id, Is.EqualTo(syncPair.Id));
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
    public async Task ListSyncPairsAsync_InitializesStore()
    {
        var store = new InMemorySyncPairSettingsStore();
        SyncApplicationService service = CreateService(store);

        await service.ListSyncPairsAsync();

        Assert.That(store.InitializeCallCount, Is.EqualTo(1));
    }

    private static SyncApplicationService CreateService(
        ISyncPairSettingsStore store,
        ISyncPairPrerequisiteValidator? prerequisites = null)
    {
        return new SyncApplicationService(store, prerequisites ?? new FakeSyncPairPrerequisiteValidator([]));
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
}
