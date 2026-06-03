// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Auth;
using Cotton.Sync.App.Preferences;
using Cotton.Sync.App.SyncPairs;
using Cotton.Sync.Desktop.Platform;
using Cotton.Sync.Desktop.Shell;
using Cotton.Sync.Desktop.ViewModels;

namespace Cotton.Sync.Desktop.Tests.ViewModels;

public sealed class ShellViewModelSyncPairCommandTests
{
    [Test]
    public async Task ToggleSelectedSyncPairEnabledCommand_DisablesSelectedPair()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Idle")));
        using var viewModel = new ShellViewModel(
            controller,
            new FakeLocalFolderPicker(),
            new FakeDesktopNotificationService(),
            new FakeDesktopThemeService());
        await viewModel.InitializeAsync();

        await ExecuteAsync(viewModel.ToggleSelectedSyncPairEnabledCommand);

        SyncPairRowViewModel selected = viewModel.SelectedSyncPair!;
        Assert.Multiple(() =>
        {
            Assert.That(controller.EnabledSyncPairId, Is.EqualTo(syncPairId));
            Assert.That(controller.EnabledSyncPairValue, Is.False);
            Assert.That(selected.IsEnabled, Is.False);
            Assert.That(selected.ToggleEnabledLabel, Is.EqualTo("Enable"));
            Assert.That(selected.Status, Is.EqualTo("Disabled"));
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Folder disabled"));
        });
    }

    [Test]
    public async Task RemoveSelectedSyncPairCommand_RemovesPairAndSelectsNextPair()
    {
        Guid firstSyncPairId = Guid.NewGuid();
        Guid secondSyncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshot(
                CreatePair(firstSyncPairId, "Documents", "Idle"),
                CreatePair(secondSyncPairId, "Pictures", "Idle")));
        using var viewModel = new ShellViewModel(
            controller,
            new FakeLocalFolderPicker(),
            new FakeDesktopNotificationService(),
            new FakeDesktopThemeService());
        await viewModel.InitializeAsync();

        await ExecuteAsync(viewModel.RemoveSelectedSyncPairCommand);

        Assert.Multiple(() =>
        {
            Assert.That(controller.RemovedSyncPairId, Is.EqualTo(firstSyncPairId));
            Assert.That(viewModel.SyncPairs, Has.Count.EqualTo(1));
            Assert.That(viewModel.SyncPairs.Single().Id, Is.EqualTo(secondSyncPairId));
            Assert.That(viewModel.SelectedSyncPair?.Id, Is.EqualTo(secondSyncPairId));
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Ready"));
        });
    }

    [Test]
    public async Task SyncNowCommand_RetriesActionRequiredSyncAndClearsMessage()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Error")))
        {
            SelfTestSnapshot = new DesktopSelfTestSnapshot(
            [
                new DesktopSelfTestItemSnapshot("Server", false, "Cotton server not found."),
            ]),
        };
        using var viewModel = new ShellViewModel(
            controller,
            new FakeLocalFolderPicker(),
            new FakeDesktopNotificationService(),
            new FakeDesktopThemeService());
        await viewModel.InitializeAsync();

        await ExecuteAsync(viewModel.SelfTestCommand);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasActionRequired, Is.True);
            Assert.That(viewModel.CanRetryActionRequired, Is.True);
            Assert.That(viewModel.ActionRequiredMessage, Is.EqualTo("Cotton server not found."));
        });

        await ExecuteAsync(viewModel.SyncNowCommand);

        Assert.Multiple(() =>
        {
            Assert.That(controller.SyncAllCalls, Is.EqualTo(1));
            Assert.That(viewModel.HasActionRequired, Is.False);
            Assert.That(viewModel.CanRetryActionRequired, Is.False);
            Assert.That(viewModel.ActionRequiredMessage, Is.Empty);
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Sync requested"));
        });
    }

    private static async Task ExecuteAsync(AsyncRelayCommand command)
    {
        Assert.That(command.CanExecute(null), Is.True);
        command.Execute(null);
        for (int attempt = 0; attempt < 50 && command.IsRunning; attempt++)
        {
            await Task.Delay(10);
        }

        Assert.That(command.IsRunning, Is.False);
    }

    private static DesktopShellSnapshot CreateSignedInSnapshot(params DesktopSyncPairSnapshot[] syncPairs)
    {
        return new DesktopShellSnapshot(
            null,
            "vadim@example.com",
            "vadim@example.com",
            false,
            true,
            AppThemeMode.System,
            new DesktopPlatformCapabilitySnapshot(
                "Linux",
                "test",
                "test",
                true,
                false,
                "Tray lifecycle is not supported in this test."),
            true,
            syncPairs);
    }

    private static DesktopSyncPairSnapshot CreatePair(Guid id, string displayName, string status)
    {
        return new DesktopSyncPairSnapshot(
            id,
            displayName,
            "/home/vadim/" + displayName,
            "/" + displayName,
            status,
            Guid.NewGuid());
    }

    private sealed class FakeDesktopShellController : IDesktopShellController
    {
        private readonly DesktopShellSnapshot _snapshot;

        public FakeDesktopShellController(DesktopShellSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public event EventHandler<DesktopSyncStatusSnapshot>? StatusChanged
        {
            add { }
            remove { }
        }

        public Guid? EnabledSyncPairId { get; private set; }

        public bool? EnabledSyncPairValue { get; private set; }

        public Guid? RemovedSyncPairId { get; private set; }

        public DesktopSelfTestSnapshot SelfTestSnapshot { get; set; } = new([]);

        public int SyncAllCalls { get; private set; }

        public Task<DesktopShellSnapshot> LoadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_snapshot);
        }

        public Task SetSyncPairEnabledAsync(
            Guid syncPairId,
            bool enabled,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnabledSyncPairId = syncPairId;
            EnabledSyncPairValue = enabled;
            return Task.CompletedTask;
        }

        public Task RemoveSyncPairAsync(Guid syncPairId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RemovedSyncPairId = syncPairId;
            return Task.CompletedTask;
        }

        public Task<DesktopServerProbeResult> ProbeServerAsync(
            string serverUrl,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AuthSession> SignInAsync(
            DesktopSignInRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<DesktopRemoteFolderListSnapshot> ListRemoteFoldersAsync(
            string remotePath,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SignOutAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SyncPairSettings> AddSyncPairAsync(
            DesktopSyncPairRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SyncAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SyncAllCalls++;
            return Task.CompletedTask;
        }

        public Task PauseAllAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task ResumeAllAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task OpenFolderAsync(string localPath, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task OpenWebAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SetStartWithOperatingSystemAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SetNotificationsEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SetThemeModeAsync(AppThemeMode themeMode, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<DesktopSelfTestSnapshot> RunSelfTestAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(SelfTestSnapshot);
        }

        public Task<string> ExportDiagnosticsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeLocalFolderPicker : ILocalFolderPicker
    {
        public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeDesktopNotificationService : IDesktopNotificationService
    {
        public bool IsSupported => false;

        public void Show(string title, string message)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeDesktopThemeService : IDesktopThemeService
    {
        public void Apply(AppThemeMode themeMode)
        {
        }
    }
}
