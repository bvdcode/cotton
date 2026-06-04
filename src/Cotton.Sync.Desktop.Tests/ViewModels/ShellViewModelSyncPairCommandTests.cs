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
        using ShellViewModel viewModel = CreateViewModel(controller);
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
        using ShellViewModel viewModel = CreateViewModel(controller);
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
    public async Task SaveSelectedSyncPairNameCommand_PersistsTrimmedName()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        viewModel.SelectedSyncPair!.EditableDisplayName = "  Work documents  ";

        await ExecuteAsync(viewModel.SaveSelectedSyncPairNameCommand);

        SyncPairRowViewModel selected = viewModel.SelectedSyncPair!;
        Assert.Multiple(() =>
        {
            Assert.That(controller.RenamedSyncPairId, Is.EqualTo(syncPairId));
            Assert.That(controller.RenamedSyncPairDisplayName, Is.EqualTo("Work documents"));
            Assert.That(selected.DisplayName, Is.EqualTo("Work documents"));
            Assert.That(selected.EditableDisplayName, Is.EqualTo("Work documents"));
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Folder renamed"));
            Assert.That(viewModel.HasActionRequired, Is.False);
        });
    }

    [Test]
    public async Task SaveSelectedSyncPairNameCommand_RejectsEmptyName()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        viewModel.SelectedSyncPair!.EditableDisplayName = "   ";

        await ExecuteAsync(viewModel.SaveSelectedSyncPairNameCommand);

        Assert.Multiple(() =>
        {
            Assert.That(controller.RenamedSyncPairId, Is.Null);
            Assert.That(viewModel.SelectedSyncPair!.DisplayName, Is.EqualTo("Documents"));
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Action required"));
            Assert.That(viewModel.ActionRequiredMessage, Is.EqualTo("Sync folder name is required."));
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
        using ShellViewModel viewModel = CreateViewModel(controller);
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

    [Test]
    public async Task StatusChanged_UpdatesCurrentProgressText()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportStatus(new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(syncPairId, "Syncing", null, "Uploading report.txt"),
        ]));

        SyncPairRowViewModel row = viewModel.SyncPairs.Single();
        Assert.Multiple(() =>
        {
            Assert.That(row.CurrentOperation, Is.EqualTo("Uploading report.txt"));
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Syncing"));
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Documents: Uploading report.txt"));
        });
    }

    [Test]
    public async Task ActivityReported_AddsRecentActivityRow()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot());
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportActivity(new DesktopActivitySnapshot(
            "Uploaded",
            "Documents/report.txt",
            "Uploaded Documents/report.txt",
            new DateTime(2026, 6, 3, 10, 15, 0, DateTimeKind.Utc)));

        ActivityRowViewModel activity = viewModel.Activities.First();
        Assert.Multiple(() =>
        {
            Assert.That(activity.Kind, Is.EqualTo("Uploaded"));
            Assert.That(activity.Path, Is.EqualTo("Documents/report.txt"));
            Assert.That(activity.Details, Is.EqualTo("Uploaded Documents/report.txt"));
        });
    }

    [Test]
    public async Task ExportDiagnosticsCommand_AddsStatusAndRecentActivity()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot())
        {
            ExportDiagnosticsPath = "/home/vadim/.local/share/Cotton Sync/diagnostics/cotton-sync-diagnostics.zip",
        };
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        await ExecuteAsync(viewModel.ExportDiagnosticsCommand);

        ActivityRowViewModel activity = viewModel.Activities.First();
        Assert.Multiple(() =>
        {
            Assert.That(controller.ExportDiagnosticsCalls, Is.EqualTo(1));
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Diagnostics exported"));
            Assert.That(viewModel.HasActionRequired, Is.False);
            Assert.That(activity.Kind, Is.EqualTo("Diagnostics"));
            Assert.That(activity.Path, Is.EqualTo(controller.ExportDiagnosticsPath));
            Assert.That(activity.Details, Is.EqualTo("Diagnostics bundle exported"));
        });
    }

    [Test]
    public async Task ConflictActivity_AddsConflictRow()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportActivity(new DesktopActivitySnapshot(
            "Conflict",
            "Documents/report.txt",
            "Created conflict copy Documents/report.txt",
            new DateTime(2026, 6, 3, 10, 15, 0, DateTimeKind.Utc),
            syncPairId));

        ConflictRowViewModel conflict = viewModel.Conflicts.Single();
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasConflicts, Is.True);
            Assert.That(viewModel.ConflictCountLabel, Is.EqualTo("1 conflict"));
            Assert.That(viewModel.SelectedConflict, Is.SameAs(conflict));
            Assert.That(conflict.SyncPairId, Is.EqualTo(syncPairId));
            Assert.That(conflict.Path, Is.EqualTo("Documents/report.txt"));
            Assert.That(conflict.Details, Is.EqualTo("Created conflict copy Documents/report.txt"));
            Assert.That(viewModel.Activities.First().Kind, Is.EqualTo("Conflict"));
        });
    }

    [Test]
    public async Task OpenSelectedConflictCommand_OpensConflictParentFolder()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        controller.ReportActivity(new DesktopActivitySnapshot(
            "Conflict",
            "Reports/q1.txt",
            "Created conflict copy Reports/q1.txt",
            new DateTime(2026, 6, 3, 10, 15, 0, DateTimeKind.Utc),
            syncPairId));

        await ExecuteAsync(viewModel.OpenSelectedConflictCommand);

        Assert.That(controller.OpenedFolderPath, Is.EqualTo("/home/vadim/Documents/Reports"));
    }

    [Test]
    public async Task OpenSelectedConflictCommand_RejectsConflictPathOutsideSyncRoot()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        controller.ReportActivity(new DesktopActivitySnapshot(
            "Conflict",
            "../outside.txt",
            "Created conflict copy ../outside.txt",
            new DateTime(2026, 6, 3, 10, 15, 0, DateTimeKind.Utc),
            syncPairId));

        await ExecuteAsync(viewModel.OpenSelectedConflictCommand);

        Assert.That(controller.OpenedFolderPath, Is.EqualTo("/home/vadim/Documents"));
    }

    [Test]
    public void ActivityEmptyState_UpdatesWhenActivityIsReported()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot());
        using ShellViewModel viewModel = CreateViewModel(controller);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasNoActivities, Is.True);
            Assert.That(viewModel.HasActivities, Is.False);
        });

        controller.ReportActivity(new DesktopActivitySnapshot(
            "Downloaded",
            "Documents/report.txt",
            "Downloaded Documents/report.txt",
            DateTime.UtcNow));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasNoActivities, Is.False);
            Assert.That(viewModel.HasActivities, Is.True);
        });
    }

    [Test]
    public async Task ShowAddSyncPairCommand_LoadsRemoteRootFolders()
    {
        var localFolderPicker = new FakeLocalFolderPicker("/home/user/Cotton");
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot());
        controller.RemoteFoldersByPath["/"] = new DesktopRemoteFolderListSnapshot(
            "/",
            [
                new DesktopRemoteFolderSnapshot(Guid.NewGuid(), "Documents", "/Documents"),
                new DesktopRemoteFolderSnapshot(Guid.NewGuid(), "Pictures", "/Pictures"),
            ]);
        using ShellViewModel viewModel = CreateViewModel(controller, localFolderPicker: localFolderPicker);
        await viewModel.InitializeAsync();

        await ExecuteAsync(viewModel.ShowAddSyncPairCommand);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsAddSyncPairWizardVisible, Is.True);
            Assert.That(viewModel.RemoteBrowserPath, Is.EqualTo("/"));
            Assert.That(viewModel.RemoteFolderPath, Is.EqualTo("/"));
            Assert.That(viewModel.RemoteFolderSelectionLabel, Is.EqualTo("Cloud folder: /"));
            Assert.That(viewModel.RemoteFolders.Select(static folder => folder.Name), Is.EqualTo(new[] { "Documents", "Pictures" }));
            Assert.That(viewModel.SelectedRemoteFolder?.Path, Is.EqualTo("/Documents"));
            Assert.That(controller.ListRemoteFolderPaths, Is.EqualTo(new[] { "/" }));
        });
    }

    [Test]
    public async Task ShowAddSyncPairCommand_PromptsForLocalFolderAndShowsCloudStepAfterSelection()
    {
        var localFolderPicker = new FakeLocalFolderPicker("/home/user/Cotton");
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot());
        controller.RemoteFoldersByPath["/"] = new DesktopRemoteFolderListSnapshot(
            "/",
            [
                new DesktopRemoteFolderSnapshot(Guid.NewGuid(), "Documents", "/Documents"),
            ]);
        using ShellViewModel viewModel = CreateViewModel(controller, localFolderPicker: localFolderPicker);
        await viewModel.InitializeAsync();

        await ExecuteAsync(viewModel.ShowAddSyncPairCommand);

        Assert.Multiple(() =>
        {
            Assert.That(localFolderPicker.PickFolderCalls, Is.EqualTo(1));
            Assert.That(viewModel.LocalFolderPath, Is.EqualTo("/home/user/Cotton"));
            Assert.That(viewModel.IsAddSyncPairWizardVisible, Is.True);
            Assert.That(viewModel.IsAddSyncPairLocalStepVisible, Is.False);
            Assert.That(viewModel.IsAddSyncPairCloudStepVisible, Is.True);
            Assert.That(viewModel.RemoteBrowserPath, Is.EqualTo("/"));
            Assert.That(viewModel.RemoteFolderPath, Is.EqualTo("/"));
            Assert.That(viewModel.RemoteFolders.Single().Name, Is.EqualTo("Documents"));
        });
    }

    [Test]
    public async Task ShowAddSyncPairCommand_StaysOnLocalStepWhenFolderSelectionIsCanceled()
    {
        var localFolderPicker = new FakeLocalFolderPicker();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot());
        controller.RemoteFoldersByPath["/"] = new DesktopRemoteFolderListSnapshot("/", []);
        using ShellViewModel viewModel = CreateViewModel(controller, localFolderPicker: localFolderPicker);
        await viewModel.InitializeAsync();

        await ExecuteAsync(viewModel.ShowAddSyncPairCommand);

        Assert.Multiple(() =>
        {
            Assert.That(localFolderPicker.PickFolderCalls, Is.EqualTo(1));
            Assert.That(viewModel.LocalFolderPath, Is.Empty);
            Assert.That(viewModel.IsAddSyncPairWizardVisible, Is.True);
            Assert.That(viewModel.IsAddSyncPairLocalStepVisible, Is.True);
            Assert.That(viewModel.IsAddSyncPairCloudStepVisible, Is.False);
            Assert.That(viewModel.RemoteBrowserPath, Is.EqualTo("/"));
            Assert.That(viewModel.RemoteFolderPath, Is.Empty);
            Assert.That(viewModel.RemoteFolders, Is.Empty);
            Assert.That(controller.ListRemoteFolderPaths, Is.Empty);
        });
    }

    [Test]
    public async Task BrowseLocalFolderCommand_LoadsCloudStepAfterCanceledInitialPicker()
    {
        var localFolderPicker = new FakeLocalFolderPicker(null, "/home/user/Cotton");
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot());
        controller.RemoteFoldersByPath["/"] = new DesktopRemoteFolderListSnapshot(
            "/",
            [
                new DesktopRemoteFolderSnapshot(Guid.NewGuid(), "Documents", "/Documents"),
            ]);
        using ShellViewModel viewModel = CreateViewModel(controller, localFolderPicker: localFolderPicker);
        await viewModel.InitializeAsync();

        await ExecuteAsync(viewModel.ShowAddSyncPairCommand);
        await ExecuteAsync(viewModel.BrowseLocalFolderCommand);

        Assert.Multiple(() =>
        {
            Assert.That(localFolderPicker.PickFolderCalls, Is.EqualTo(2));
            Assert.That(viewModel.LocalFolderPath, Is.EqualTo("/home/user/Cotton"));
            Assert.That(viewModel.IsAddSyncPairLocalStepVisible, Is.False);
            Assert.That(viewModel.IsAddSyncPairCloudStepVisible, Is.True);
            Assert.That(viewModel.RemoteBrowserPath, Is.EqualTo("/"));
            Assert.That(viewModel.RemoteFolderPath, Is.EqualTo("/"));
            Assert.That(viewModel.RemoteFolders.Single().Name, Is.EqualTo("Documents"));
            Assert.That(controller.ListRemoteFolderPaths, Is.EqualTo(new[] { "/" }));
        });
    }


    [Test]
    public async Task OpenRemoteFolderCommand_NavigatesToSelectedCloudFolder()
    {
        Guid archiveId = Guid.NewGuid();
        var localFolderPicker = new FakeLocalFolderPicker("/home/user/Cotton");
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot());
        controller.RemoteFoldersByPath["/"] = new DesktopRemoteFolderListSnapshot(
            "/",
            [
                new DesktopRemoteFolderSnapshot(Guid.NewGuid(), "Documents", "/Documents"),
            ]);
        controller.RemoteFoldersByPath["/Documents"] = new DesktopRemoteFolderListSnapshot(
            "/Documents",
            [
                new DesktopRemoteFolderSnapshot(archiveId, "Archive", "/Documents/Archive"),
            ]);
        using ShellViewModel viewModel = CreateViewModel(controller, localFolderPicker: localFolderPicker);
        await viewModel.InitializeAsync();
        await ExecuteAsync(viewModel.ShowAddSyncPairCommand);

        await ExecuteAsync(viewModel.OpenRemoteFolderCommand);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.RemoteBrowserPath, Is.EqualTo("/Documents"));
            Assert.That(viewModel.RemoteFolderPath, Is.EqualTo("/Documents"));
            Assert.That(viewModel.RemoteFolderSelectionLabel, Is.EqualTo("Cloud folder: /Documents"));
            Assert.That(viewModel.RemoteFolders.Single().Id, Is.EqualTo(archiveId));
            Assert.That(viewModel.SelectedRemoteFolder?.Path, Is.EqualTo("/Documents/Archive"));
            Assert.That(viewModel.RemoteFolderUpCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public async Task ServerProbe_NormalizesVerifiedBareHostAndEnablesSignIn()
    {
        var controller = new FakeDesktopShellController(CreateSignedOutSnapshot())
        {
            ServerProbeResult = new DesktopServerProbeResult(
                new Uri("https://app.cottoncloud.dev/"),
                true,
                "Cotton Cloud",
                "instance-hash"),
        };
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        viewModel.ServerUrl = "app.cottoncloud.dev";
        viewModel.Username = "desktop@example.test";
        viewModel.Password = "password";

        await WaitForAsync(() => viewModel.IsServerVerified);

        Assert.Multiple(() =>
        {
            Assert.That(controller.ProbedServerUrls, Is.EqualTo(new[] { "app.cottoncloud.dev" }));
            Assert.That(viewModel.ServerUrl, Is.EqualTo("https://app.cottoncloud.dev/"));
            Assert.That(viewModel.IsServerProbeFailed, Is.False);
            Assert.That(viewModel.ServerProbeStatus, Is.EqualTo("Cotton Cloud"));
            Assert.That(viewModel.IsServerStepVisible, Is.False);
            Assert.That(viewModel.IsSignInStepVisible, Is.True);
            Assert.That(viewModel.SetupTitle, Is.EqualTo("Sign in"));
            Assert.That(viewModel.SignInCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public async Task SetupFlow_StartsWithServerStepUntilCottonServerIsVerified()
    {
        using ShellViewModel viewModel = CreateViewModel(new FakeDesktopShellController(CreateSignedOutSnapshot()));
        await viewModel.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSetupVisible, Is.True);
            Assert.That(viewModel.IsServerStepVisible, Is.True);
            Assert.That(viewModel.IsSignInStepVisible, Is.False);
            Assert.That(viewModel.SetupTitle, Is.EqualTo("Connect Cotton Sync"));
            Assert.That(viewModel.SignInCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public async Task ChangeServerCommand_ReturnsSetupFlowToServerStepAndClearsSecrets()
    {
        var controller = new FakeDesktopShellController(CreateSignedOutSnapshot())
        {
            ServerProbeResult = new DesktopServerProbeResult(
                new Uri("https://app.cottoncloud.dev/"),
                true,
                "Cotton Cloud",
                "instance-hash"),
        };
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        viewModel.ServerUrl = "app.cottoncloud.dev";
        viewModel.Password = "password";
        viewModel.TotpCode = "123456";
        await WaitForAsync(() => viewModel.IsSignInStepVisible);

        await ExecuteAsync(viewModel.ChangeServerCommand);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsServerVerified, Is.False);
            Assert.That(viewModel.IsServerStepVisible, Is.True);
            Assert.That(viewModel.IsSignInStepVisible, Is.False);
            Assert.That(viewModel.Password, Is.Empty);
            Assert.That(viewModel.TotpCode, Is.Empty);
            Assert.That(viewModel.ServerProbeStatus, Is.EqualTo("Edit server address"));
        });
    }

    [Test]
    public async Task SignInCommand_OpensAddFolderWizardWhenNoSyncPairsExist()
    {
        var controller = new FakeDesktopShellController(CreateSignedOutSnapshot())
        {
            ServerProbeResult = new DesktopServerProbeResult(
                new Uri("https://app.cottoncloud.dev/"),
                true,
                "Cotton Cloud",
                "instance-hash"),
        };
        controller.RemoteFoldersByPath["/"] = new DesktopRemoteFolderListSnapshot(
            "/",
            [
                new DesktopRemoteFolderSnapshot(Guid.NewGuid(), "Documents", "/Documents"),
            ]);
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        viewModel.ServerUrl = "app.cottoncloud.dev";
        viewModel.Username = "desktop@example.test";
        viewModel.Password = "password";
        await WaitForAsync(() => viewModel.IsSignInStepVisible);

        await ExecuteAsync(viewModel.SignInCommand);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSignedIn, Is.True);
            Assert.That(viewModel.IsAddSyncPairWizardVisible, Is.True);
            Assert.That(viewModel.IsAddSyncPairLocalStepVisible, Is.True);
            Assert.That(viewModel.RemoteBrowserPath, Is.EqualTo("/"));
            Assert.That(viewModel.RemoteFolderPath, Is.Empty);
            Assert.That(viewModel.RemoteFolders, Is.Empty);
            Assert.That(controller.ListRemoteFolderPaths, Is.Empty);
            Assert.That(controller.SignInRequest?.ServerUrl, Is.EqualTo("https://app.cottoncloud.dev/"));
        });
    }

    [Test]
    public async Task SignOutCommand_ClearsSensitiveSetupState()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(Guid.NewGuid(), "Documents", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        viewModel.Password = "password";
        viewModel.TotpCode = "123456";
        await ExecuteAsync(viewModel.ShowSettingsCommand);

        await ExecuteAsync(viewModel.SignOutCommand);

        Assert.Multiple(() =>
        {
            Assert.That(controller.SignOutCalls, Is.EqualTo(1));
            Assert.That(viewModel.IsSignedIn, Is.False);
            Assert.That(viewModel.IsSetupVisible, Is.True);
            Assert.That(viewModel.AccountName, Is.EqualTo("Signed out"));
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Signed out"));
            Assert.That(viewModel.Password, Is.Empty);
            Assert.That(viewModel.TotpCode, Is.Empty);
            Assert.That(viewModel.IsSettingsVisible, Is.False);
            Assert.That(viewModel.SignOutCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public void FutureSyncModesVisibility_UsesFeatureFlag()
    {
        using ShellViewModel hiddenViewModel = CreateViewModel(
            new FakeDesktopShellController(CreateSignedInSnapshot()),
            new DesktopFeatureFlags(false));
        using ShellViewModel visibleViewModel = CreateViewModel(
            new FakeDesktopShellController(CreateSignedInSnapshot()),
            new DesktopFeatureFlags(true));

        Assert.Multiple(() =>
        {
            Assert.That(hiddenViewModel.IsFutureSyncModesVisible, Is.False);
            Assert.That(visibleViewModel.IsFutureSyncModesVisible, Is.True);
            Assert.That(visibleViewModel.SelectedSyncModeLabel, Is.EqualTo("Full mirror"));
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

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail("Condition was not met before timeout.");
    }

    private static DesktopShellSnapshot CreateSignedOutSnapshot()
    {
        return new DesktopShellSnapshot(
            null,
            null,
            null,
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
            false,
            []);
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

        public event EventHandler<DesktopSyncStatusSnapshot>? StatusChanged;

        public event EventHandler<DesktopActivitySnapshot>? ActivityReported;

        public Guid? EnabledSyncPairId { get; private set; }

        public bool? EnabledSyncPairValue { get; private set; }

        public Guid? RemovedSyncPairId { get; private set; }

        public Guid? RenamedSyncPairId { get; private set; }

        public string? RenamedSyncPairDisplayName { get; private set; }

        public int SignOutCalls { get; private set; }

        public DesktopSelfTestSnapshot SelfTestSnapshot { get; set; } = new([]);

        public DesktopServerProbeResult? ServerProbeResult { get; set; }

        public DesktopSignInRequest? SignInRequest { get; private set; }

        public Dictionary<string, DesktopRemoteFolderListSnapshot> RemoteFoldersByPath { get; } = [];

        public List<string> ListRemoteFolderPaths { get; } = [];

        public List<string> ProbedServerUrls { get; } = [];

        public int SyncAllCalls { get; private set; }

        public int ExportDiagnosticsCalls { get; private set; }

        public string ExportDiagnosticsPath { get; set; } = "/tmp/cotton-sync-diagnostics.zip";

        public string? OpenedFolderPath { get; private set; }

        public void ReportActivity(DesktopActivitySnapshot activity)
        {
            ActivityReported?.Invoke(this, activity);
        }

        public void ReportStatus(DesktopSyncStatusSnapshot status)
        {
            StatusChanged?.Invoke(this, status);
        }

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

        public Task RenameSyncPairAsync(
            Guid syncPairId,
            string displayName,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RenamedSyncPairId = syncPairId;
            RenamedSyncPairDisplayName = displayName;
            return Task.CompletedTask;
        }

        public Task<DesktopServerProbeResult> ProbeServerAsync(
            string serverUrl,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProbedServerUrls.Add(serverUrl);
            return Task.FromResult(ServerProbeResult ?? throw new NotSupportedException());
        }

        public Task<AuthSession> SignInAsync(
            DesktopSignInRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SignInRequest = request;
            return Task.FromResult(new AuthSession(
                Guid.NewGuid(),
                request.Username,
                request.Username,
                false));
        }

        public Task<DesktopRemoteFolderListSnapshot> ListRemoteFoldersAsync(
            string remotePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ListRemoteFolderPaths.Add(remotePath);
            return Task.FromResult(RemoteFoldersByPath.GetValueOrDefault(
                remotePath,
                new DesktopRemoteFolderListSnapshot(remotePath, [])));
        }

        public Task SignOutAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SignOutCalls++;
            return Task.CompletedTask;
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
            cancellationToken.ThrowIfCancellationRequested();
            OpenedFolderPath = localPath;
            return Task.CompletedTask;
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
            cancellationToken.ThrowIfCancellationRequested();
            ExportDiagnosticsCalls++;
            return Task.FromResult(ExportDiagnosticsPath);
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private static ShellViewModel CreateViewModel(
        FakeDesktopShellController controller,
        DesktopFeatureFlags? featureFlags = null,
        FakeLocalFolderPicker? localFolderPicker = null)
    {
        return new ShellViewModel(
            controller,
            localFolderPicker ?? new FakeLocalFolderPicker(),
            new FakeDesktopNotificationService(),
            new FakeDesktopThemeService(),
            new InlineDesktopUiDispatcher(),
            featureFlags);
    }

    private sealed class FakeLocalFolderPicker : ILocalFolderPicker
    {
        private readonly Queue<string?> _selectedPaths;

        public FakeLocalFolderPicker(params string?[] selectedPaths)
        {
            _selectedPaths = new Queue<string?>(selectedPaths);
        }

        public int PickFolderCalls { get; private set; }

        public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PickFolderCalls++;
            return Task.FromResult(_selectedPaths.Count == 0 ? null : _selectedPaths.Dequeue());
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

    private sealed class InlineDesktopUiDispatcher : IDesktopUiDispatcher
    {
        public bool CheckAccess()
        {
            return true;
        }

        public void Post(Action action)
        {
            action();
        }

        public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            action();
            return Task.CompletedTask;
        }
    }
}
