// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Auth;
using Cotton.Sync.App.Preferences;
using Cotton.Sync.App.SyncPairs;
using Cotton.Sync.Desktop.Platform;
using Cotton.Sync.Desktop.Startup;

namespace Cotton.Sync.Desktop.Shell;

internal sealed class VisualSmokeShellController : IDesktopShellController
{
    private static readonly Guid DocumentsPairId = Guid.Parse("8e40c25d-7a6d-4a8c-92cf-f7b5422a7e78");
    private static readonly Guid PhotosPairId = Guid.Parse("aa0c3835-2e86-4667-8bf9-81ce3bcd2bb8");
    private readonly DesktopVisualSmokeScenario _scenario;

    private VisualSmokeShellController(DesktopVisualSmokeScenario scenario)
    {
        _scenario = scenario;
    }

    public event EventHandler<DesktopSyncStatusSnapshot>? StatusChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<DesktopActivitySnapshot>? ActivityReported
    {
        add { }
        remove { }
    }

    public event EventHandler<DesktopTransferProgressSnapshot>? TransferProgressChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<DesktopRunProgressSnapshot>? RunProgressChanged
    {
        add { }
        remove { }
    }

    public static VisualSmokeShellController Create(DesktopVisualSmokeScenario scenario)
    {
        return new VisualSmokeShellController(scenario);
    }

    public Task<DesktopShellSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTime syncedAt = DateTime.UtcNow.AddMinutes(-7);
        bool isSignedIn = _scenario != DesktopVisualSmokeScenario.SignInError;
        IReadOnlyList<DesktopSyncPairSnapshot> pairs = CreatePairs(syncedAt);

