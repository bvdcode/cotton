// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Contracts.Auth;
using Cotton.Contracts.Files;
using Cotton.Contracts.Nodes;
using Cotton.Contracts.Sync;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Nodes;
using Cotton.Sdk.Sync;
using Cotton.Sync.App.Activities;
using Cotton.Sync.App.Auth;
using Cotton.Sync.App.Platform;
using Cotton.Sync.App.Preferences;
using Cotton.Sync.App.Status;
using Cotton.Sync.App.SyncApplication;
using Cotton.Sync.App.SyncPairs;
using Cotton.Sync.Desktop.Auth;
using Cotton.Sync.Desktop.Composition;
using Cotton.Sync.Desktop.Diagnostics;
using Cotton.Sync.Desktop.Platform;
using Cotton.Sync.Desktop.Shell;
using Cotton.Sync.Remote;

namespace Cotton.Sync.Desktop.Tests.Shell;

public sealed class DesktopShellControllerHostLifecycleTests
{
    private string _tempDirectory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "cotton-shell-lifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Test]
    public async Task LoadAsync_StopsPreviousRestoredHostWhenSessionIsRestoredAgain()
    {
        DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
        Uri serverUrl = new("https://cotton.example.test/");
        var preferencesStore = new SqliteAppPreferencesStore(paths.AppDatabasePath);
        await preferencesStore.InitializeAsync();
        await preferencesStore.SaveAsync(new AppPreferences
        {
            RememberedServerUrl = serverUrl,
        });
        FakeDesktopApplicationHost firstHost = FakeDesktopApplicationHost.Create(serverUrl);
        FakeDesktopApplicationHost secondHost = FakeDesktopApplicationHost.Create(serverUrl);
        var factory = new QueueingDesktopSyncApplicationFactory(firstHost.Host, secondHost.Host);
        using DesktopShellController controller = CreateController(paths, factory);

        DesktopShellSnapshot firstSnapshot = await controller.LoadAsync();
        DesktopShellSnapshot secondSnapshot = await controller.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(firstSnapshot.IsSignedIn, Is.True);
            Assert.That(secondSnapshot.IsSignedIn, Is.True);
            Assert.That(factory.CreatedServerUrls, Is.EqualTo(new[] { serverUrl, serverUrl }));
            Assert.That(firstHost.App.RestoreSessionCalls, Is.EqualTo(1));
            Assert.That(firstHost.App.StopSyncCalls, Is.EqualTo(1));
            Assert.That(firstHost.AsyncResource.DisposeAsyncCalls, Is.EqualTo(1));
            Assert.That(secondHost.App.RestoreSessionCalls, Is.EqualTo(1));
            Assert.That(secondHost.App.StopSyncCalls, Is.Zero);
            Assert.That(secondHost.AsyncResource.DisposeAsyncCalls, Is.Zero);
        });
    }

    [Test]
    public async Task DisposeAsync_StopsActiveRestoredHost()
    {
        DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
        Uri serverUrl = new("https://cotton.example.test/");
        var preferencesStore = new SqliteAppPreferencesStore(paths.AppDatabasePath);
        await preferencesStore.InitializeAsync();
        await preferencesStore.SaveAsync(new AppPreferences
        {
            RememberedServerUrl = serverUrl,
        });
        FakeDesktopApplicationHost host = FakeDesktopApplicationHost.Create(serverUrl);
        var factory = new QueueingDesktopSyncApplicationFactory(host.Host);
        DesktopShellController controller = CreateController(paths, factory);

        await controller.LoadAsync();
        await controller.DisposeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(host.App.RestoreSessionCalls, Is.EqualTo(1));
            Assert.That(host.App.StopSyncCalls, Is.EqualTo(1));
            Assert.That(host.AsyncResource.DisposeAsyncCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SignInAsync_RejectsInsecureTokenStorageBeforeCreatingHost()
    {
        DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
        var factory = new QueueingDesktopSyncApplicationFactory();
        using DesktopShellController controller = CreateController(
            paths,
            factory,
            tokenStorageCapabilities: CreateInsecureTokenStorage);

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await controller.SignInAsync(
                new DesktopSignInRequest(
                    "https://cotton.example.test/",
                    "desktop@example.test",
                    "password",
                    string.Empty)));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Does.Contain("Secure token storage is unavailable"));
            Assert.That(factory.CreatedServerUrls, Is.Empty);
        });
    }

    [Test]
    public async Task LoadAsync_SkipsSessionRestoreWhenTokenStorageIsInsecure()
    {
        DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
        Uri serverUrl = new("https://cotton.example.test/");
        var preferencesStore = new SqliteAppPreferencesStore(paths.AppDatabasePath);
        await preferencesStore.InitializeAsync();
        await preferencesStore.SaveAsync(new AppPreferences
        {
            RememberedServerUrl = serverUrl,
        });
        var factory = new QueueingDesktopSyncApplicationFactory();
        using DesktopShellController controller = CreateController(
            paths,
            factory,
            tokenStorageCapabilities: CreateInsecureTokenStorage);

        DesktopShellSnapshot snapshot = await controller.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.ServerUrl, Is.EqualTo(serverUrl));
            Assert.That(snapshot.IsSignedIn, Is.False);
            Assert.That(factory.CreatedServerUrls, Is.Empty);
        });
    }

    [Test]
    public async Task StatusChanged_ForwardsLastSuccessfulSyncTimestamp()
    {
        DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
        Uri serverUrl = new("https://cotton.example.test/");
        var preferencesStore = new SqliteAppPreferencesStore(paths.AppDatabasePath);
        await preferencesStore.InitializeAsync();
        await preferencesStore.SaveAsync(new AppPreferences
        {
            RememberedServerUrl = serverUrl,
        });
        FakeDesktopApplicationHost host = FakeDesktopApplicationHost.Create(serverUrl);
        var factory = new QueueingDesktopSyncApplicationFactory(host.Host);
        using DesktopShellController controller = CreateController(paths, factory);
        var statusEvents = new List<DesktopSyncStatusSnapshot>();
        controller.StatusChanged += (_, status) => statusEvents.Add(status);
        Guid syncPairId = Guid.NewGuid();
        DateTime completedAtUtc = new(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc);

        await controller.LoadAsync();
        host.StatusPublisher.Publish(new SyncAppStatus(
            isAuthenticated: true,
            [
                new SyncPairStatus(
                    syncPairId,
                    "Documents",
                    SyncPairRunState.Idle,
                    null,
                    null,
                    DateTime.UtcNow,
                    completedAtUtc),
            ],
            DateTime.UtcNow));

        DesktopSyncPairStatusSnapshot pairStatus = statusEvents.Last().SyncPairs.Single();
        Assert.Multiple(() =>
        {
            Assert.That(pairStatus.Id, Is.EqualTo(syncPairId));
            Assert.That(pairStatus.LastSyncedAtUtc, Is.EqualTo(completedAtUtc));
        });
    }

    private static DesktopShellController CreateController(
        DesktopAppPaths paths,
        IDesktopSyncApplicationFactory factory,
        Func<DesktopTokenStorageCapabilitySnapshot>? tokenStorageCapabilities = null)
    {
        return new DesktopShellController(
            paths,
            factory,
            new SqliteAppPreferencesStore(paths.AppDatabasePath),
            new SqliteSyncPairSettingsStore(paths.AppDatabasePath),
            new FakePlatformCommandService(),
            new FakeAutostartService(),
            tokenStorageCapabilities: tokenStorageCapabilities ?? CreateSecureTokenStorage);
    }

    private static DesktopTokenStorageCapabilitySnapshot CreateSecureTokenStorage()
    {
        return new DesktopTokenStorageCapabilitySnapshot(
            "test-secure",
            IsReleaseSecure: true,
            "Test secure token storage");
    }

    private static DesktopTokenStorageCapabilitySnapshot CreateInsecureTokenStorage()
    {
        return new DesktopTokenStorageCapabilitySnapshot(
            "restricted-file-v1",
            IsReleaseSecure: false,
            "Development fallback");
    }

    private sealed class QueueingDesktopSyncApplicationFactory : IDesktopSyncApplicationFactory
    {
        private readonly Queue<DesktopSyncApplicationHost> _hosts;

        public QueueingDesktopSyncApplicationFactory(params DesktopSyncApplicationHost[] hosts)
        {
            _hosts = new Queue<DesktopSyncApplicationHost>(hosts);
        }

        public List<Uri> CreatedServerUrls { get; } = [];

        public DesktopSyncApplicationHost Create(Uri serverUrl)
        {
            CreatedServerUrls.Add(serverUrl);
            return _hosts.Dequeue();
        }
    }

    private sealed class FakeDesktopApplicationHost
    {
        private FakeDesktopApplicationHost(Uri serverUrl)
        {
            App = new FakeSyncApplicationService();
            AsyncResource = new FakeAsyncResource();
            StatusPublisher = new InMemoryAppStatusPublisher();
            Host = new DesktopSyncApplicationHost(
                App,
                new FakeRemoteRootResolver(),
                StatusPublisher,
                new InMemoryAppActivityPublisher(),
                new FakeCottonTokenStore(),
                new FakeCottonNodeClient(),
                new FakeCottonSyncClient(),
                new HttpClient(),
                serverUrl,
                AsyncResource);
        }

        public FakeSyncApplicationService App { get; }

        public InMemoryAppStatusPublisher StatusPublisher { get; }

        public FakeAsyncResource AsyncResource { get; }

        public DesktopSyncApplicationHost Host { get; }

        public static FakeDesktopApplicationHost Create(Uri serverUrl)
        {
            return new FakeDesktopApplicationHost(serverUrl);
        }
    }

    private sealed class FakeAsyncResource : IAsyncDisposable
    {
        public int DisposeAsyncCalls { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeAsyncCalls++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeSyncApplicationService : ISyncApplicationService
    {
        public int RestoreSessionCalls { get; private set; }

        public int StopSyncCalls { get; private set; }

        public Task<AuthSession> SignInAsync(
            PasswordSignInRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateSession(request.Username));
        }

        public Task<AuthSession> RestoreSessionAsync(CancellationToken cancellationToken = default)
        {
            RestoreSessionCalls++;
            return Task.FromResult(CreateSession("restored"));
        }

        public Task SignOutAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<AppPreferences> GetPreferencesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AppPreferences());
        }

        public Task SavePreferencesAsync(AppPreferences preferences, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SyncPairSettings>> ListSyncPairsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<SyncPairSettings>>([]);
        }

        public Task<SyncPairSettings?> GetSyncPairAsync(Guid syncPairId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<SyncPairSettings?>(null);
        }

        public Task<SyncPairSaveResult> SaveSyncPairAsync(
            SyncPairSettings syncPair,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SyncPairSaveResult.Saved(new SyncPairValidationResult([])));
        }

        public Task DeleteSyncPairAsync(Guid syncPairId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task StartSyncAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SyncAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SyncNowAsync(Guid syncPairId, CancellationToken cancellationToken = default)
        {
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

        public Task StopSyncAsync(CancellationToken cancellationToken = default)
        {
            StopSyncCalls++;
            return Task.CompletedTask;
        }

        public Task OpenFolderAsync(string localPath, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task OpenWebAsync(Uri url, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        private static AuthSession CreateSession(string username)
        {
            return new AuthSession(Guid.NewGuid(), username, username + "@example.test", isTotpEnabled: false);
        }
    }

    private sealed class FakeCottonTokenStore : ICottonTokenStore
    {
        private TokenPairDto? _tokens = new()
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
        };

        public Task<TokenPairDto?> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tokens);
        }

        public Task SaveAsync(TokenPairDto tokens, CancellationToken cancellationToken = default)
        {
            _tokens = tokens;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            _tokens = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRemoteRootResolver : IRemoteRootResolver
    {
        public Task<NodeDto> EnsureAsync(string? remotePath = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeCottonNodeClient : ICottonNodeClient
    {
        public Task<NodeDto> ResolveAsync(string? path = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<NodeDto> GetAsync(Guid nodeId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<NodeContentDto> GetChildrenAsync(
            Guid nodeId,
            int page = 1,
            int pageSize = 100,
            int depth = 0,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<NodeDto> CreateAsync(Guid parentId, string name, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<NodeDto> MoveAsync(Guid nodeId, Guid parentId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<NodeDto> RenameAsync(Guid nodeId, string name, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<NodeDto> UpdateMetadataAsync(
            Guid nodeId,
            IReadOnlyDictionary<string, string> metadata,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(Guid nodeId, bool skipTrash = false, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<NodeDto> RestoreAsync(
            Guid nodeId,
            RestoreItemRequestDto? request = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<NodeDto>> GetAncestorsAsync(Guid nodeId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeCottonSyncClient : ICottonSyncClient
    {
        public Task<SyncChangesResponseDto> GetChangesAsync(
            long sinceCursor = 0,
            int limit = 500,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SyncChangesResponseDto
            {
                SinceCursor = sinceCursor,
                NextCursor = sinceCursor,
                HasMore = false,
            });
        }
    }

    private sealed class FakeAutostartService : IAutostartService
    {
        public bool IsSupported => true;

        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakePlatformCommandService : IPlatformCommandService
    {
        public Task OpenFolderAsync(string localPath, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task OpenWebAsync(Uri url, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
