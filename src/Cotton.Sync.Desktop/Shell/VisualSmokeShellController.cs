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

    public static VisualSmokeShellController Create(DesktopVisualSmokeScenario scenario)
    {
        return new VisualSmokeShellController(scenario);
    }

    public Task<DesktopShellSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTime syncedAt = DateTime.UtcNow.AddMinutes(-7);
        IReadOnlyList<DesktopSyncPairSnapshot> pairs = _scenario == DesktopVisualSmokeScenario.AddFolder
            ? []
            : CreateDashboardPairs(syncedAt);

        var snapshot = new DesktopShellSnapshot(
            new Uri("https://app.cottoncloud.dev/"),
            "qa@cottoncloud.dev",
            "qa@cottoncloud.dev",
            true,
            true,
            AppThemeMode.Dark,
            DesktopPlatformCapabilities.CreateSnapshot(),
            true,
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

    private IReadOnlyList<DesktopSyncPairSnapshot> CreateDashboardPairs(DateTime syncedAt)
    {
        return
        [
            new DesktopSyncPairSnapshot(
                DocumentsPairId,
                "Documents",
                CreateLocalPath("Documents"),
                "/Documents",
                _scenario == DesktopVisualSmokeScenario.Error ? "Error" : "Idle",
                Guid.Parse("29f81b10-b9a8-4f1d-88b0-9bdc6861b4e6"),
                syncedAt,
                1842,
                _scenario == DesktopVisualSmokeScenario.Error
                    ? "Sync API unavailable."
                    : null),
            new DesktopSyncPairSnapshot(
                PhotosPairId,
                "Camera uploads",
                CreateLocalPath("Pictures", "Camera Uploads"),
                "/Photos/Camera Uploads",
                "Idle",
                Guid.Parse("c88c7b48-66a3-49dc-aee3-dd7b28614f96"),
                syncedAt.AddMinutes(-3),
                1859),
        ];
    }
}