        var snapshot = new DesktopShellSnapshot(
            new Uri("https://app.cottoncloud.dev/"),
            isSignedIn ? "qa@cottoncloud.dev" : "Signed out",
            "qa@cottoncloud.dev",
            true,
            true,
            AppThemeMode.Dark,
            new DesktopDataPathSnapshot(
                Path.Combine(Path.GetTempPath(), "cotton-sync-visual-smoke"),
                Path.Combine(Path.GetTempPath(), "cotton-sync-visual-smoke", "sync-app.db"),
                Path.Combine(Path.GetTempPath(), "cotton-sync-visual-smoke", "sync-state.db"),
                Path.Combine(Path.GetTempPath(), "cotton-sync-visual-smoke", "tokens.json")),
            DesktopPlatformCapabilities.CreateSnapshot(),
            isSignedIn,
            pairs);
        return Task.FromResult(snapshot);
    }

    public Task<DesktopServerProbeResult> ProbeServerAsync(
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var url = new Uri("https://app.cottoncloud.dev/");
        return Task.FromResult(new DesktopServerProbeResult(url, true, "Cotton Cloud", "visual-smoke"));
    }

    public Task<AuthSession> SignInAsync(
        DesktopSignInRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AuthSession(
            Guid.Parse("7ab1a10f-5fa8-4e4e-8d4d-db3ea720aeef"),
            "qa@cottoncloud.dev",
            "qa@cottoncloud.dev",
            isTotpEnabled: true));
    }

    public Task<DesktopRemoteFolderListSnapshot> ListRemoteFoldersAsync(
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<DesktopRemoteFolderSnapshot> folders =
        [
            new DesktopRemoteFolderSnapshot(Guid.Parse("10a52979-ae72-42e6-8f05-c70b0a73cd20"), "Documents", "/Documents"),
            new DesktopRemoteFolderSnapshot(Guid.Parse("74b4732d-8d0b-4e39-b41b-99eb070c212f"), "Photos", "/Photos"),
            new DesktopRemoteFolderSnapshot(Guid.Parse("386f35fc-f1b7-492c-8fe0-c814144d1646"), "Projects", "/Projects"),
        ];
        return Task.FromResult(new DesktopRemoteFolderListSnapshot("/", folders));
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<SyncPairSettings> AddSyncPairAsync(
        DesktopSyncPairRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SyncPairSettings
        {
            Id = Guid.NewGuid(),
            DisplayName = "New folder",
            LocalRootPath = request.LocalFolderPath,
            RemoteRootNodeId = Guid.NewGuid(),
            RemoteDisplayPath = request.RemoteFolderPath,
            IsEnabled = true,
            Mode = SyncPairMode.FullMirror,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
    }

    public Task SetSyncPairEnabledAsync(Guid syncPairId, bool enabled, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task SetSyncPairLocalFolderAsync(
        Guid syncPairId,
        string localFolderPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<SyncPairSettings> SetSyncPairRemoteFolderAsync(
        Guid syncPairId,
        string remoteFolderPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SyncPairSettings
        {
            Id = syncPairId,
            DisplayName = "Documents",
            LocalRootPath = "/home/qa/Cotton/Documents",
            RemoteRootNodeId = Guid.NewGuid(),
            RemoteDisplayPath = remoteFolderPath,
            IsEnabled = true,
            Mode = SyncPairMode.FullMirror,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
    }

    public Task RenameSyncPairAsync(Guid syncPairId, string displayName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task RemoveSyncPairAsync(Guid syncPairId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task PauseAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task ResumeAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task OpenFolderAsync(string localPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task OpenWebAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task SetStartWithOperatingSystemAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task SetNotificationsEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task SetThemeModeAsync(AppThemeMode themeMode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<DesktopSelfTestSnapshot> RunSelfTestAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<DesktopSelfTestItemSnapshot> items =
        [
            new DesktopSelfTestItemSnapshot("Preferences database", true, "Writable"),
            new DesktopSelfTestItemSnapshot("Token storage", true, "Release-secure storage available"),
            new DesktopSelfTestItemSnapshot("Server identity", true, "Cotton Cloud"),
        ];
        return Task.FromResult(new DesktopSelfTestSnapshot(items));
    }

    public Task<string> ExportDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Path.Combine(Path.GetTempPath(), "cotton-sync-visual-smoke-diagnostics.zip"));
    }

    public Task<DesktopRemoteFolderSnapshot> CreateRemoteFolderAsync(
        string parentPath,
        string folderName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string normalizedParent = string.IsNullOrWhiteSpace(parentPath) ? "/" : parentPath;
        string normalizedName = folderName.Trim();
        string path = normalizedParent == "/"
            ? "/" + normalizedName
            : normalizedParent.TrimEnd('/') + "/" + normalizedName;
        return Task.FromResult(new DesktopRemoteFolderSnapshot(Guid.NewGuid(), normalizedName, path));
    }

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private static string CreateLocalPath(params string[] segments)
    {
        string root = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Cotton")
            : "/home/qa/Cotton";
        return segments.Aggregate(root, Path.Combine);
    }

    private IReadOnlyList<DesktopSyncPairSnapshot> CreatePairs(DateTime syncedAt)
    {
        return _scenario is DesktopVisualSmokeScenario.SignInError
            or DesktopVisualSmokeScenario.AddFolder
            or DesktopVisualSmokeScenario.EmptyDashboard
            ? []
            : CreateDashboardPairs(syncedAt);
    }

    private IReadOnlyList<DesktopSyncPairSnapshot> CreateDashboardPairs(DateTime syncedAt)
    {
        return
        [
            new DesktopSyncPairSnapshot(
                DocumentsPairId,
                "Documents",
                CreateLocalPath("Documents"),
                "/Documents",
                CreateDocumentsStatus(),
                Guid.Parse("29f81b10-b9a8-4f1d-88b0-9bdc6861b4e6"),
                syncedAt,
                1842,
                _scenario == DesktopVisualSmokeScenario.Error
                    ? DesktopActionRequiredMessageResolver.MissingDesktopSyncChangesApiMessage
                    : null),
            new DesktopSyncPairSnapshot(
                PhotosPairId,
                "Camera uploads",
                CreateLocalPath("Pictures", "Camera Uploads"),
                "/Photos/Camera Uploads",
                CreateCameraUploadsStatus(),
                Guid.Parse("c88c7b48-66a3-49dc-aee3-dd7b28614f96"),
                syncedAt.AddMinutes(-3),
                1859),
        ];
    }

    private string CreateDocumentsStatus()
    {
        return _scenario switch
        {
            DesktopVisualSmokeScenario.Error => "Error",
            DesktopVisualSmokeScenario.Progress => "Syncing",
            _ => "Idle",
        };
    }

    private string CreateCameraUploadsStatus()
    {
        return _scenario == DesktopVisualSmokeScenario.Progress
            ? "Syncing"
            : "Idle";
    }
}
