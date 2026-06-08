// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Net;
using System.Reflection;
using Cotton.Sdk;
using Cotton.Sync.App.Auth;
using Cotton.Sync.App.Preferences;
using Cotton.Sync.App.SyncPairs;
using Cotton.Sync.Desktop.Platform;
using Cotton.Sync.Desktop.Shell;
using Cotton.Sync.Desktop.Startup;
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
            Assert.That(selected.IsDisabled, Is.True);
            Assert.That(selected.ToggleEnabledLabel, Is.EqualTo("Enable sync folder"));
            Assert.That(selected.Status, Is.EqualTo("Disabled"));
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Folder disabled"));
        });
    }

    [Test]
    public async Task ToggleSelectedSyncPairEnabledCommand_KeepsOtherPairsPausedWhenDisablingDuringGlobalPause()
    {
        Guid disabledSyncPairId = Guid.NewGuid();
        Guid otherSyncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshot(
                CreatePair(disabledSyncPairId, "Cloud", "Paused"),
                CreatePair(otherSyncPairId, "Videos", "Paused")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        await ExecuteAsync(viewModel.ToggleSelectedSyncPairEnabledCommand);

        SyncPairRowViewModel disabledPair = viewModel.SyncPairs.Single(pair => pair.Id == disabledSyncPairId);
        SyncPairRowViewModel otherPair = viewModel.SyncPairs.Single(pair => pair.Id == otherSyncPairId);
        Assert.Multiple(() =>
        {
            Assert.That(controller.EnabledSyncPairId, Is.EqualTo(disabledSyncPairId));
            Assert.That(controller.EnabledSyncPairValue, Is.False);
            Assert.That(disabledPair.Status, Is.EqualTo("Disabled"));
            Assert.That(disabledPair.IsEnabled, Is.False);
            Assert.That(otherPair.Status, Is.EqualTo("Paused"));
            Assert.That(otherPair.IsEnabled, Is.True);
            Assert.That(viewModel.IsSyncPaused, Is.True);
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Paused"));
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Sync is paused."));
        });
    }

    [Test]
    public async Task RemoveSelectedSyncPairCommand_RequiresConfirmationBeforeRemovingPair()
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
            Assert.That(controller.RemovedSyncPairId, Is.Null);
            Assert.That(viewModel.IsSelectedSyncPairEditorVisible, Is.True);
            Assert.That(viewModel.SelectedSyncPair?.IsEditorVisible, Is.True);
            Assert.That(viewModel.IsRemoveSyncPairConfirmationVisible, Is.True);
            Assert.That(viewModel.RemoveSyncPairConfirmationTitle, Is.EqualTo("Remove Documents?"));
            Assert.That(viewModel.RemoveSyncPairConfirmationPath, Does.EndWith("Documents"));
            Assert.That(viewModel.ConfirmRemoveSelectedSyncPairCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.RemoveSelectedSyncPairCommand.CanExecute(null), Is.False);
        });

        await ExecuteAsync(viewModel.CancelRemoveSyncPairCommand);

        Assert.Multiple(() =>
        {
            Assert.That(controller.RemovedSyncPairId, Is.Null);
            Assert.That(viewModel.IsRemoveSyncPairConfirmationVisible, Is.False);
            Assert.That(viewModel.RemoveSelectedSyncPairCommand.CanExecute(null), Is.True);
        });

        await ExecuteAsync(viewModel.RemoveSelectedSyncPairCommand);
        await ExecuteAsync(viewModel.ConfirmRemoveSelectedSyncPairCommand);

        Assert.Multiple(() =>
        {
            Assert.That(controller.RemovedSyncPairId, Is.EqualTo(firstSyncPairId));
            Assert.That(viewModel.SyncPairs, Has.Count.EqualTo(1));
            Assert.That(viewModel.SyncPairs.Single().Id, Is.EqualTo(secondSyncPairId));
            Assert.That(viewModel.SelectedSyncPair?.Id, Is.EqualTo(secondSyncPairId));
            Assert.That(viewModel.IsSelectedSyncPairEditorVisible, Is.False);
            Assert.That(viewModel.SyncPairs.Single().IsEditorVisible, Is.False);
            Assert.That(viewModel.IsRemoveSyncPairConfirmationVisible, Is.False);
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Ready"));
        });
    }

    [Test]
    public async Task ShowSelectedSyncPairEditorCommand_OpensControlsForCommandParameter()
    {
        Guid firstSyncPairId = Guid.NewGuid();
        Guid secondSyncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshot(
                CreatePair(firstSyncPairId, "Documents", "Idle"),
                CreatePair(secondSyncPairId, "Pictures", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        SyncPairRowViewModel firstPair = viewModel.SyncPairs.Single(pair => pair.Id == firstSyncPairId);
        SyncPairRowViewModel secondPair = viewModel.SyncPairs.Single(pair => pair.Id == secondSyncPairId);

        await ExecuteAsync(viewModel.ShowSelectedSyncPairEditorCommand, secondPair);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedSyncPair?.Id, Is.EqualTo(secondSyncPairId));
            Assert.That(viewModel.IsSelectedSyncPairEditorVisible, Is.True);
            Assert.That(firstPair.IsEditorVisible, Is.False);
            Assert.That(secondPair.IsEditorVisible, Is.True);
            Assert.That(viewModel.IsRemoveSyncPairConfirmationVisible, Is.False);
            Assert.That(viewModel.CancelSelectedSyncPairEditorCommand.CanExecute(null), Is.True);
        });

        await ExecuteAsync(viewModel.ShowSelectedSyncPairEditorCommand, secondPair);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedSyncPair?.Id, Is.EqualTo(secondSyncPairId));
            Assert.That(viewModel.IsSelectedSyncPairEditorVisible, Is.False);
            Assert.That(secondPair.IsEditorVisible, Is.False);
            Assert.That(viewModel.CancelSelectedSyncPairEditorCommand.CanExecute(null), Is.False);
        });

        await ExecuteAsync(viewModel.ShowSelectedSyncPairEditorCommand, secondPair);
        await ExecuteAsync(viewModel.CancelSelectedSyncPairEditorCommand);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSelectedSyncPairEditorVisible, Is.False);
            Assert.That(secondPair.IsEditorVisible, Is.False);
            Assert.That(viewModel.CancelSelectedSyncPairEditorCommand.CanExecute(null), Is.False);
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
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Fix the issue below to continue syncing."));
        });
    }

    [Test]
    public async Task ChangeSelectedSyncPairLocalFolderCommand_PersistsSelectedFolder()
    {
        Guid syncPairId = Guid.NewGuid();
        var localFolderPicker = new FakeLocalFolderPicker("  /home/vadim/New Documents  ");
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(
            syncPairId,
            "Documents",
            "Idle",
            localPath: "/home/vadim/Documents")));
        using ShellViewModel viewModel = CreateViewModel(controller, localFolderPicker: localFolderPicker);
        await viewModel.InitializeAsync();

        await ExecuteAsync(viewModel.ChangeSelectedSyncPairLocalFolderCommand);

        Assert.Multiple(() =>
        {
            Assert.That(localFolderPicker.PickFolderCalls, Is.EqualTo(1));
            Assert.That(controller.LocalFolderSyncPairId, Is.EqualTo(syncPairId));
            Assert.That(controller.LocalFolderPath, Is.EqualTo("/home/vadim/New Documents"));
            Assert.That(viewModel.SelectedSyncPair!.LocalPath, Is.EqualTo("/home/vadim/New Documents"));
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Folder updated"));
            Assert.That(viewModel.HasActionRequired, Is.False);
            Assert.That(viewModel.Activities.First().Kind, Is.EqualTo("Pair"));
            Assert.That(viewModel.Activities.First().Path, Is.EqualTo("/home/vadim/New Documents"));
            Assert.That(viewModel.Activities.First().Details, Is.EqualTo("Local folder changed"));
        });
    }

    [Test]
    public async Task ChangeSelectedSyncPairLocalFolderCommand_RejectsOverlappingFolder()
    {
        Guid firstSyncPairId = Guid.NewGuid();
        Guid secondSyncPairId = Guid.NewGuid();
        var localFolderPicker = new FakeLocalFolderPicker("/home/vadim/Pictures/Raw");
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshot(
                CreatePair(firstSyncPairId, "Documents", "Idle", localPath: "/home/vadim/Documents"),
                CreatePair(secondSyncPairId, "Pictures", "Idle", localPath: "/home/vadim/Pictures")));
        using ShellViewModel viewModel = CreateViewModel(controller, localFolderPicker: localFolderPicker);
        await viewModel.InitializeAsync();

        await ExecuteAsync(viewModel.ChangeSelectedSyncPairLocalFolderCommand);

        Assert.Multiple(() =>
        {
            Assert.That(localFolderPicker.PickFolderCalls, Is.EqualTo(1));
            Assert.That(controller.LocalFolderSyncPairId, Is.Null);
            Assert.That(controller.LocalFolderPath, Is.Null);
            Assert.That(viewModel.SelectedSyncPair!.LocalPath, Is.EqualTo("/home/vadim/Documents"));
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Action required"));
            Assert.That(viewModel.HasActionRequired, Is.True);
            Assert.That(viewModel.ActionRequiredMessage, Is.EqualTo("Sync folders cannot be inside each other."));
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Fix the issue below to continue syncing."));
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
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Fix the issue below to continue syncing."));
        });

        await ExecuteAsync(viewModel.SyncNowCommand);

        Assert.Multiple(() =>
        {
            Assert.That(controller.SyncAllCalls, Is.EqualTo(1));
            Assert.That(viewModel.HasActionRequired, Is.False);
            Assert.That(viewModel.CanRetryActionRequired, Is.False);
            Assert.That(viewModel.ActionRequiredMessage, Is.Empty);
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Checked for changes"));
        });
    }

    [Test]
    public async Task Initialize_TreatsSyncPairErrorAsAttentionBeforeErrorMessageIsResolved()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(Guid.NewGuid(), "Videos", "Error")));
        using ShellViewModel viewModel = CreateViewModel(controller);

        await viewModel.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasActionRequired, Is.False);
            Assert.That(viewModel.HasStatusAttention, Is.True);
            Assert.That(viewModel.HeaderStatusText, Is.EqualTo("Action required"));
            Assert.That(viewModel.IsStatusCardVisible, Is.True);
            Assert.That(viewModel.StatusCardTitle, Is.EqualTo("Sync needs attention"));
            Assert.That(viewModel.StatusCardDetailText, Is.EqualTo("Fix the folder issue to continue syncing."));
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Fix the folder issue to continue syncing."));
        });
    }

    [Test]
    public async Task SelfTestPass_PreservesCurrentSyncPairErrorActionRequired()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Videos", "Idle")))
        {
            SelfTestSnapshot = new DesktopSelfTestSnapshot(
            [
                new DesktopSelfTestItemSnapshot("Preferences database", true, "Ready"),
                new DesktopSelfTestItemSnapshot("Sync state database", true, "Ready"),
            ]),
        };
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportStatus(new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(syncPairId, "Error", "There is not enough space on the disk."),
        ]));

        await ExecuteAsync(viewModel.SelfTestCommand);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Action required"));
            Assert.That(viewModel.HeaderStatusText, Is.EqualTo("Action required"));
            Assert.That(viewModel.HasActionRequired, Is.True);
            Assert.That(viewModel.IsStatusCardVisible, Is.False);
            Assert.That(
                viewModel.ActionRequiredMessage,
                Is.EqualTo("This computer does not have enough free disk space for sync. Free space and retry."));
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Fix the issue below to continue syncing."));
        });
    }

    [Test]
    public async Task CommandFailure_UpdatesProgressTextInsteadOfReportingUpToDate()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(Guid.NewGuid(), "Documents", "Idle")))
        {
            SyncAllException = new CottonApiException(
                HttpStatusCode.OK,
                "<!doctype html><html>App</html>",
                "Cotton API request GET /api/v1/sync/changes?since=0&limit=500 returned invalid JSON "
                + "with content type 'text/html' and status 200 (OK)."),
        };
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        viewModel.SyncNowCommand.Execute(null);
        await WaitForAsync(() => viewModel.HasActionRequired);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Action required"));
            Assert.That(viewModel.IsStatusCardVisible, Is.False);
            Assert.That(
                viewModel.ActionRequiredMessage,
                Is.EqualTo("This Cotton server does not expose the desktop sync changes API yet. Deploy the latest Cotton backend and retry sync."));
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Fix the issue below to continue syncing."));
        });
    }

    [Test]
    public async Task ApplyVisualSmokeScenarioAsync_ShowsSettings()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(Guid.NewGuid(), "Documents", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        await viewModel.ApplyVisualSmokeScenarioAsync(DesktopVisualSmokeScenario.Settings);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSettingsVisible, Is.True);
            Assert.That(viewModel.IsDashboardChromeVisible, Is.False);
            Assert.That(viewModel.SelectedSettingsTabIndex, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task ApplyVisualSmokeScenarioAsync_ShowsSignInError()
    {
        using ShellViewModel viewModel = CreateViewModel(new FakeDesktopShellController(CreateSignedOutSnapshot()));
        await viewModel.InitializeAsync();

        await viewModel.ApplyVisualSmokeScenarioAsync(DesktopVisualSmokeScenario.SignInError);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSetupVisible, Is.True);
            Assert.That(viewModel.IsSignInStepVisible, Is.True);
            Assert.That(viewModel.IsSignedIn, Is.False);
            Assert.That(viewModel.ServerUrl, Is.EqualTo("https://app.cottoncloud.dev/"));
            Assert.That(viewModel.Username, Is.EqualTo("qa@cottoncloud.dev"));
            Assert.That(viewModel.Password, Is.Not.Empty);
            Assert.That(viewModel.TotpCode, Is.EqualTo("000000"));
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Sign-in failed"));
            Assert.That(viewModel.HasActionRequired, Is.True);
            Assert.That(viewModel.CanRetryActionRequired, Is.False);
            Assert.That(viewModel.ActionRequiredMessage, Is.EqualTo("Invalid username or password."));
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Sign in to continue."));
        });
    }

    [Test]
    public async Task ApplyVisualSmokeScenarioAsync_ShowsAddFolderWizard()
    {
        var localFolderPicker = new FakeLocalFolderPicker();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot());
        controller.RemoteFoldersByPath["/"] = new DesktopRemoteFolderListSnapshot(
            "/",
            [
                new DesktopRemoteFolderSnapshot(Guid.NewGuid(), "Documents", "/Documents"),
                new DesktopRemoteFolderSnapshot(Guid.NewGuid(), "Photos", "/Photos"),
            ]);
        using ShellViewModel viewModel = CreateViewModel(controller, localFolderPicker: localFolderPicker);
        await viewModel.InitializeAsync();

        await viewModel.ApplyVisualSmokeScenarioAsync(DesktopVisualSmokeScenario.AddFolder);

        Assert.Multiple(() =>
        {
            Assert.That(localFolderPicker.PickFolderCalls, Is.EqualTo(0));
            Assert.That(viewModel.IsAddSyncPairWizardVisible, Is.True);
            Assert.That(viewModel.IsAddSyncPairLocalStepVisible, Is.False);
            Assert.That(viewModel.IsAddSyncPairCloudStepVisible, Is.True);
            Assert.That(viewModel.LocalFolderPath, Is.Not.Empty);
            Assert.That(viewModel.RemoteBrowserPath, Is.EqualTo("/"));
            Assert.That(viewModel.RemoteFolderPath, Is.EqualTo("/"));
            Assert.That(viewModel.RemoteFolders.Select(static folder => folder.Name), Is.EqualTo(new[] { "Documents", "Photos" }));
            Assert.That(viewModel.SelectedRemoteFolder, Is.Null);
            Assert.That(controller.ListRemoteFolderPaths, Is.EqualTo(new[] { "/" }));
        });
    }

    [Test]
    public async Task ApplyVisualSmokeScenarioAsync_ShowsEmptyDashboardWithoutOpeningWizard()
    {
        var localFolderPicker = new FakeLocalFolderPicker();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot());
        using ShellViewModel viewModel = CreateViewModel(controller, localFolderPicker: localFolderPicker);
        await viewModel.InitializeAsync();

        await viewModel.ApplyVisualSmokeScenarioAsync(DesktopVisualSmokeScenario.EmptyDashboard);

        Assert.Multiple(() =>
        {
            Assert.That(localFolderPicker.PickFolderCalls, Is.EqualTo(0));
            Assert.That(viewModel.IsSignedIn, Is.True);
            Assert.That(viewModel.SyncPairs, Is.Empty);
            Assert.That(viewModel.IsAddSyncPairWizardVisible, Is.False);
            Assert.That(viewModel.IsStatusCardVisible, Is.False);
            Assert.That(viewModel.CurrentProgressText, Is.Empty);
            Assert.That(viewModel.HeaderTitleText, Is.EqualTo("vadim@example.com"));
            Assert.That(viewModel.HeaderStatusText, Is.EqualTo("Connected"));
        });
    }

    [Test]
    public async Task ApplyVisualSmokeScenarioAsync_ShowsSettingsDiagnosticsTab()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(Guid.NewGuid(), "Documents", "Idle")))
        {
            SelfTestSnapshot = new DesktopSelfTestSnapshot(
            [
                new DesktopSelfTestItemSnapshot("Preferences database", true, "Writable"),
                new DesktopSelfTestItemSnapshot("Token storage", true, "Release-secure storage available"),
            ]),
        };
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        await viewModel.ApplyVisualSmokeScenarioAsync(DesktopVisualSmokeScenario.SettingsDiagnostics);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSettingsVisible, Is.True);
            Assert.That(viewModel.IsDashboardChromeVisible, Is.False);
            Assert.That(viewModel.SelectedSettingsTabIndex, Is.EqualTo(3));
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Diagnostics exported"));
            Assert.That(viewModel.HasSelfTestItems, Is.True);
            Assert.That(viewModel.SelfTestItems, Has.Count.EqualTo(2));
            Assert.That(viewModel.HasLastDiagnosticsBundlePath, Is.True);
            Assert.That(viewModel.LastDiagnosticsBundlePath, Is.EqualTo(controller.ExportDiagnosticsPath));
        });
    }

    [Test]
    public async Task ApplyVisualSmokeScenarioAsync_ShowsActionRequiredError()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(Guid.NewGuid(), "Documents", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        await viewModel.ApplyVisualSmokeScenarioAsync(DesktopVisualSmokeScenario.Error);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Action required"));
            Assert.That(viewModel.IsStatusCardVisible, Is.False);
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Fix the issue below to continue syncing."));
            Assert.That(
                viewModel.ActionRequiredMessage,
                Is.EqualTo("This Cotton server does not expose the desktop sync changes API yet. Deploy the latest Cotton backend and retry sync."));
        });
    }

    [Test]
    public async Task ApplyVisualSmokeScenarioAsync_ShowsProgressCards()
    {
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshot(
                CreatePair(Guid.NewGuid(), "Documents", "Syncing"),
                CreatePair(Guid.NewGuid(), "Camera uploads", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        await viewModel.ApplyVisualSmokeScenarioAsync(DesktopVisualSmokeScenario.Progress);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Syncing"));
            Assert.That(viewModel.HasCurrentRunProgress, Is.True);
            Assert.That(viewModel.CurrentRunProgressTitle, Is.EqualTo("Syncing 2 folders"));
            Assert.That(viewModel.CurrentRunProgressDetails, Is.EqualTo("10 of 40 files across 2 folders"));
            Assert.That(viewModel.HasCurrentTransfer, Is.True);
            Assert.That(viewModel.CurrentTransferTitle, Is.EqualTo("Camera uploads: Downloading 07.7z"));
            Assert.That(viewModel.CurrentTransferDetails, Does.Contain("/s"));
            Assert.That(viewModel.CurrentTransferDetails, Does.Contain("left"));
            Assert.That(viewModel.CurrentWorkProgressTitle, Is.EqualTo("Syncing 2 folders"));
            Assert.That(viewModel.CurrentWorkProgressHeaderDetails, Is.EqualTo("7.0 MB · 4.0 MB/s"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("10 of 40 files across 2 folders"));
            Assert.That(viewModel.CurrentWorkProgressSecondaryDetails, Is.Empty);
        });
    }

    [Test]
    public async Task ApplyVisualSmokeScenarioAsync_ShowsFolderControls()
    {
        Guid firstPairId = Guid.NewGuid();
        Guid secondPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshot(
                CreatePair(firstPairId, "Documents", "Idle"),
                CreatePair(secondPairId, "Photos", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        await viewModel.ApplyVisualSmokeScenarioAsync(DesktopVisualSmokeScenario.FolderControls);

        SyncPairRowViewModel firstPair = viewModel.SyncPairs.Single(pair => pair.Id == firstPairId);
        SyncPairRowViewModel secondPair = viewModel.SyncPairs.Single(pair => pair.Id == secondPairId);
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSelectedSyncPairEditorVisible, Is.True);
            Assert.That(viewModel.SelectedSyncPair?.Id, Is.EqualTo(firstPairId));
            Assert.That(firstPair.IsEditorVisible, Is.True);
            Assert.That(secondPair.IsEditorVisible, Is.False);
            Assert.That(viewModel.IsRemoveSyncPairConfirmationVisible, Is.False);
        });
    }

    [Test]
    public async Task ApplyVisualSmokeScenarioAsync_ShowsConflictList()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(Guid.NewGuid(), "Documents", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        await viewModel.ApplyVisualSmokeScenarioAsync(DesktopVisualSmokeScenario.Conflict);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasConflicts, Is.True);
            Assert.That(viewModel.ConflictCountLabel, Is.EqualTo("1 conflict"));
            Assert.That(viewModel.SelectedConflict?.Path, Is.EqualTo("Reports/budget.xlsx"));
            Assert.That(viewModel.Activities.First().Kind, Is.EqualTo("Conflict"));
        });
    }

    [Test]
    public async Task PauseResumeCommands_AreMutuallyAvailable()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(Guid.NewGuid(), "Documents", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CanPauseSync, Is.True);
            Assert.That(viewModel.CanResumeSync, Is.False);
            Assert.That(viewModel.CanTogglePauseResumeSync, Is.True);
            Assert.That(viewModel.PauseResumeSyncLabel, Is.EqualTo("Pause sync"));
            Assert.That(viewModel.PauseResumeTrayLabel, Is.EqualTo("Pause"));
            Assert.That(viewModel.SyncNowCommand.CanExecute(null), Is.True);
        });

        await ExecuteAsync(viewModel.PauseResumeCommand);

        Assert.Multiple(() =>
        {
            Assert.That(controller.PauseAllCalls, Is.EqualTo(1));
            Assert.That(viewModel.CanPauseSync, Is.False);
            Assert.That(viewModel.CanResumeSync, Is.True);
            Assert.That(viewModel.CanTogglePauseResumeSync, Is.True);
            Assert.That(viewModel.PauseResumeSyncLabel, Is.EqualTo("Resume sync"));
            Assert.That(viewModel.PauseResumeTrayLabel, Is.EqualTo("Resume"));
            Assert.That(viewModel.SyncNowCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Sync is paused."));
        });

        await ExecuteAsync(viewModel.PauseResumeCommand);

        Assert.Multiple(() =>
        {
            Assert.That(controller.ResumeAllCalls, Is.EqualTo(1));
            Assert.That(viewModel.CanPauseSync, Is.True);
            Assert.That(viewModel.CanResumeSync, Is.False);
            Assert.That(viewModel.CanTogglePauseResumeSync, Is.True);
            Assert.That(viewModel.PauseResumeSyncLabel, Is.EqualTo("Pause sync"));
            Assert.That(viewModel.PauseResumeTrayLabel, Is.EqualTo("Pause"));
            Assert.That(viewModel.SyncNowCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public async Task GlobalControls_RemainAvailableWhileManualSyncIsRunning()
    {
        var syncAllCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(Guid.NewGuid(), "Documents", "Idle")))
        {
            SyncAllCompletion = syncAllCompletion,
        };
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        viewModel.SyncNowCommand.Execute(null);
        await WaitForAsync(() => viewModel.IsBusy && controller.SyncAllCalls == 1);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SyncNowCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.ShowSettingsCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.CanPauseSync, Is.True);
            Assert.That(viewModel.CanTogglePauseResumeSync, Is.True);
            Assert.That(viewModel.PauseResumeCommand.CanExecute(null), Is.True);
        });

        await ExecuteAsync(viewModel.PauseResumeCommand);

        Assert.Multiple(() =>
        {
            Assert.That(controller.PauseAllCalls, Is.EqualTo(1));
            Assert.That(viewModel.IsBusy, Is.True);
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Paused"));
            Assert.That(viewModel.PauseResumeTrayLabel, Is.EqualTo("Resume"));
        });

        syncAllCompletion.SetResult(true);
        await WaitForAsync(() => !viewModel.SyncNowCommand.IsRunning);

        Assert.That(viewModel.IsBusy, Is.False);
    }

    [Test]
    public async Task PauseResumeCommand_ShowsPausingWhilePauseRequestIsRunning()
    {
        var pauseAllCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Videos", "Syncing")))
        {
            PauseAllCompletion = pauseAllCompletion,
        };
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        viewModel.PauseResumeCommand.Execute(null);
        await WaitForAsync(() => viewModel.PauseResumeCommand.IsRunning && controller.PauseAllCalls == 1);

        SyncPairRowViewModel row = viewModel.SyncPairs.Single();
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSyncPausePending, Is.True);
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Pausing"));
            Assert.That(viewModel.PauseResumeSyncLabel, Is.EqualTo("Pausing sync"));
            Assert.That(viewModel.PauseResumeTrayLabel, Is.EqualTo("Pausing"));
            Assert.That(viewModel.CanTogglePauseResumeSync, Is.False);
            Assert.That(viewModel.PauseResumeCommand.CanExecute(null), Is.False);
            Assert.That(row.Status, Is.EqualTo("Pausing"));
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Videos: Pausing"));
        });

        pauseAllCompletion.SetResult(true);
        await WaitForAsync(() => !viewModel.PauseResumeCommand.IsRunning);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSyncPausePending, Is.False);
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Paused"));
            Assert.That(viewModel.CanTogglePauseResumeSync, Is.True);
            Assert.That(viewModel.PauseResumeSyncLabel, Is.EqualTo("Resume sync"));
        });
    }

    [Test]
    public async Task PauseResumeCommand_RemainsAvailableDuringBackgroundSyncProgress()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Videos", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 5,
            FilesTotal: 20,
            CurrentPath: "Videos/clip.mp4",
            StartedAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 4, 9, 0, 5, DateTimeKind.Utc)));
        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            syncPairId,
            SyncTransferDirection.Download,
            "Videos/clip.mp4",
            TransferredBytes: 512,
            TotalBytes: 1024,
            IsCompleted: false,
            new DateTime(2026, 6, 4, 9, 0, 7, DateTimeKind.Utc)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HeaderStatusText, Is.EqualTo("Syncing"));
            Assert.That(viewModel.CanPauseSync, Is.True);
            Assert.That(viewModel.CanTogglePauseResumeSync, Is.True);
            Assert.That(viewModel.PauseResumeCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.PauseResumeSyncLabel, Is.EqualTo("Pause sync"));
            Assert.That(viewModel.PauseResumeTrayLabel, Is.EqualTo("Pause"));
        });
    }

    [Test]
    public async Task GlobalSyncCommands_DoNotChangeDisabledPairRows()
    {
        Guid enabledPairId = Guid.NewGuid();
        Guid disabledPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshot(
                CreatePair(enabledPairId, "Documents", "Idle"),
                CreatePair(disabledPairId, "Archive", "Disabled")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        await ExecuteAsync(viewModel.SyncNowCommand);

        SyncPairRowViewModel enabledPair = viewModel.SyncPairs.Single(pair => pair.Id == enabledPairId);
        SyncPairRowViewModel disabledPair = viewModel.SyncPairs.Single(pair => pair.Id == disabledPairId);
        Assert.Multiple(() =>
        {
            Assert.That(enabledPair.Status, Is.EqualTo("Idle"));
            Assert.That(enabledPair.CurrentOperation, Is.Empty);
            Assert.That(disabledPair.Status, Is.EqualTo("Disabled"));
            Assert.That(disabledPair.CurrentOperation, Is.Empty);
        });

        await ExecuteAsync(viewModel.PauseCommand);

        Assert.Multiple(() =>
        {
            Assert.That(enabledPair.Status, Is.EqualTo("Paused"));
            Assert.That(disabledPair.Status, Is.EqualTo("Disabled"));
        });

        await ExecuteAsync(viewModel.ResumeCommand);

        Assert.Multiple(() =>
        {
            Assert.That(enabledPair.Status, Is.EqualTo("Idle"));
            Assert.That(disabledPair.Status, Is.EqualTo("Disabled"));
        });
    }

    [Test]
    public async Task OpenFolderCommand_UsesRowParameterWhenProvided()
    {
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshot(
                CreatePair(Guid.NewGuid(), "Documents", "Idle"),
                CreatePair(Guid.NewGuid(), "Pictures", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        await ExecuteAsync(viewModel.OpenFolderCommand, viewModel.SyncPairs[1]);

        Assert.That(controller.OpenedFolderPath, Is.EqualTo("/home/vadim/Pictures"));
    }

    [Test]
    public async Task OpenTrayFolderCommand_OpensSingleSyncPair()
    {
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshot(CreatePair(Guid.NewGuid(), "Documents", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CanOpenTrayFolder, Is.True);
            Assert.That(viewModel.TrayOpenFolderLabel, Is.EqualTo("Open local folder"));
            Assert.That(viewModel.OpenTrayFolderCommand.CanExecute(null), Is.True);
        });

        await ExecuteAsync(viewModel.OpenTrayFolderCommand);

        Assert.That(controller.OpenedFolderPath, Is.EqualTo("/home/vadim/Documents"));
    }

    [Test]
    public async Task OpenTrayFolderCommand_IsDisabledForMultipleSyncPairs()
    {
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshot(
                CreatePair(Guid.NewGuid(), "Documents", "Idle"),
                CreatePair(Guid.NewGuid(), "Pictures", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CanOpenTrayFolder, Is.False);
            Assert.That(viewModel.TrayOpenFolderLabel, Is.EqualTo("Open local folder"));
            Assert.That(viewModel.OpenTrayFolderCommand.CanExecute(null), Is.False);
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
            Assert.That(row.HasCurrentOperation, Is.True);
            Assert.That(row.HasCurrentProgress, Is.True);
            Assert.That(row.IsCurrentProgressIndeterminate, Is.True);
            Assert.That(row.CurrentProgressValue, Is.Zero);
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Syncing"));
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Documents: Uploading report.txt"));
        });
    }

    [Test]
    public async Task StatusChanged_TreatsDisabledPairsAsOutOfScopeForPausedGlobalStatus()
    {
        Guid enabledPairId = Guid.NewGuid();
        Guid disabledPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshot(
                CreatePair(enabledPairId, "Documents", "Idle"),
                CreatePair(disabledPairId, "Archive", "Disabled")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportStatus(new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(enabledPairId, "Paused", null),
            new DesktopSyncPairStatusSnapshot(disabledPairId, "Disabled", null),
        ]));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Paused"));
            Assert.That(viewModel.HeaderStatusText, Is.EqualTo("Paused"));
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Sync is paused."));
            Assert.That(viewModel.CanResumeSync, Is.True);
            Assert.That(viewModel.CanPauseSync, Is.False);
        });
    }

    [Test]
    public async Task StatusChanged_UsesHumanDiskFullActionRequiredMessage()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshotWithNotifications(
                enableNotifications: false,
                CreatePair(syncPairId, "Documents", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportStatus(new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(syncPairId, "Error", "No space left on device"),
        ]));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Action required"));
            Assert.That(
                viewModel.ActionRequiredMessage,
                Is.EqualTo("This computer does not have enough free disk space for sync. Free space and retry."));
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Fix the issue below to continue syncing."));
        });
    }

    [Test]
    public async Task StatusChanged_AddsHumanErrorActivityMessage()
    {
        Guid syncPairId = Guid.NewGuid();
        const string rawError = "'<' is an invalid start of a value. Path: $ | LineNumber: 0 | BytePositionInLine: 0.";
        const string expectedMessage =
            "Cotton API returned a web page instead of JSON. Check the server URL or backend deployment and retry.";
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshotWithNotifications(
                enableNotifications: false,
                CreatePair(syncPairId, "Documents", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportStatus(new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(syncPairId, "Error", rawError),
        ]));

        ActivityRowViewModel errorActivity = viewModel.Activities.First(activity => activity.Kind == "Error");
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SyncPairs.Single().LastError, Is.EqualTo(rawError));
            Assert.That(viewModel.ActionRequiredMessage, Is.EqualTo(expectedMessage));
            Assert.That(errorActivity.Path, Does.EndWith("Documents"));
            Assert.That(errorActivity.Details, Is.EqualTo(expectedMessage));
        });
    }

    [Test]
    public async Task StatusChanged_DeduplicatesUnchangedErrorActivityMessage()
    {
        Guid syncPairId = Guid.NewGuid();
        const string rawError = "There is not enough space on the disk.";
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshotWithNotifications(
                enableNotifications: false,
                CreatePair(syncPairId, "Documents", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportStatus(new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(syncPairId, "Error", rawError),
        ]));
        controller.ReportStatus(new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(syncPairId, "Error", rawError),
        ]));

        Assert.That(viewModel.Activities.Count(static activity => activity.Kind == "Error"), Is.EqualTo(1));

        controller.ReportStatus(new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(syncPairId, "Idle", null),
        ]));
        controller.ReportStatus(new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(syncPairId, "Error", rawError),
        ]));

        Assert.That(viewModel.Activities.Count(static activity => activity.Kind == "Error"), Is.EqualTo(2));
    }

    [Test]
    public async Task TransferProgressChanged_UpdatesCurrentTransferState()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            syncPairId,
            SyncTransferDirection.Upload,
            "Reports/report.txt",
            TransferredBytes: 512,
            TotalBytes: 1024,
            IsCompleted: false,
            new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc)));

        Assert.Multiple(() =>
        {
            SyncPairRowViewModel row = viewModel.SyncPairs.Single();
            Assert.That(viewModel.HasCurrentTransfer, Is.True);
            Assert.That(viewModel.IsStatusCardVisible, Is.False);
            Assert.That(viewModel.IsCurrentTransferIndeterminate, Is.False);
            Assert.That(viewModel.CurrentTransferProgressValue, Is.EqualTo(50).Within(0.01));
            Assert.That(viewModel.CurrentTransferTitle, Is.EqualTo("Documents: Uploading report.txt"));
            Assert.That(viewModel.CurrentTransferDetails, Is.EqualTo("512 B / 1.0 KB"));
            Assert.That(row.CurrentOperation, Is.EqualTo("Uploading report.txt"));
            Assert.That(row.HasCurrentOperation, Is.True);
            Assert.That(row.HasCurrentProgress, Is.True);
            Assert.That(row.IsCurrentProgressIndeterminate, Is.False);
            Assert.That(row.CurrentProgressValue, Is.EqualTo(50).Within(0.01));
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Documents: Uploading report.txt"));
        });
    }

    [Test]
    public async Task TransferProgressChanged_ShowsHashProgressAsChecking()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Videos", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            syncPairId,
            SyncTransferDirection.Hash,
            "2026/video.mp4",
            TransferredBytes: 256,
            TotalBytes: 1024,
            IsCompleted: false,
            new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc)));

        Assert.Multiple(() =>
        {
            SyncPairRowViewModel row = viewModel.SyncPairs.Single();
            Assert.That(viewModel.CurrentTransferTitle, Is.EqualTo("Videos: Checking video.mp4"));
            Assert.That(viewModel.CurrentTransferDetails, Is.EqualTo("256 B / 1.0 KB"));
            Assert.That(row.CurrentOperation, Is.EqualTo("Checking video.mp4"));
            Assert.That(row.CurrentProgressValue, Is.EqualTo(25).Within(0.01));
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Videos: Checking video.mp4"));
        });
    }

    [Test]
    public async Task TransferProgressChanged_DoesNotCountHashBytesAsRunTransferBytes()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Videos", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        DateTime startedAtUtc = new(2026, 6, 7, 9, 0, 0, DateTimeKind.Utc);
        const long totalRunBytes = 10L * 1024 * 1024 * 1024;
        const long completedRunBytes = 3L * 1024 * 1024 * 1024;

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 200,
            FilesTotal: 29189,
            CurrentPath: "Videos/clip.mp4",
            StartedAtUtc: startedAtUtc,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc.AddSeconds(60),
            BytesCompleted: completedRunBytes,
            BytesTotal: totalRunBytes));
        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            syncPairId,
            SyncTransferDirection.Hash,
            "Videos/clip.mp4",
            TransferredBytes: 512L * 1024 * 1024,
            TotalBytes: 1024L * 1024 * 1024,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc.AddSeconds(61),
            SpeedBytesPerSecond: 256L * 1024 * 1024,
            EstimatedTimeRemaining: TimeSpan.FromSeconds(2)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentTransferTitle, Is.EqualTo("Videos: Checking clip.mp4"));
            Assert.That(viewModel.CurrentWorkProgressHeaderSizeDetails, Is.EqualTo("3.0 GB / 10 GB"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("Checking files · 200 of 29189 files"));
            Assert.That(viewModel.CurrentWorkProgressValue, Is.EqualTo(30).Within(0.01));
        });
    }

    [Test]
    public async Task TransferProgressChanged_ShowsSyncingHeaderEvenWhenLatestStatusIsIdle()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Videos", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            syncPairId,
            SyncTransferDirection.Download,
            "Archive/09.7z",
            TransferredBytes: 25L * 1024L * 1024L * 1024L,
            TotalBytes: 28L * 1024L * 1024L * 1024L,
            IsCompleted: false,
            new DateTime(2026, 6, 7, 10, 0, 0, DateTimeKind.Utc)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Connected"));
            Assert.That(viewModel.HeaderStatusText, Is.EqualTo("Syncing"));
            Assert.That(viewModel.HasCurrentWorkProgress, Is.True);
            Assert.That(viewModel.CurrentTransferTitle, Is.EqualTo("Videos: Downloading 09.7z"));
        });
    }

    [Test]
    public async Task TransferProgressChanged_ShowsTransferSpeedAndRemainingTime()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            syncPairId,
            SyncTransferDirection.Download,
            "Reports/report.txt",
            TransferredBytes: 2 * 1024 * 1024,
            TotalBytes: 10 * 1024 * 1024,
            IsCompleted: false,
            new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc),
            SpeedBytesPerSecond: 1024 * 1024,
            EstimatedTimeRemaining: TimeSpan.FromSeconds(8)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasCurrentWorkProgress, Is.True);
            Assert.That(viewModel.CurrentWorkProgressTitle, Is.EqualTo("Documents: Downloading report.txt"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("2.0 MB / 10 MB · 1.0 MB/s · 8s left"));
            Assert.That(viewModel.CurrentWorkProgressValue, Is.EqualTo(20).Within(0.01));
            Assert.That(viewModel.IsCurrentWorkProgressIndeterminate, Is.False);
        });
    }

    [Test]
    public async Task TransferProgressChanged_CoalescesBurstBeforeUiQueue()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        var dispatcher = new QueuedDesktopUiDispatcher();
        using ShellViewModel viewModel = CreateViewModel(controller, uiDispatcher: dispatcher);
        await viewModel.InitializeAsync();
        DateTime occurredAtUtc = new(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc);

        for (int index = 0; index < 100; index++)
        {
            controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
                syncPairId,
                SyncTransferDirection.Upload,
                "Reports/report.txt",
                TransferredBytes: index * 1024,
                TotalBytes: 100 * 1024,
                IsCompleted: false,
                occurredAtUtc.AddMilliseconds(index * 5)));
        }

        Assert.Multiple(() =>
        {
            Assert.That(dispatcher.PostedActionCount, Is.EqualTo(1));
            Assert.That(dispatcher.PendingActionCount, Is.EqualTo(1));
            Assert.That(viewModel.HasCurrentTransfer, Is.False);
        });

        dispatcher.DrainAll();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasCurrentTransfer, Is.True);
            Assert.That(viewModel.CurrentWorkProgressTitle, Is.EqualTo("Documents: Uploading report.txt"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("99 KB / 100 KB"));
            Assert.That(viewModel.CurrentWorkProgressValue, Is.EqualTo(99).Within(0.01));
        });
    }

    [Test]
    public async Task TransferProgressChanged_ThrottlesVisibleUpdates()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        DateTime startedAtUtc = new(2026, 6, 7, 10, 0, 0, DateTimeKind.Utc);

        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            syncPairId,
            SyncTransferDirection.Upload,
            "Reports/report.txt",
            TransferredBytes: 20,
            TotalBytes: 100,
            IsCompleted: false,
            startedAtUtc));
        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            syncPairId,
            SyncTransferDirection.Upload,
            "Reports/report.txt",
            TransferredBytes: 40,
            TotalBytes: 100,
            IsCompleted: false,
            startedAtUtc.AddMilliseconds(50)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentTransferProgressValue, Is.EqualTo(20).Within(0.01));
            Assert.That(viewModel.CurrentTransferDetails, Is.EqualTo("20 B / 100 B"));
        });

        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            syncPairId,
            SyncTransferDirection.Upload,
            "Reports/report.txt",
            TransferredBytes: 60,
            TotalBytes: 100,
            IsCompleted: false,
            startedAtUtc.AddMilliseconds(100)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentTransferProgressValue, Is.EqualTo(60).Within(0.01));
            Assert.That(viewModel.CurrentTransferDetails, Is.EqualTo("60 B / 100 B"));
        });
    }

    [Test]
    public async Task TransferProgressChanged_DoesNotThrottleCompletionSample()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        DateTime startedAtUtc = new(2026, 6, 7, 10, 0, 0, DateTimeKind.Utc);

        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            syncPairId,
            SyncTransferDirection.Upload,
            "Reports/report.txt",
            TransferredBytes: 20,
            TotalBytes: 100,
            IsCompleted: false,
            startedAtUtc));
        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            syncPairId,
            SyncTransferDirection.Upload,
            "Reports/report.txt",
            TransferredBytes: 100,
            TotalBytes: 100,
            IsCompleted: true,
            startedAtUtc.AddMilliseconds(100)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasCurrentTransfer, Is.True);
            Assert.That(viewModel.CurrentTransferProgressValue, Is.EqualTo(100).Within(0.01));
            Assert.That(viewModel.CurrentTransferTitle, Is.EqualTo("Documents: Uploaded report.txt"));
            Assert.That(viewModel.CurrentTransferDetails, Is.EqualTo("100 B / 100 B"));
        });
    }

    [Test]
    public async Task RunProgressChanged_UpdatesCurrentRunProgressState()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 3,
            FilesTotal: 10,
            CurrentPath: "Reports/report.txt",
            StartedAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 4, 9, 0, 5, DateTimeKind.Utc)));

        Assert.Multiple(() =>
        {
            SyncPairRowViewModel row = viewModel.SyncPairs.Single();
            Assert.That(viewModel.HasCurrentRunProgress, Is.True);
            Assert.That(viewModel.IsStatusCardVisible, Is.False);
            Assert.That(viewModel.IsCurrentRunProgressIndeterminate, Is.False);
            Assert.That(viewModel.CurrentRunProgressValue, Is.EqualTo(30).Within(0.01));
            Assert.That(viewModel.CurrentRunProgressTitle, Is.EqualTo("Documents"));
            Assert.That(viewModel.CurrentRunProgressDetails, Is.EqualTo("Checking files · 3 of 10 files"));
            Assert.That(viewModel.HasCurrentWorkProgress, Is.True);
            Assert.That(viewModel.CurrentWorkProgressTitle, Is.EqualTo("Documents"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("Checking files · 3 of 10 files"));
            Assert.That(row.CurrentOperation, Is.EqualTo("Checking files 3 of 10"));
            Assert.That(row.HasCurrentOperation, Is.True);
            Assert.That(row.HasCurrentProgress, Is.True);
            Assert.That(row.IsCurrentProgressIndeterminate, Is.False);
            Assert.That(row.CurrentProgressValue, Is.EqualTo(30).Within(0.01));
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Documents: Checking files 3 of 10"));
        });
    }

    [Test]
    public async Task RunProgressChanged_UsesPlannedBytesForGlobalProgress()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Videos", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        const long totalBytes = 10L * 1024 * 1024 * 1024;
        const long completedBytes = 3L * 1024 * 1024 * 1024;

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 200,
            FilesTotal: 29189,
            CurrentPath: "Videos/clip.mp4",
            StartedAtUtc: new DateTime(2026, 6, 7, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 7, 9, 1, 0, DateTimeKind.Utc),
            BytesCompleted: completedBytes,
            BytesTotal: totalBytes));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentRunProgressValue, Is.EqualTo(30).Within(0.01));
            Assert.That(viewModel.CurrentWorkProgressValue, Is.EqualTo(30).Within(0.01));
            Assert.That(viewModel.CurrentWorkProgressHeaderSizeDetails, Is.EqualTo("3.0 GB / 10 GB"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("Checking files · 200 of 29189 files"));
        });
    }

    [Test]
    public async Task RunProgressChanged_UsesGlobalBytesForHeaderSpeedAndEstimate()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Videos", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        DateTime startedAtUtc = new(2026, 6, 7, 9, 0, 0, DateTimeKind.Utc);
        const long totalBytes = 10L * 1024 * 1024 * 1024;

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 100,
            FilesTotal: 1_000,
            CurrentPath: "Videos/clip-100.mp4",
            startedAtUtc,
            IsCompleted: false,
            startedAtUtc.AddSeconds(10),
            BytesCompleted: 1L * 1024 * 1024 * 1024,
            BytesTotal: totalBytes));
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 200,
            FilesTotal: 1_000,
            CurrentPath: "Videos/clip-200.mp4",
            startedAtUtc,
            IsCompleted: false,
            startedAtUtc.AddSeconds(15),
            BytesCompleted: 2L * 1024 * 1024 * 1024,
            BytesTotal: totalBytes));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentWorkProgressHeaderSizeDetails, Is.EqualTo("2.0 GB / 10 GB"));
            Assert.That(viewModel.CurrentWorkProgressHeaderRateDetails, Is.EqualTo("205 MB/s · 40s left"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("Checking files · 200 of 1000 files"));
        });
    }

    [Test]
    public async Task RunProgressChanged_ThrottlesVisibleUpdates()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        DateTime startedAtUtc = new(2026, 6, 7, 10, 0, 0, DateTimeKind.Utc);

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 1,
            FilesTotal: 100,
            CurrentPath: "Reports/file-001.txt",
            StartedAtUtc: startedAtUtc,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc));
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 50,
            FilesTotal: 100,
            CurrentPath: "Reports/file-050.txt",
            StartedAtUtc: startedAtUtc,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc.AddMilliseconds(50)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentRunProgressValue, Is.EqualTo(1).Within(0.01));
            Assert.That(viewModel.CurrentRunProgressDetails, Is.EqualTo("Checking files · 1 of 100 files"));
        });

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 75,
            FilesTotal: 100,
            CurrentPath: "Reports/file-075.txt",
            StartedAtUtc: startedAtUtc,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc.AddMilliseconds(100)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentRunProgressValue, Is.EqualTo(75).Within(0.01));
            Assert.That(viewModel.CurrentRunProgressDetails, Is.EqualTo("Checking files · 75 of 100 files"));
        });
    }

    [Test]
    public async Task RunProgressChanged_UpdatesDirectoryRunProgressState()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingDirectories,
            FilesCompleted: 3,
            FilesTotal: 10,
            CurrentPath: "Reports",
            StartedAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 4, 9, 0, 5, DateTimeKind.Utc)));

        Assert.Multiple(() =>
        {
            SyncPairRowViewModel row = viewModel.SyncPairs.Single();
            Assert.That(viewModel.HasCurrentRunProgress, Is.True);
            Assert.That(viewModel.IsCurrentRunProgressIndeterminate, Is.False);
            Assert.That(viewModel.CurrentRunProgressValue, Is.EqualTo(30).Within(0.01));
            Assert.That(viewModel.CurrentRunProgressTitle, Is.EqualTo("Documents"));
            Assert.That(viewModel.CurrentRunProgressDetails, Is.EqualTo("Preparing folders · 3 of 10 folders"));
            Assert.That(viewModel.CurrentWorkProgressTitle, Is.EqualTo("Documents"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("Preparing folders · 3 of 10 folders"));
            Assert.That(row.CurrentOperation, Is.EqualTo("Preparing folders 3 of 10"));
            Assert.That(row.IsCurrentProgressIndeterminate, Is.False);
            Assert.That(row.CurrentProgressValue, Is.EqualTo(30).Within(0.01));
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Documents: Preparing folders 3 of 10"));
        });
    }

    [Test]
    public async Task RunProgressChanged_CoalescesBurstBeforeUiQueue()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        var dispatcher = new QueuedDesktopUiDispatcher();
        using ShellViewModel viewModel = CreateViewModel(controller, uiDispatcher: dispatcher);
        await viewModel.InitializeAsync();
        DateTime occurredAtUtc = new(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc);

        for (int index = 0; index < 100; index++)
        {
            string path = "Reports/file-" + index.ToString("000", CultureInfo.InvariantCulture) + ".txt";
            controller.ReportRunProgress(new DesktopRunProgressSnapshot(
                syncPairId,
                SyncRunProgressStage.ReconcilingFiles,
                FilesCompleted: index,
                FilesTotal: 100,
                CurrentPath: path,
                StartedAtUtc: occurredAtUtc,
                IsCompleted: false,
                OccurredAtUtc: occurredAtUtc.AddMilliseconds(index * 5)));
        }

        Assert.Multiple(() =>
        {
            Assert.That(dispatcher.PostedActionCount, Is.EqualTo(1));
            Assert.That(dispatcher.PendingActionCount, Is.EqualTo(1));
            Assert.That(viewModel.HasCurrentRunProgress, Is.False);
        });

        dispatcher.DrainAll();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasCurrentRunProgress, Is.True);
            Assert.That(viewModel.CurrentWorkProgressTitle, Is.EqualTo("Documents"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("Checking files · 99 of 100 files"));
            Assert.That(viewModel.CurrentWorkProgressValue, Is.EqualTo(99).Within(0.01));
        });
    }

    [Test]
    public async Task RunProgressChanged_ShowsLocalScanDiscoveryCount()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ScanningLocal,
            FilesCompleted: 123,
            FilesTotal: null,
            CurrentPath: "Reports/report.txt",
            StartedAtUtc: new DateTime(2026, 6, 6, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 6, 9, 0, 5, DateTimeKind.Utc)));

        Assert.Multiple(() =>
        {
            SyncPairRowViewModel row = viewModel.SyncPairs.Single();
            Assert.That(viewModel.HasCurrentRunProgress, Is.True);
            Assert.That(viewModel.CurrentRunProgressTitle, Is.EqualTo("Documents"));
            Assert.That(viewModel.CurrentRunProgressDetails, Is.EqualTo("Scanning local files · 123 files found · report.txt"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("Scanning local files · 123 files found · report.txt"));
            Assert.That(viewModel.IsCurrentWorkProgressIndeterminate, Is.True);
            Assert.That(row.CurrentOperation, Is.EqualTo("Scanning local files"));
            Assert.That(row.IsCurrentProgressIndeterminate, Is.True);
        });
    }

    [Test]
    public async Task RunProgressChanged_ShowsLocalScanCurrentPathBeforeFilesAreFound()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ScanningLocal,
            FilesCompleted: 0,
            FilesTotal: null,
            CurrentPath: "Reports",
            StartedAtUtc: new DateTime(2026, 6, 6, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 6, 9, 0, 5, DateTimeKind.Utc)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasCurrentRunProgress, Is.True);
            Assert.That(viewModel.CurrentRunProgressDetails, Is.EqualTo("Looking for local changes · Reports"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("Looking for local changes · Reports"));
            Assert.That(viewModel.IsCurrentWorkProgressIndeterminate, Is.True);
        });
    }

    [Test]
    public async Task RunProgressChanged_ShowsRemoteScanDiscoveryCount()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ScanningRemote,
            FilesCompleted: 123,
            FilesTotal: null,
            CurrentPath: "Reports/report.txt",
            StartedAtUtc: new DateTime(2026, 6, 6, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 6, 9, 0, 5, DateTimeKind.Utc)));

        Assert.Multiple(() =>
        {
            SyncPairRowViewModel row = viewModel.SyncPairs.Single();
            Assert.That(viewModel.HasCurrentRunProgress, Is.True);
            Assert.That(viewModel.CurrentRunProgressTitle, Is.EqualTo("Documents"));
            Assert.That(viewModel.CurrentRunProgressDetails, Is.EqualTo("Scanning Cotton Cloud · 123 cloud files found · report.txt"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("Scanning Cotton Cloud · 123 cloud files found · report.txt"));
            Assert.That(viewModel.IsCurrentWorkProgressIndeterminate, Is.True);
            Assert.That(row.CurrentOperation, Is.EqualTo("Scanning Cotton Cloud"));
            Assert.That(row.IsCurrentProgressIndeterminate, Is.True);
        });
    }

    [Test]
    public async Task RunProgressChanged_ShowsRemoteScanCurrentPathBeforeFilesAreFound()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ScanningRemote,
            FilesCompleted: 0,
            FilesTotal: null,
            CurrentPath: "Reports",
            StartedAtUtc: new DateTime(2026, 6, 6, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 6, 9, 0, 5, DateTimeKind.Utc)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasCurrentRunProgress, Is.True);
            Assert.That(viewModel.CurrentRunProgressDetails, Is.EqualTo("Checking Cotton Cloud · Reports"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("Checking Cotton Cloud · Reports"));
            Assert.That(viewModel.IsCurrentWorkProgressIndeterminate, Is.True);
        });
    }

    [Test]
    public async Task RunProgressChanged_AggregatesMultipleFolderProgress()
    {
        Guid documentsPairId = Guid.NewGuid();
        Guid videosPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshot(
                CreatePair(documentsPairId, "Documents", "Syncing"),
                CreatePair(videosPairId, "Videos", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            documentsPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 3,
            FilesTotal: 10,
            CurrentPath: "Reports/report.txt",
            StartedAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 4, 9, 0, 5, DateTimeKind.Utc)));
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            videosPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 5,
            FilesTotal: 20,
            CurrentPath: "Videos/clip.mp4",
            StartedAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 4, 9, 0, 6, DateTimeKind.Utc)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasCurrentRunProgress, Is.True);
            Assert.That(viewModel.CurrentRunProgressTitle, Is.EqualTo("Syncing 2 folders"));
            Assert.That(viewModel.CurrentRunProgressDetails, Is.EqualTo("8 of 30 files across 2 folders"));
            Assert.That(viewModel.CurrentRunProgressValue, Is.EqualTo(26.666).Within(0.01));
            Assert.That(viewModel.IsCurrentRunProgressIndeterminate, Is.False);
            Assert.That(viewModel.CurrentWorkProgressTitle, Is.EqualTo("Syncing 2 folders"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("8 of 30 files across 2 folders"));
            Assert.That(viewModel.CurrentWorkProgressValue, Is.EqualTo(26.666).Within(0.01));
            Assert.That(viewModel.IsCurrentWorkProgressIndeterminate, Is.False);
        });
    }

    [Test]
    public async Task RunProgressChanged_AggregatesMultipleLocalScanCounts()
    {
        Guid documentsPairId = Guid.NewGuid();
        Guid videosPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshot(
                CreatePair(documentsPairId, "Documents", "Syncing"),
                CreatePair(videosPairId, "Videos", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            documentsPairId,
            SyncRunProgressStage.ScanningLocal,
            FilesCompleted: 123,
            FilesTotal: null,
            CurrentPath: "Reports/report.txt",
            StartedAtUtc: new DateTime(2026, 6, 6, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 6, 9, 0, 5, DateTimeKind.Utc)));
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            videosPairId,
            SyncRunProgressStage.ScanningLocal,
            FilesCompleted: 456,
            FilesTotal: null,
            CurrentPath: "Videos/clip.mp4",
            StartedAtUtc: new DateTime(2026, 6, 6, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 6, 9, 0, 6, DateTimeKind.Utc)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasCurrentRunProgress, Is.True);
            Assert.That(viewModel.CurrentRunProgressTitle, Is.EqualTo("Syncing 2 folders"));
            Assert.That(viewModel.CurrentRunProgressDetails, Is.EqualTo("579 files found across 2 folders"));
            Assert.That(viewModel.IsCurrentRunProgressIndeterminate, Is.True);
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("579 files found across 2 folders"));
            Assert.That(viewModel.IsCurrentWorkProgressIndeterminate, Is.True);
        });
    }

    [Test]
    public async Task RunProgressChanged_AggregatesMultipleRemoteScanCounts()
    {
        Guid documentsPairId = Guid.NewGuid();
        Guid videosPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshot(
                CreatePair(documentsPairId, "Documents", "Syncing"),
                CreatePair(videosPairId, "Videos", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            documentsPairId,
            SyncRunProgressStage.ScanningRemote,
            FilesCompleted: 123,
            FilesTotal: null,
            CurrentPath: "Reports/report.txt",
            StartedAtUtc: new DateTime(2026, 6, 6, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 6, 9, 0, 5, DateTimeKind.Utc)));
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            videosPairId,
            SyncRunProgressStage.ScanningRemote,
            FilesCompleted: 456,
            FilesTotal: null,
            CurrentPath: "Videos/clip.mp4",
            StartedAtUtc: new DateTime(2026, 6, 6, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 6, 9, 0, 6, DateTimeKind.Utc)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasCurrentRunProgress, Is.True);
            Assert.That(viewModel.CurrentRunProgressTitle, Is.EqualTo("Syncing 2 folders"));
            Assert.That(viewModel.CurrentRunProgressDetails, Is.EqualTo("579 cloud files found across 2 folders"));
            Assert.That(viewModel.IsCurrentRunProgressIndeterminate, Is.True);
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("579 cloud files found across 2 folders"));
            Assert.That(viewModel.IsCurrentWorkProgressIndeterminate, Is.True);
        });
    }

    [Test]
    public async Task RunProgressChanged_ClearsCompletedRunProgressBeforeIdleStatusArrives()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 3,
            FilesTotal: 10,
            CurrentPath: "Reports/report.txt",
            StartedAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 4, 9, 0, 5, DateTimeKind.Utc)));

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.Completed,
            FilesCompleted: 10,
            FilesTotal: 10,
            CurrentPath: string.Empty,
            StartedAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: true,
            OccurredAtUtc: new DateTime(2026, 6, 4, 9, 0, 15, DateTimeKind.Utc)));

        Assert.Multiple(() =>
        {
            SyncPairRowViewModel row = viewModel.SyncPairs.Single();
            Assert.That(viewModel.HasCurrentRunProgress, Is.False);
            Assert.That(viewModel.CurrentRunProgressTitle, Is.Empty);
            Assert.That(viewModel.CurrentRunProgressDetails, Is.Empty);
            Assert.That(row.CurrentOperation, Is.Empty);
            Assert.That(row.HasCurrentOperation, Is.False);
            Assert.That(row.HasCurrentProgress, Is.False);
            Assert.That(row.IsCurrentProgressIndeterminate, Is.False);
            Assert.That(row.CurrentProgressValue, Is.Zero);
        });
    }

    [Test]
    public async Task RunProgressChanged_RemovesCompletedFolderFromAggregateProgress()
    {
        Guid documentsPairId = Guid.NewGuid();
        Guid videosPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshot(
                CreatePair(documentsPairId, "Documents", "Syncing"),
                CreatePair(videosPairId, "Videos", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            documentsPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 3,
            FilesTotal: 10,
            CurrentPath: "Reports/report.txt",
            StartedAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 4, 9, 0, 5, DateTimeKind.Utc)));
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            videosPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 5,
            FilesTotal: 20,
            CurrentPath: "Videos/clip.mp4",
            StartedAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 4, 9, 0, 6, DateTimeKind.Utc)));

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            documentsPairId,
            SyncRunProgressStage.Completed,
            FilesCompleted: 10,
            FilesTotal: 10,
            CurrentPath: string.Empty,
            StartedAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: true,
            OccurredAtUtc: new DateTime(2026, 6, 4, 9, 0, 15, DateTimeKind.Utc)));

        Assert.Multiple(() =>
        {
            SyncPairRowViewModel documentsRow = viewModel.SyncPairs.Single(pair => pair.Id == documentsPairId);
            SyncPairRowViewModel videosRow = viewModel.SyncPairs.Single(pair => pair.Id == videosPairId);
            Assert.That(viewModel.CurrentWorkProgressTitle, Is.EqualTo("Videos"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("Checking files · 5 of 20 files"));
            Assert.That(viewModel.CurrentWorkProgressValue, Is.EqualTo(25).Within(0.01));
            Assert.That(documentsRow.HasCurrentProgress, Is.False);
            Assert.That(documentsRow.CurrentOperation, Is.Empty);
            Assert.That(videosRow.HasCurrentProgress, Is.True);
            Assert.That(videosRow.CurrentOperation, Is.EqualTo("Checking files 5 of 20"));
        });
    }

    [Test]
    public async Task TransferProgressChanged_KeepsAggregateRunProgressPrimaryForMultipleFolders()
    {
        Guid documentsPairId = Guid.NewGuid();
        Guid videosPairId = Guid.NewGuid();
        FakeDesktopShellController controller = CreateTwoFolderSyncingController(documentsPairId, videosPairId);
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        ReportTwoFolderCheckingProgress(controller, documentsPairId, videosPairId);

        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            documentsPairId,
            SyncTransferDirection.Upload,
            "Reports/report.txt",
            TransferredBytes: 512,
            TotalBytes: 1024,
            IsCompleted: false,
            new DateTime(2026, 6, 4, 9, 0, 7, DateTimeKind.Utc)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasCurrentTransfer, Is.True);
            Assert.That(viewModel.CurrentWorkProgressTitle, Is.EqualTo("Syncing 2 folders"));
            Assert.That(viewModel.CurrentWorkProgressHeaderDetails, Is.EqualTo("512 B · 1.3 files/s · 20s left"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("8 of 30 files across 2 folders"));
            Assert.That(viewModel.CurrentWorkProgressSecondaryDetails, Is.Empty);
            Assert.That(viewModel.CurrentWorkProgressValue, Is.EqualTo(28.333).Within(0.01));
            Assert.That(viewModel.IsCurrentWorkProgressIndeterminate, Is.False);
        });
    }

    [Test]
    public async Task TransferProgressChanged_KeepsRunProgressPrimaryForOneFolder()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 0,
            FilesTotal: 20,
            CurrentPath: "Reports/report.txt",
            StartedAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc)));
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 5,
            FilesTotal: 20,
            CurrentPath: "Reports/report.txt",
            StartedAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 4, 9, 0, 5, DateTimeKind.Utc)));
        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            syncPairId,
            SyncTransferDirection.Upload,
            "Reports/report.txt",
            TransferredBytes: 512,
            TotalBytes: 1024,
            IsCompleted: false,
            new DateTime(2026, 6, 4, 9, 0, 7, DateTimeKind.Utc),
            SpeedBytesPerSecond: 256,
            EstimatedTimeRemaining: TimeSpan.FromSeconds(2)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentWorkProgressTitle, Is.EqualTo("Documents"));
            Assert.That(viewModel.CurrentWorkProgressHeaderDetails, Is.EqualTo("512 B · 256 B/s · 15s left"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("Checking files · 5 of 20 files"));
            Assert.That(viewModel.CurrentWorkProgressSecondaryDetails, Is.Empty);
            Assert.That(viewModel.CurrentWorkProgressValue, Is.EqualTo(27.5).Within(0.01));
            Assert.That(viewModel.IsCurrentWorkProgressIndeterminate, Is.False);
        });
    }

    [Test]
    public async Task TransferProgressChanged_KeepsGlobalRunByteProgressPrimaryForOneFolder()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Videos", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        DateTime startedAtUtc = new(2026, 6, 7, 9, 0, 0, DateTimeKind.Utc);
        const long totalRunBytes = 10L * 1024 * 1024 * 1024;
        const long completedRunBytes = 3L * 1024 * 1024 * 1024;
        const long currentFileTransferredBytes = 512L * 1024 * 1024;
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 200,
            FilesTotal: 29189,
            CurrentPath: "Videos/clip.mp4",
            StartedAtUtc: startedAtUtc,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc.AddSeconds(60),
            BytesCompleted: completedRunBytes,
            BytesTotal: totalRunBytes));

        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            syncPairId,
            SyncTransferDirection.Download,
            "Videos/clip.mp4",
            TransferredBytes: 256L * 1024 * 1024,
            TotalBytes: 1024L * 1024 * 1024,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc.AddSeconds(60),
            SpeedBytesPerSecond: 512L * 1024 * 1024,
            EstimatedTimeRemaining: TimeSpan.FromSeconds(2)));
        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            syncPairId,
            SyncTransferDirection.Download,
            "Videos/clip.mp4",
            TransferredBytes: currentFileTransferredBytes,
            TotalBytes: 1024L * 1024 * 1024,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc.AddSeconds(62),
            SpeedBytesPerSecond: 64L * 1024 * 1024,
            EstimatedTimeRemaining: TimeSpan.FromSeconds(8)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentWorkProgressTitle, Is.EqualTo("Videos"));
            Assert.That(viewModel.CurrentWorkProgressHeaderSizeDetails, Is.EqualTo("3.5 GB / 10 GB"));
            Assert.That(viewModel.CurrentWorkProgressHeaderRateDetails, Is.EqualTo("128 MB/s · 55s left"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("Checking files · 200 of 29189 files"));
            Assert.That(viewModel.CurrentWorkProgressSecondaryDetails, Is.Empty);
            Assert.That(viewModel.CurrentWorkProgressValue, Is.EqualTo(35).Within(0.01));
        });
    }

    [Test]
    public async Task TransferProgressChanged_AggregatesHeaderMetricsForMultipleActiveTransfers()
    {
        Guid documentsPairId = Guid.NewGuid();
        Guid videosPairId = Guid.NewGuid();
        FakeDesktopShellController controller = CreateTwoFolderSyncingController(documentsPairId, videosPairId);
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        ReportTwoFolderCheckingProgress(controller, documentsPairId, videosPairId);

        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            documentsPairId,
            SyncTransferDirection.Upload,
            "Reports/report.txt",
            TransferredBytes: 512,
            TotalBytes: 1024,
            IsCompleted: false,
            new DateTime(2026, 6, 4, 9, 0, 7, DateTimeKind.Utc),
            SpeedBytesPerSecond: 256));
        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            videosPairId,
            SyncTransferDirection.Download,
            "Videos/clip.mp4",
            TransferredBytes: 1536,
            TotalBytes: 3072,
            IsCompleted: false,
            new DateTime(2026, 6, 4, 9, 0, 8, DateTimeKind.Utc),
            SpeedBytesPerSecond: 512));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentWorkProgressTitle, Is.EqualTo("Syncing 2 folders"));
            Assert.That(viewModel.CurrentWorkProgressHeaderDetails, Is.EqualTo("2.0 KB · 768 B/s · 20s left"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("8 of 30 files across 2 folders"));
            Assert.That(viewModel.CurrentWorkProgressSecondaryDetails, Is.Empty);
            Assert.That(viewModel.CurrentWorkProgressValue, Is.EqualTo(30).Within(0.01));
        });
    }

    [Test]
    public async Task TransferProgressChanged_OmitsTransferEstimateFromAggregateRunHeader()
    {
        Guid documentsPairId = Guid.NewGuid();
        Guid videosPairId = Guid.NewGuid();
        FakeDesktopShellController controller = CreateTwoFolderSyncingController(documentsPairId, videosPairId);
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        ReportTwoFolderCheckingProgress(controller, documentsPairId, videosPairId);

        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            documentsPairId,
            SyncTransferDirection.Upload,
            "Reports/report.txt",
            TransferredBytes: 512,
            TotalBytes: 1024,
            IsCompleted: false,
            new DateTime(2026, 6, 4, 9, 0, 7, DateTimeKind.Utc),
            SpeedBytesPerSecond: 256,
            EstimatedTimeRemaining: TimeSpan.FromSeconds(2)));
        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            videosPairId,
            SyncTransferDirection.Download,
            "Videos/clip.mp4",
            TransferredBytes: 1536,
            TotalBytes: 3072,
            IsCompleted: false,
            new DateTime(2026, 6, 4, 9, 0, 8, DateTimeKind.Utc),
            SpeedBytesPerSecond: 512,
            EstimatedTimeRemaining: TimeSpan.FromSeconds(20)));

        Assert.That(viewModel.CurrentWorkProgressHeaderDetails, Is.EqualTo("2.0 KB · 768 B/s · 20s left"));
    }

    [Test]
    public async Task TransferProgressChanged_DoesNotDuplicateAggregateRunDetailsAfterTransferCompletes()
    {
        Guid documentsPairId = Guid.NewGuid();
        Guid videosPairId = Guid.NewGuid();
        FakeDesktopShellController controller = CreateTwoFolderSyncingController(documentsPairId, videosPairId);
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        ReportTwoFolderCheckingProgress(controller, documentsPairId, videosPairId);

        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            documentsPairId,
            SyncTransferDirection.Upload,
            "Reports/report.txt",
            TransferredBytes: 1024,
            TotalBytes: 1024,
            IsCompleted: true,
            new DateTime(2026, 6, 4, 9, 0, 7, DateTimeKind.Utc)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentWorkProgressTitle, Is.EqualTo("Syncing 2 folders"));
            Assert.That(viewModel.CurrentWorkProgressHeaderDetails, Is.EqualTo("1.0 KB · 1.3 files/s · 20s left"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("8 of 30 files across 2 folders"));
            Assert.That(viewModel.CurrentWorkProgressSecondaryDetails, Is.Empty);
        });
    }

    [Test]
    public async Task TransferProgressChanged_KeepsRunMetricsAfterCompletedSmallTransfers()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Videos", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        DateTime startedAtUtc = new(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc);

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 0,
            FilesTotal: 100,
            CurrentPath: "Videos/first.mp4",
            StartedAtUtc: startedAtUtc,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc));
        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            syncPairId,
            SyncTransferDirection.Upload,
            "Videos/first.mp4",
            TransferredBytes: 1024,
            TotalBytes: 1024,
            IsCompleted: true,
            startedAtUtc.AddSeconds(1)));
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 1,
            FilesTotal: 100,
            CurrentPath: "Videos/second.mp4",
            StartedAtUtc: startedAtUtc,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc.AddSeconds(1)));
        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            syncPairId,
            SyncTransferDirection.Upload,
            "Videos/second.mp4",
            TransferredBytes: 1024,
            TotalBytes: 1024,
            IsCompleted: true,
            startedAtUtc.AddSeconds(3)));
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 2,
            FilesTotal: 100,
            CurrentPath: "Videos/third.mp4",
            StartedAtUtc: startedAtUtc,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc.AddSeconds(3)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentWorkProgressHeaderSizeDetails, Is.EqualTo("2.0 KB"));
            Assert.That(viewModel.CurrentWorkProgressHeaderRateDetails, Does.Contain("512 B/s"));
            Assert.That(viewModel.CurrentWorkProgressHeaderRateDetails, Does.Not.Contain("left"));
            Assert.That(viewModel.CurrentWorkProgressHeaderDetails, Does.Contain("2.0 KB · 512 B/s"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("Checking files · 2 of 100 files"));
        });
    }

    [Test]
    public async Task RunProgressChanged_ShowsGlobalFileRateWhenByteRateIsUnavailable()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Videos", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        DateTime startedAtUtc = new(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc);

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 0,
            FilesTotal: 1000,
            CurrentPath: "Videos/clip-0000.mp4",
            StartedAtUtc: startedAtUtc,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc));
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 100,
            FilesTotal: 1000,
            CurrentPath: "Videos/clip-0100.mp4",
            StartedAtUtc: startedAtUtc,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc.AddSeconds(10)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentWorkProgressHeaderSizeDetails, Is.Empty);
            Assert.That(viewModel.CurrentWorkProgressHeaderRateDetails, Is.EqualTo("10 files/s · 1m 30s left"));
            Assert.That(viewModel.CurrentWorkProgressHeaderDetails, Is.EqualTo("10 files/s · 1m 30s left"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("Checking files · 100 of 1000 files"));
        });
    }

    [Test]
    public async Task RunProgressChanged_ShowsGlobalFileRateAfterShortManyFileProgress()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Videos", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        DateTime startedAtUtc = new(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc);

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 0,
            FilesTotal: 1000,
            CurrentPath: "Videos/clip-0000.mp4",
            StartedAtUtc: startedAtUtc,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc));
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 100,
            FilesTotal: 1000,
            CurrentPath: "Videos/clip-0100.mp4",
            StartedAtUtc: startedAtUtc,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc.AddSeconds(2)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentWorkProgressHeaderRateDetails, Is.EqualTo("50 files/s · 20s left"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("Checking files · 100 of 1000 files"));
        });
    }

    [Test]
    public async Task RunProgressChanged_KeepsGlobalFileRateWhenAggregateTotalGrows()
    {
        Guid firstSyncPairId = Guid.NewGuid();
        Guid secondSyncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(
            CreatePair(firstSyncPairId, "Cloud", "Syncing"),
            CreatePair(secondSyncPairId, "Videos", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        DateTime startedAtUtc = new(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc);

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            firstSyncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 0,
            FilesTotal: 1000,
            CurrentPath: "Cloud/file-0000.txt",
            StartedAtUtc: startedAtUtc,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc));
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            firstSyncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 100,
            FilesTotal: 1000,
            CurrentPath: "Cloud/file-0100.txt",
            StartedAtUtc: startedAtUtc,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc.AddSeconds(10)));
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            secondSyncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 50,
            FilesTotal: 1000,
            CurrentPath: "Videos/clip-0050.mp4",
            StartedAtUtc: startedAtUtc,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc.AddSeconds(15)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentWorkProgressTitle, Is.EqualTo("Syncing 2 folders"));
            Assert.That(viewModel.CurrentWorkProgressHeaderRateDetails, Is.EqualTo("10 files/s · 2m 05s left"));
            Assert.That(viewModel.CurrentWorkProgressDetails, Is.EqualTo("150 of 2000 files across 2 folders"));
        });
    }

    [Test]
    public async Task TransferProgressChanged_UsesGlobalFileRateWhenActiveTransferHasNoByteRate()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Videos", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        DateTime startedAtUtc = new(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc);

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 0,
            FilesTotal: 1000,
            CurrentPath: "Videos/clip-0000.mp4",
            StartedAtUtc: startedAtUtc,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc));
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 100,
            FilesTotal: 1000,
            CurrentPath: "Videos/clip-0100.mp4",
            StartedAtUtc: startedAtUtc,
            IsCompleted: false,
            OccurredAtUtc: startedAtUtc.AddSeconds(10)));
        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            syncPairId,
            SyncTransferDirection.Download,
            "Videos/clip-0101.mp4",
            TransferredBytes: 1024,
            TotalBytes: 1024 * 1024,
            IsCompleted: false,
            startedAtUtc.AddSeconds(10)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasCurrentTransfer, Is.True);
            Assert.That(viewModel.CurrentWorkProgressHeaderRateDetails, Is.EqualTo("10 files/s · 1m 30s left"));
            Assert.That(viewModel.CurrentWorkProgressSecondaryDetails, Is.Empty);
        });
    }

    [Test]
    public async Task RunProgressChanged_EstimatesFromRecentFileProgressInsteadOfPassStart()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Videos", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        DateTime passStartedAtUtc = new(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc);
        DateTime reconcileStartedAtUtc = passStartedAtUtc.AddMinutes(5);

        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 0,
            FilesTotal: 1000,
            CurrentPath: "Videos/clip-0000.mp4",
            StartedAtUtc: passStartedAtUtc,
            IsCompleted: false,
            OccurredAtUtc: reconcileStartedAtUtc));
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 100,
            FilesTotal: 1000,
            CurrentPath: "Videos/clip-0100.mp4",
            StartedAtUtc: passStartedAtUtc,
            IsCompleted: false,
            OccurredAtUtc: reconcileStartedAtUtc.AddSeconds(10)));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentWorkProgressHeaderRateDetails, Is.EqualTo("10 files/s · 1m 30s left"));
            Assert.That(viewModel.CurrentWorkProgressHeaderRateDetails, Does.Not.Contain("45m"));
        });
    }

    [Test]
    public async Task StatusChanged_ClearsCurrentRunProgressWhenSyncBecomesIdle()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            syncPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 3,
            FilesTotal: 10,
            CurrentPath: "Reports/report.txt",
            StartedAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 4, 9, 0, 5, DateTimeKind.Utc)));

        controller.ReportStatus(new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(
                syncPairId,
                "Idle",
                null,
                LastSyncedAtUtc: new DateTime(2026, 6, 4, 9, 1, 0, DateTimeKind.Utc)),
        ]));

        Assert.Multiple(() =>
        {
            SyncPairRowViewModel row = viewModel.SyncPairs.Single();
            Assert.That(viewModel.HasCurrentRunProgress, Is.False);
            Assert.That(viewModel.CurrentRunProgressTitle, Is.Empty);
            Assert.That(viewModel.CurrentRunProgressDetails, Is.Empty);
            Assert.That(viewModel.CurrentRunProgressValue, Is.Zero);
            Assert.That(row.CurrentOperation, Is.Empty);
            Assert.That(row.HasCurrentOperation, Is.False);
            Assert.That(row.HasCurrentProgress, Is.False);
            Assert.That(row.IsCurrentProgressIndeterminate, Is.False);
            Assert.That(row.CurrentProgressValue, Is.Zero);
        });
    }

    [Test]
    public async Task StatusChanged_ClearsCurrentTransferWhenSyncBecomesIdle()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        controller.ReportTransferProgress(new DesktopTransferProgressSnapshot(
            syncPairId,
            SyncTransferDirection.Download,
            "Reports/report.txt",
            TransferredBytes: 1024,
            TotalBytes: 1024,
            IsCompleted: true,
            new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc)));

        controller.ReportStatus(new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(
                syncPairId,
                "Idle",
                null,
                LastSyncedAtUtc: new DateTime(2026, 6, 4, 9, 1, 0, DateTimeKind.Utc)),
        ]));

        Assert.Multiple(() =>
        {
            SyncPairRowViewModel row = viewModel.SyncPairs.Single();
            Assert.That(viewModel.HasCurrentTransfer, Is.False);
            Assert.That(viewModel.CurrentTransferTitle, Is.Empty);
            Assert.That(viewModel.CurrentTransferDetails, Is.Empty);
            Assert.That(viewModel.CurrentTransferProgressValue, Is.Zero);
            Assert.That(row.CurrentOperation, Is.Empty);
            Assert.That(row.HasCurrentOperation, Is.False);
            Assert.That(row.HasCurrentProgress, Is.False);
            Assert.That(row.IsCurrentProgressIndeterminate, Is.False);
            Assert.That(row.CurrentProgressValue, Is.Zero);
        });
    }

    [Test]
    public async Task Initialize_ShowsFirstSyncPendingUntilPairHasBaseline()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(Guid.NewGuid(), "Documents", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);

        await viewModel.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsStatusCardVisible, Is.True);
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Waiting for first sync."));
            Assert.That(viewModel.StatusCardTitle, Is.EqualTo("Waiting for first sync."));
            Assert.That(viewModel.HasStatusCardDetail, Is.False);
        });
    }

    [Test]
    public async Task Initialize_ShowsUpToDateAfterPairHasBaseline()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(
            Guid.NewGuid(),
            "Documents",
            "Idle",
            new DateTime(2026, 6, 4, 7, 30, 0, DateTimeKind.Utc))));
        using ShellViewModel viewModel = CreateViewModel(controller);

        await viewModel.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsStatusCardVisible, Is.False);
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("All folders are up to date."));
        });
    }

    [Test]
    public async Task StatusChanged_UpdatesBaselineAndShowsUpToDateAfterSuccessfulSync()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportStatus(new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(
                syncPairId,
                "Idle",
                null,
                LastSyncedAtUtc: new DateTime(2026, 6, 4, 8, 0, 0, DateTimeKind.Utc)),
        ]));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SyncPairs.Single().LastSyncedAtUtc, Is.EqualTo(new DateTime(2026, 6, 4, 8, 0, 0, DateTimeKind.Utc)));
            Assert.That(viewModel.IsStatusCardVisible, Is.False);
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("All folders are up to date."));
        });
    }

    [Test]
    public async Task StatusChanged_RecordsCompletionNotificationWithoutDashboardCard()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshotWithNotifications(
                enableNotifications: false,
                CreatePair(syncPairId, "Documents", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportStatus(new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(syncPairId, "Syncing", null),
        ]));
        controller.ReportStatus(new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(
                syncPairId,
                "Idle",
                null,
                LastSyncedAtUtc: new DateTime(2026, 6, 4, 8, 0, 0, DateTimeKind.Utc)),
        ]));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasNotifications, Is.True);
            Assert.That(viewModel.Notifications.Single().Title, Is.EqualTo("Initial sync complete"));
            Assert.That(viewModel.HasDashboardNotifications, Is.False);
        });

        controller.ReportStatus(new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(syncPairId, "Paused", null),
        ]));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasNotifications, Is.True);
            Assert.That(viewModel.HasDashboardNotifications, Is.False);
            Assert.That(viewModel.IsStatusCardVisible, Is.True);
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Sync is paused."));
        });
    }

    [Test]
    public async Task RunProgressChanged_HidesCompletionNotificationWhileAnotherFolderIsActive()
    {
        Guid completedPairId = Guid.NewGuid();
        Guid activePairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshotWithNotifications(
                enableNotifications: false,
                CreatePair(completedPairId, "Documents", "Syncing"),
                CreatePair(activePairId, "Videos", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        controller.ReportStatus(new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(completedPairId, "Syncing", null),
            new DesktopSyncPairStatusSnapshot(activePairId, "Syncing", null),
        ]));
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            activePairId,
            SyncRunProgressStage.ScanningLocal,
            FilesCompleted: 0,
            FilesTotal: null,
            CurrentPath: string.Empty,
            StartedAtUtc: new DateTime(2026, 6, 4, 8, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 4, 8, 0, 1, DateTimeKind.Utc)));

        controller.ReportStatus(new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(
                completedPairId,
                "Idle",
                null,
                LastSyncedAtUtc: new DateTime(2026, 6, 4, 8, 1, 0, DateTimeKind.Utc)),
            new DesktopSyncPairStatusSnapshot(activePairId, "Syncing", null),
        ]));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasNotifications, Is.True);
            Assert.That(viewModel.HasCurrentWorkProgress, Is.True);
            Assert.That(viewModel.HasDashboardNotifications, Is.False);
            Assert.That(viewModel.CurrentWorkProgressTitle, Is.EqualTo("Videos"));
        });
    }

    [Test]
    public async Task Initialize_AsksToEnableFolderWhenAllPairsAreDisabled()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(Guid.NewGuid(), "Documents", "Disabled")));
        using ShellViewModel viewModel = CreateViewModel(controller);

        await viewModel.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsStatusCardVisible, Is.True);
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Enable a folder to start syncing."));
        });
    }

    [Test]
    public async Task InitializeAsync_UsesRememberedUsernameWhenRestoredAccountNameIsBlank()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot() with
        {
            AccountName = "   ",
            RememberedUsername = "  desktop@example.test  ",
        });
        using ShellViewModel viewModel = CreateViewModel(controller);

        await viewModel.InitializeAsync();

        ActivityRowViewModel sessionActivity = viewModel.Activities.First(static activity => activity.Kind == "Account");
        IReadOnlyDictionary<string, string> diagnostics = viewModel.DiagnosticsItems
            .ToDictionary(static item => item.Label, static item => item.Value);
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.AccountName, Is.EqualTo("desktop@example.test"));
            Assert.That(viewModel.HeaderTitleText, Is.EqualTo("desktop@example.test"));
            Assert.That(sessionActivity.Path, Is.EqualTo("desktop@example.test"));
            Assert.That(diagnostics["Account"], Is.EqualTo("desktop@example.test"));
        });
    }

    [Test]
    public async Task InitializeAsync_UsesSnapshotDeviceName()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot() with
        {
            DeviceName = "Cotton Sync Desktop (QA-WIN11)",
        });
        using ShellViewModel viewModel = CreateViewModel(controller);

        await viewModel.InitializeAsync();

        Assert.That(viewModel.DeviceName, Is.EqualTo("Cotton Sync Desktop (QA-WIN11)"));
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
    public async Task ActivityReported_CoalescesHighVolumeTransferBurst()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        int initialActivityCount = viewModel.Activities.Count;
        DateTime startedAtUtc = new(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc);

        for (int index = 0; index < 100; index++)
        {
            string path = "Documents/file-" + index.ToString("000", CultureInfo.InvariantCulture) + ".txt";
            controller.ReportActivity(new DesktopActivitySnapshot(
                "Uploaded",
                path,
                "Uploaded " + path,
                startedAtUtc.AddMilliseconds(index * 5),
                syncPairId));
        }

        ActivityRowViewModel activity = viewModel.Activities.First();
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Activities, Has.Count.EqualTo(initialActivityCount + 1));
            Assert.That(activity.Kind, Is.EqualTo("Uploaded"));
            Assert.That(activity.Path, Is.EqualTo("Documents/file-099.txt"));
            Assert.That(activity.Details, Is.EqualTo("Uploaded Documents/file-099.txt"));
        });
    }

    [Test]
    public async Task ActivityReported_CoalescesHighVolumeTransferBurstBeforeUiQueue()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        var dispatcher = new QueuedDesktopUiDispatcher();
        using ShellViewModel viewModel = CreateViewModel(controller, uiDispatcher: dispatcher);
        await viewModel.InitializeAsync();
        int initialActivityCount = viewModel.Activities.Count;
        DateTime startedAtUtc = new(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc);

        for (int index = 0; index < 100; index++)
        {
            string path = "Documents/file-" + index.ToString("000", CultureInfo.InvariantCulture) + ".txt";
            controller.ReportActivity(new DesktopActivitySnapshot(
                "Uploaded",
                path,
                "Uploaded " + path,
                startedAtUtc.AddMilliseconds(index * 5),
                syncPairId));
        }

        Assert.Multiple(() =>
        {
            Assert.That(dispatcher.PostedActionCount, Is.EqualTo(1));
            Assert.That(dispatcher.PendingActionCount, Is.EqualTo(1));
            Assert.That(viewModel.Activities, Has.Count.EqualTo(initialActivityCount));
        });

        dispatcher.DrainAll();

        ActivityRowViewModel activity = viewModel.Activities.First();
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Activities, Has.Count.EqualTo(initialActivityCount + 1));
            Assert.That(activity.Kind, Is.EqualTo("Uploaded"));
            Assert.That(activity.Path, Is.EqualTo("Documents/file-099.txt"));
        });
    }

    [Test]
    public async Task ActivityReported_DoesNotCoalesceDifferentSyncPairTransferRows()
    {
        Guid documentsPairId = Guid.NewGuid();
        Guid videosPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshot(
                CreatePair(documentsPairId, "Documents", "Syncing"),
                CreatePair(videosPairId, "Videos", "Syncing")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        int initialActivityCount = viewModel.Activities.Count;
        DateTime occurredAtUtc = new(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc);

        controller.ReportActivity(new DesktopActivitySnapshot(
            "Uploaded",
            "Documents/report.txt",
            "Uploaded Documents/report.txt",
            occurredAtUtc,
            documentsPairId));
        controller.ReportActivity(new DesktopActivitySnapshot(
            "Uploaded",
            "Videos/clip.mp4",
            "Uploaded Videos/clip.mp4",
            occurredAtUtc.AddMilliseconds(10),
            videosPairId));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Activities, Has.Count.EqualTo(initialActivityCount + 2));
            Assert.That(viewModel.Activities[0].Path, Is.EqualTo("Videos/clip.mp4"));
            Assert.That(viewModel.Activities[1].Path, Is.EqualTo("Documents/report.txt"));
        });
    }

    [Test]
    public async Task ActivityReported_DoesNotCoalesceDifferentSyncPairTransfersBeforeUiQueue()
    {
        Guid documentsPairId = Guid.NewGuid();
        Guid videosPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshot(
                CreatePair(documentsPairId, "Documents", "Syncing"),
                CreatePair(videosPairId, "Videos", "Syncing")));
        var dispatcher = new QueuedDesktopUiDispatcher();
        using ShellViewModel viewModel = CreateViewModel(controller, uiDispatcher: dispatcher);
        await viewModel.InitializeAsync();
        int initialActivityCount = viewModel.Activities.Count;
        DateTime occurredAtUtc = new(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc);

        controller.ReportActivity(new DesktopActivitySnapshot(
            "Uploaded",
            "Documents/report.txt",
            "Uploaded Documents/report.txt",
            occurredAtUtc,
            documentsPairId));
        controller.ReportActivity(new DesktopActivitySnapshot(
            "Uploaded",
            "Videos/clip.mp4",
            "Uploaded Videos/clip.mp4",
            occurredAtUtc.AddMilliseconds(10),
            videosPairId));

        Assert.That(dispatcher.PostedActionCount, Is.EqualTo(2));

        dispatcher.DrainAll();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Activities, Has.Count.EqualTo(initialActivityCount + 2));
            Assert.That(viewModel.Activities[0].Path, Is.EqualTo("Videos/clip.mp4"));
            Assert.That(viewModel.Activities[1].Path, Is.EqualTo("Documents/report.txt"));
        });
    }

    [Test]
    public async Task InitializeAsync_AddsDataPathsToDiagnostics()
    {
        DesktopDataPathSnapshot dataPaths = CreateTestDataPathSnapshot();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot());
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        IReadOnlyDictionary<string, string> diagnostics = viewModel.DiagnosticsItems
            .ToDictionary(static item => item.Label, static item => item.Value);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics["Data folder"], Is.EqualTo(dataPaths.DataDirectory));
            Assert.That(diagnostics["Preferences database"], Is.EqualTo(dataPaths.AppDatabasePath));
            Assert.That(diagnostics["Sync state database"], Is.EqualTo(dataPaths.SyncStateDatabasePath));
            Assert.That(diagnostics["Token store"], Is.EqualTo(dataPaths.TokenStorePath));
        });
    }

    [Test]
    public async Task OpenDataFolderCommand_OpensDiagnosticsDataDirectory()
    {
        DesktopDataPathSnapshot dataPaths = CreateTestDataPathSnapshot();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot());
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        Assert.That(viewModel.OpenDataFolderCommand.CanExecute(null), Is.True);

        await ExecuteAsync(viewModel.OpenDataFolderCommand);

        ActivityRowViewModel activity = viewModel.Activities.First();
        Assert.Multiple(() =>
        {
            Assert.That(controller.OpenedFolderPath, Is.EqualTo(dataPaths.DataDirectory));
            Assert.That(activity.Kind, Is.EqualTo("Open"));
            Assert.That(activity.Path, Is.EqualTo(dataPaths.DataDirectory));
            Assert.That(activity.Details, Is.EqualTo("Data folder opened"));
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
            Assert.That(viewModel.HasLastDiagnosticsBundlePath, Is.True);
            Assert.That(viewModel.LastDiagnosticsBundlePath, Is.EqualTo(controller.ExportDiagnosticsPath));
            Assert.That(viewModel.OpenDiagnosticsBundleFolderCommand.CanExecute(null), Is.True);
            Assert.That(activity.Kind, Is.EqualTo("Diagnostics"));
            Assert.That(activity.Path, Is.EqualTo(controller.ExportDiagnosticsPath));
            Assert.That(activity.Details, Does.Contain(controller.ExportDiagnosticsPath));
        });

        await ExecuteAsync(viewModel.OpenDiagnosticsBundleFolderCommand);

        Assert.That(
            controller.OpenedFolderPath,
            Is.EqualTo(Path.GetDirectoryName(controller.ExportDiagnosticsPath)));
    }

    [Test]
    public async Task ConflictActivity_AddsConflictRow()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(
            syncPairId,
            "Documents",
            "Idle",
            new DateTime(2026, 6, 4, 8, 0, 0, DateTimeKind.Utc))));
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
            Assert.That(viewModel.HasStatusAttention, Is.True);
            Assert.That(viewModel.ConflictCountLabel, Is.EqualTo("1 conflict"));
            Assert.That(viewModel.HeaderStatusText, Is.EqualTo("Conflicts need review"));
            Assert.That(viewModel.IsStatusCardVisible, Is.False);
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Review conflicts below to continue syncing."));
            Assert.That(viewModel.SelectedConflict, Is.SameAs(conflict));
            Assert.That(conflict.SyncPairId, Is.EqualTo(syncPairId));
            Assert.That(conflict.Path, Is.EqualTo("Documents/report.txt"));
            Assert.That(conflict.Details, Is.EqualTo("Created conflict copy Documents/report.txt"));
            Assert.That(viewModel.Activities.First().Kind, Is.EqualTo("Conflict"));
        });
    }

    [Test]
    public async Task OpenConflictCommand_OpensRequestedConflictParentFolder()
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
        controller.ReportActivity(new DesktopActivitySnapshot(
            "Conflict",
            "Finance/q2.txt",
            "Created conflict copy Finance/q2.txt",
            new DateTime(2026, 6, 3, 10, 16, 0, DateTimeKind.Utc),
            syncPairId));

        ConflictRowViewModel requestedConflict = viewModel.Conflicts.Single(conflict => conflict.Path == "Finance/q2.txt");
        Assert.That(viewModel.SelectedConflict?.Path, Is.EqualTo("Reports/q1.txt"));
        await ExecuteAsync(viewModel.OpenConflictCommand, requestedConflict);

        Assert.That(controller.OpenedFolderPath, Is.EqualTo("/home/vadim/Documents/Finance"));
    }

    [Test]
    public async Task OpenConflictCommand_RejectsConflictPathOutsideSyncRoot()
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

        await ExecuteAsync(viewModel.OpenConflictCommand, viewModel.Conflicts.Single());

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
    public async Task ToggleActivityCommand_TogglesDashboardActivityVisibility()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot());
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsActivityVisible, Is.False);
            Assert.That(viewModel.IsActivityHidden, Is.True);
            Assert.That(viewModel.ActivityToggleToolTip, Is.EqualTo("Show activity"));
        });

        await ExecuteAsync(viewModel.ToggleActivityCommand);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsActivityVisible, Is.True);
            Assert.That(viewModel.IsActivityHidden, Is.False);
            Assert.That(viewModel.ActivityToggleToolTip, Is.EqualTo("Hide activity"));
        });
    }

    [Test]
    public async Task ShowAddSyncPairCommand_LoadsRemoteRootFolders()
    {
        var localFolderPicker = new FakeLocalFolderPicker();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot());
        controller.RemoteFoldersByPath["/"] = new DesktopRemoteFolderListSnapshot(
            "/",
            [
                new DesktopRemoteFolderSnapshot(Guid.NewGuid(), "Documents", "/Documents"),
                new DesktopRemoteFolderSnapshot(Guid.NewGuid(), "Pictures", "/Pictures"),
            ]);
        using ShellViewModel viewModel = CreateViewModel(controller, localFolderPicker: localFolderPicker);
        await viewModel.InitializeAsync();
        viewModel.LocalFolderPath = "/home/user/Cotton";

        await ExecuteAsync(viewModel.ShowAddSyncPairCommand);

        Assert.Multiple(() =>
        {
            Assert.That(localFolderPicker.PickFolderCalls, Is.Zero);
            Assert.That(viewModel.IsAddSyncPairWizardVisible, Is.True);
            Assert.That(viewModel.RemoteBrowserPath, Is.EqualTo("/"));
            Assert.That(viewModel.RemoteFolderPath, Is.EqualTo("/"));
            Assert.That(viewModel.RemoteFolderSelectionLabel, Is.EqualTo("Cloud folder: /"));
            Assert.That(viewModel.RemoteFolders.Select(static folder => folder.Name), Is.EqualTo(new[] { "Documents", "Pictures" }));
            Assert.That(viewModel.SelectedRemoteFolder, Is.Null);
            Assert.That(viewModel.OpenRemoteFolderCommand.CanExecute(null), Is.False);
            Assert.That(controller.ListRemoteFolderPaths, Is.EqualTo(new[] { "/" }));
        });
    }

    [Test]
    public async Task ShowAddSyncPairCommand_OpensLocalStepWithoutPromptingForFolder()
    {
        var localFolderPicker = new FakeLocalFolderPicker("/home/user/Cotton");
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot());
        using ShellViewModel viewModel = CreateViewModel(controller, localFolderPicker: localFolderPicker);
        await viewModel.InitializeAsync();

        await ExecuteAsync(viewModel.ShowAddSyncPairCommand);

        Assert.Multiple(() =>
        {
            Assert.That(localFolderPicker.PickFolderCalls, Is.Zero);
            Assert.That(viewModel.LocalFolderPath, Is.Empty);
            Assert.That(viewModel.IsAddSyncPairWizardVisible, Is.True);
            Assert.That(viewModel.IsDashboardChromeVisible, Is.False);
            Assert.That(viewModel.IsAddSyncPairLocalStepVisible, Is.True);
            Assert.That(viewModel.IsAddSyncPairCloudStepVisible, Is.False);
            Assert.That(viewModel.RemoteBrowserPath, Is.EqualTo("/"));
            Assert.That(viewModel.RemoteFolderPath, Is.Empty);
            Assert.That(viewModel.RemoteFolders, Is.Empty);
            Assert.That(controller.ListRemoteFolderPaths, Is.Empty);
        });
    }

    [Test]
    public async Task BrowseLocalFolderCommand_StaysOnLocalStepWhenFolderSelectionIsCanceled()
    {
        var localFolderPicker = new FakeLocalFolderPicker();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot());
        controller.RemoteFoldersByPath["/"] = new DesktopRemoteFolderListSnapshot("/", []);
        using ShellViewModel viewModel = CreateViewModel(controller, localFolderPicker: localFolderPicker);
        await viewModel.InitializeAsync();

        await ExecuteAsync(viewModel.ShowAddSyncPairCommand);
        await ExecuteAsync(viewModel.BrowseLocalFolderCommand);

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
    public async Task BrowseLocalFolderCommand_LoadsCloudStepAfterSelection()
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
        await ExecuteAsync(viewModel.BrowseLocalFolderCommand);

        Assert.Multiple(() =>
        {
            Assert.That(localFolderPicker.PickFolderCalls, Is.EqualTo(1));
            Assert.That(viewModel.LocalFolderPath, Is.EqualTo("/home/user/Cotton"));
            Assert.That(viewModel.IsAddSyncPairLocalStepVisible, Is.False);
            Assert.That(viewModel.IsAddSyncPairCloudStepVisible, Is.True);
            Assert.That(viewModel.RemoteBrowserPath, Is.EqualTo("/"));
            Assert.That(viewModel.RemoteFolderPath, Is.EqualTo("/"));
            Assert.That(viewModel.RemoteFolders.Single().Name, Is.EqualTo("Documents"));
            Assert.That(controller.ListRemoteFolderPaths, Is.EqualTo(new[] { "/" }));
        });
    }

    [TestCase("/home/user/Downloads", "/home/user/Downloads", "This folder is already syncing.")]
    [TestCase("/home/user/Downloads", "/home/user/Downloads/Work", "Sync folders cannot be inside each other.")]
    [TestCase(@"C:\Users\Vadim\Downloads", @"c:\users\vadim\downloads\Work", "Sync folders cannot be inside each other.")]
    public async Task BrowseLocalFolderCommand_RejectsExistingOrNestedSyncRootBeforeCloudStep(
        string existingLocalPath,
        string selectedLocalPath,
        string expectedMessage)
    {
        var localFolderPicker = new FakeLocalFolderPicker(selectedLocalPath);
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(
            Guid.NewGuid(),
            "Downloads",
            "Idle",
            localPath: existingLocalPath)));
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
            Assert.That(localFolderPicker.PickFolderCalls, Is.EqualTo(1));
            Assert.That(viewModel.LocalFolderPath, Is.Empty);
            Assert.That(viewModel.IsAddSyncPairWizardVisible, Is.True);
            Assert.That(viewModel.IsAddSyncPairLocalStepVisible, Is.True);
            Assert.That(viewModel.IsAddSyncPairCloudStepVisible, Is.False);
            Assert.That(viewModel.RemoteFolderPath, Is.Empty);
            Assert.That(viewModel.RemoteFolders, Is.Empty);
            Assert.That(controller.ListRemoteFolderPaths, Is.Empty);
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Action required"));
            Assert.That(viewModel.ActionRequiredMessage, Is.EqualTo(expectedMessage));
            Assert.That(viewModel.AddSyncPairCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public async Task BrowseLocalFolderCommand_ClearsOverlapErrorWhenNextSelectionIsValid()
    {
        var localFolderPicker = new FakeLocalFolderPicker("/home/user/Downloads", "/home/user/Cotton");
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(
            Guid.NewGuid(),
            "Downloads",
            "Idle",
            localPath: "/home/user/Downloads")));
        controller.RemoteFoldersByPath["/"] = new DesktopRemoteFolderListSnapshot(
            "/",
            [
                new DesktopRemoteFolderSnapshot(Guid.NewGuid(), "Documents", "/Documents"),
            ]);
        using ShellViewModel viewModel = CreateViewModel(controller, localFolderPicker: localFolderPicker);
        await viewModel.InitializeAsync();
        await ExecuteAsync(viewModel.ShowAddSyncPairCommand);
        await ExecuteAsync(viewModel.BrowseLocalFolderCommand);

        await ExecuteAsync(viewModel.BrowseLocalFolderCommand);

        Assert.Multiple(() =>
        {
            Assert.That(localFolderPicker.PickFolderCalls, Is.EqualTo(2));
            Assert.That(viewModel.LocalFolderPath, Is.EqualTo("/home/user/Cotton"));
            Assert.That(viewModel.IsAddSyncPairCloudStepVisible, Is.True);
            Assert.That(viewModel.RemoteFolderPath, Is.EqualTo("/"));
            Assert.That(viewModel.RemoteFolders.Single().Name, Is.EqualTo("Documents"));
            Assert.That(controller.ListRemoteFolderPaths, Is.EqualTo(new[] { "/" }));
            Assert.That(viewModel.HasActionRequired, Is.False);
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Connected"));
        });
    }

    [Test]
    public async Task MissingDesktopSyncChangesApi_BlocksAddFolderFlowWithoutReplacingTheServerError()
    {
        var localFolderPicker = new FakeLocalFolderPicker("/home/user/Downloads");
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(
            Guid.NewGuid(),
            "Downloads",
            "Idle",
            localPath: "/home/user/Downloads")))
        {
            SelfTestSnapshot = new DesktopSelfTestSnapshot(
            [
                new DesktopSelfTestItemSnapshot(
                    "Desktop sync change feed",
                    false,
                    "Cotton API request GET /api/v1/sync/changes?since=0&limit=1 returned invalid JSON "
                    + "with content type 'text/html' and status 200 (OK)."),
            ]),
        };
        using ShellViewModel viewModel = CreateViewModel(controller, localFolderPicker: localFolderPicker);
        await viewModel.InitializeAsync();
        viewModel.LocalFolderPath = "/home/user/Cotton";
        viewModel.RemoteFolderPath = "/";

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ShowAddSyncPairCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.BrowseLocalFolderCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.AddSyncPairCommand.CanExecute(null), Is.True);
        });

        await ExecuteAsync(viewModel.ShowAddSyncPairCommand);
        await ExecuteAsync(viewModel.SelfTestCommand);
        viewModel.BrowseLocalFolderCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(localFolderPicker.PickFolderCalls, Is.Zero);
            Assert.That(viewModel.ShowAddSyncPairCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.BrowseLocalFolderCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.AddSyncPairCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Action required"));
            Assert.That(
                viewModel.ActionRequiredMessage,
                Is.EqualTo("This Cotton server does not expose the desktop sync changes API yet. Deploy the latest Cotton backend and retry sync."));
        });

        await ExecuteAsync(viewModel.ExportDiagnosticsCommand);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ShowAddSyncPairCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.BrowseLocalFolderCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.AddSyncPairCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Action required"));
            Assert.That(
                viewModel.ActionRequiredMessage,
                Is.EqualTo("This Cotton server does not expose the desktop sync changes API yet. Deploy the latest Cotton backend and retry sync."));
            Assert.That(viewModel.HasLastDiagnosticsBundlePath, Is.True);
        });
    }

    [Test]
    public async Task SelfTestPass_ClearsMissingDesktopSyncChangesApiAddFolderBlock()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot())
        {
            SelfTestSnapshot = new DesktopSelfTestSnapshot(
            [
                new DesktopSelfTestItemSnapshot(
                    "Desktop sync change feed",
                    false,
                    "Cotton API request GET /api/v1/sync/changes?since=0&limit=1 returned invalid JSON "
                    + "with content type 'text/html' and status 200 (OK)."),
            ]),
        };
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        viewModel.LocalFolderPath = "/home/user/Cotton";
        viewModel.RemoteFolderPath = "/";

        await ExecuteAsync(viewModel.SelfTestCommand);
        Assert.That(viewModel.AddSyncPairCommand.CanExecute(null), Is.False);

        controller.SelfTestSnapshot = new DesktopSelfTestSnapshot(
        [
            new DesktopSelfTestItemSnapshot("Desktop sync change feed", true, "Ready"),
        ]);

        await ExecuteAsync(viewModel.SelfTestCommand);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Self-test passed"));
            Assert.That(viewModel.HasActionRequired, Is.False);
            Assert.That(viewModel.AddSyncPairCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public async Task SelfTest_BlocksAddFolderWhenMissingSyncApiFailureIsNotFirst()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot())
        {
            SelfTestSnapshot = new DesktopSelfTestSnapshot(
            [
                new DesktopSelfTestItemSnapshot("Token storage", false, "Restricted file storage is not release-secure."),
                new DesktopSelfTestItemSnapshot(
                    "Desktop sync change feed",
                    false,
                    "Cotton API request GET /api/v1/sync/changes?since=0&limit=1 returned invalid JSON "
                    + "with content type 'text/html' and status 200 (OK)."),
            ]),
        };
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        viewModel.LocalFolderPath = "/home/user/Cotton";
        viewModel.RemoteFolderPath = "/";

        await ExecuteAsync(viewModel.SelfTestCommand);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Action required"));
            Assert.That(viewModel.ActionRequiredMessage, Is.EqualTo("Restricted file storage is not release-secure."));
            Assert.That(viewModel.ShowAddSyncPairCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.AddSyncPairCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public async Task StatusChanged_MissingDesktopSyncChangesApiBlocksAddFolderFlow()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshotWithNotifications(
                enableNotifications: false,
                CreatePair(syncPairId, "Documents", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        viewModel.LocalFolderPath = "/home/user/Cotton";
        viewModel.RemoteFolderPath = "/";

        controller.ReportStatus(new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(
                syncPairId,
                "Error",
                "Cotton API request GET /api/v1/sync/changes?since=0&limit=1 returned invalid JSON "
                + "with content type 'text/html' and status 200 (OK)."),
        ]));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Action required"));
            Assert.That(
                viewModel.ActionRequiredMessage,
                Is.EqualTo("This Cotton server does not expose the desktop sync changes API yet. Deploy the latest Cotton backend and retry sync."));
            Assert.That(viewModel.ShowAddSyncPairCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.AddSyncPairCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public async Task CancelAddSyncPairCommand_ClearsLocalFolderOverlapError()
    {
        var localFolderPicker = new FakeLocalFolderPicker("/home/user/Downloads");
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(
            Guid.NewGuid(),
            "Downloads",
            "Idle",
            localPath: "/home/user/Downloads")));
        using ShellViewModel viewModel = CreateViewModel(controller, localFolderPicker: localFolderPicker);
        await viewModel.InitializeAsync();
        await ExecuteAsync(viewModel.ShowAddSyncPairCommand);
        await ExecuteAsync(viewModel.BrowseLocalFolderCommand);

        await ExecuteAsync(viewModel.CancelAddSyncPairCommand);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsAddSyncPairWizardVisible, Is.False);
            Assert.That(viewModel.IsDashboardChromeVisible, Is.True);
            Assert.That(viewModel.LocalFolderPath, Is.Empty);
            Assert.That(viewModel.HasActionRequired, Is.False);
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Connected"));
        });
    }

    [Test]
    public async Task CreateRemoteFolderCommand_CreatesFolderAndUsesItAsCurrentCloudTarget()
    {
        var localFolderPicker = new FakeLocalFolderPicker("/home/user/Cotton");
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot());
        controller.RemoteFoldersByPath["/"] = new DesktopRemoteFolderListSnapshot("/", []);
        using ShellViewModel viewModel = CreateViewModel(controller, localFolderPicker: localFolderPicker);
        await viewModel.InitializeAsync();
        await ExecuteAsync(viewModel.ShowAddSyncPairCommand);
        await ExecuteAsync(viewModel.BrowseLocalFolderCommand);

        await ExecuteAsync(viewModel.ShowCreateRemoteFolderCommand);
        viewModel.NewRemoteFolderName = "  Projects  ";
        await ExecuteAsync(viewModel.CreateRemoteFolderCommand);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsCreateRemoteFolderVisible, Is.False);
            Assert.That(viewModel.NewRemoteFolderName, Is.Empty);
            Assert.That(controller.CreatedRemoteFolders, Is.EqualTo(new[] { ("/", "Projects") }));
            Assert.That(viewModel.RemoteBrowserPath, Is.EqualTo("/Projects"));
            Assert.That(viewModel.RemoteFolderPath, Is.EqualTo("/Projects"));
            Assert.That(viewModel.RemoteFolderSelectionLabel, Is.EqualTo("Cloud folder: /Projects"));
            Assert.That(viewModel.HasActionRequired, Is.False);
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
        await ExecuteAsync(viewModel.BrowseLocalFolderCommand);
        viewModel.SelectedRemoteFolder = viewModel.RemoteFolders.Single();

        await ExecuteAsync(viewModel.OpenRemoteFolderCommand);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.RemoteBrowserPath, Is.EqualTo("/Documents"));
            Assert.That(viewModel.RemoteFolderPath, Is.EqualTo("/Documents"));
            Assert.That(viewModel.RemoteFolderSelectionLabel, Is.EqualTo("Cloud folder: /Documents"));
            Assert.That(viewModel.RemoteFolders.Single().Id, Is.EqualTo(archiveId));
            Assert.That(viewModel.SelectedRemoteFolder, Is.Null);
            Assert.That(viewModel.OpenRemoteFolderCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.RemoteFolderUpCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public async Task UseRemoteFolderCommand_AddsSyncPairInAddMode()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot());
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        viewModel.LocalFolderPath = "/home/user/Cotton";
        viewModel.RemoteFolderPath = "/Documents";

        await ExecuteAsync(viewModel.UseRemoteFolderCommand);

        Assert.Multiple(() =>
        {
            Assert.That(controller.AddedSyncPairRequest, Is.Not.Null);
            Assert.That(controller.AddedSyncPairRequest!.LocalFolderPath, Is.EqualTo("/home/user/Cotton"));
            Assert.That(controller.AddedSyncPairRequest.RemoteFolderPath, Is.EqualTo("/Documents"));
            Assert.That(viewModel.SyncPairs, Has.Count.EqualTo(1));
            Assert.That(viewModel.SyncPairs.Single().RemotePath, Is.EqualTo("/Documents"));
            Assert.That(viewModel.IsAddSyncPairWizardVisible, Is.False);
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Sync requested"));
        });
    }

    [Test]
    public async Task ChangeSelectedSyncPairRemoteFolderCommand_OpensCloudPickerForSelectedPair()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(
            syncPairId,
            "Documents",
            "Idle",
            localPath: "/home/user/Cotton",
            remotePath: "/Documents")));
        controller.RemoteFoldersByPath["/Documents"] = new DesktopRemoteFolderListSnapshot(
            "/Documents",
            [
                new DesktopRemoteFolderSnapshot(Guid.NewGuid(), "Archive", "/Documents/Archive"),
            ]);
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();

        await ExecuteAsync(viewModel.ChangeSelectedSyncPairRemoteFolderCommand);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsAddSyncPairWizardVisible, Is.True);
            Assert.That(viewModel.IsEditingSelectedSyncPairRemoteFolder, Is.True);
            Assert.That(viewModel.IsAddSyncPairLocalStepVisible, Is.False);
            Assert.That(viewModel.IsAddSyncPairCloudStepVisible, Is.True);
            Assert.That(viewModel.IsAddSyncPairLocalSummaryVisible, Is.False);
            Assert.That(viewModel.AddSyncPairWizardTitle, Is.EqualTo("Change cloud folder"));
            Assert.That(viewModel.AddSyncPairWizardSubtitle, Is.EqualTo("Pick the Cotton Cloud folder for Documents."));
            Assert.That(viewModel.RemoteFolderWizardPrimaryActionText, Is.EqualTo("Update cloud folder"));
            Assert.That(viewModel.LocalFolderPath, Is.EqualTo("/home/user/Cotton"));
            Assert.That(viewModel.RemoteBrowserPath, Is.EqualTo("/Documents"));
            Assert.That(viewModel.RemoteFolderPath, Is.EqualTo("/Documents"));
            Assert.That(viewModel.RemoteFolders.Single().Name, Is.EqualTo("Archive"));
            Assert.That(controller.ListRemoteFolderPaths, Is.EqualTo(new[] { "/Documents" }));
            Assert.That(viewModel.AddSyncPairCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.BrowseLocalFolderCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.UseRemoteFolderCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public async Task UseRemoteFolderCommand_UpdatesSelectedPairRemoteFolderInEditMode()
    {
        Guid syncPairId = Guid.NewGuid();
        Guid remoteRootNodeId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(
            syncPairId,
            "Documents",
            "Idle",
            localPath: "/home/user/Cotton",
            remotePath: "/Documents")))
        {
            RemoteFolderRootNodeId = remoteRootNodeId,
        };
        controller.RemoteFoldersByPath["/Documents"] = new DesktopRemoteFolderListSnapshot(
            "/Documents",
            [
                new DesktopRemoteFolderSnapshot(remoteRootNodeId, "Archive", "/Documents/Archive"),
            ]);
        controller.RemoteFoldersByPath["/Documents/Archive"] = new DesktopRemoteFolderListSnapshot("/Documents/Archive", []);
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        await ExecuteAsync(viewModel.ChangeSelectedSyncPairRemoteFolderCommand);
        viewModel.SelectedRemoteFolder = viewModel.RemoteFolders.Single();
        await ExecuteAsync(viewModel.OpenRemoteFolderCommand);

        await ExecuteAsync(viewModel.UseRemoteFolderCommand);

        Assert.Multiple(() =>
        {
            Assert.That(controller.RemoteFolderSyncPairId, Is.EqualTo(syncPairId));
            Assert.That(controller.RemoteFolderPath, Is.EqualTo("/Documents/Archive"));
            Assert.That(viewModel.SelectedSyncPair!.RemoteRootNodeId, Is.EqualTo(remoteRootNodeId));
            Assert.That(viewModel.SelectedSyncPair.RemotePath, Is.EqualTo("/Documents/Archive"));
            Assert.That(viewModel.IsAddSyncPairWizardVisible, Is.False);
            Assert.That(viewModel.IsEditingSelectedSyncPairRemoteFolder, Is.False);
            Assert.That(viewModel.LocalFolderPath, Is.Empty);
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Cloud folder updated"));
            Assert.That(viewModel.Activities.First().Details, Is.EqualTo("Cloud folder changed to /Documents/Archive"));
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
    public async Task InitializeAsync_ShowsStartupLoadingInsteadOfSetupWhileRestoringSession()
    {
        var loadCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(Guid.NewGuid(), "Cloud", "Idle")))
        {
            LoadCompletion = loadCompletion,
        };
        using ShellViewModel viewModel = CreateViewModel(controller);

        Task initializeTask = viewModel.InitializeAsync();
        await WaitForAsync(() => controller.LoadStarted);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsStartupLoadingVisible, Is.True);
            Assert.That(viewModel.IsSetupVisible, Is.False);
            Assert.That(viewModel.IsServerStepVisible, Is.False);
            Assert.That(viewModel.IsSignInStepVisible, Is.False);
            Assert.That(viewModel.IsDashboardVisible, Is.False);
        });

        loadCompletion.SetResult(true);
        await initializeTask;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsStartupLoadingVisible, Is.False);
            Assert.That(viewModel.IsSignedIn, Is.True);
            Assert.That(viewModel.IsSetupVisible, Is.False);
            Assert.That(viewModel.IsDashboardVisible, Is.True);
        });
    }

    [Test]
    public async Task InitializeAsync_WhenLoadFailsBeforeSignInStepShowsActionRequired()
    {
        var controller = new FakeDesktopShellController(CreateSignedOutSnapshot())
        {
            LoadException = new InvalidOperationException("Preferences database is unavailable."),
        };
        using ShellViewModel viewModel = CreateViewModel(controller);

        await viewModel.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSignedIn, Is.False);
            Assert.That(viewModel.IsServerStepVisible, Is.True);
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Action required"));
            Assert.That(viewModel.ActionRequiredMessage, Is.EqualTo("Preferences database is unavailable."));
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Sign in to continue."));
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
    public async Task SignInCommand_LeavesAddFolderWizardClosedWhenNoSyncPairsExist()
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
            Assert.That(viewModel.IsAddSyncPairWizardVisible, Is.False);
            Assert.That(viewModel.IsAddSyncPairLocalStepVisible, Is.False);
            Assert.That(viewModel.HasNoSyncPairs, Is.True);
            Assert.That(viewModel.HeaderTitleText, Is.EqualTo("desktop@example.test"));
            Assert.That(viewModel.HeaderStatusText, Is.EqualTo("Connected"));
            Assert.That(viewModel.IsStatusCardVisible, Is.False);
            Assert.That(viewModel.CurrentProgressText, Is.Empty);
            Assert.That(viewModel.RemoteBrowserPath, Is.EqualTo("/"));
            Assert.That(viewModel.RemoteFolderPath, Is.Empty);
            Assert.That(viewModel.RemoteFolders, Is.Empty);
            Assert.That(controller.ListRemoteFolderPaths, Is.Empty);
            Assert.That(controller.SignInRequest?.ServerUrl, Is.EqualTo("https://app.cottoncloud.dev/"));
        });
    }

    [Test]
    public async Task SignInCommand_ShowsNativeNotificationWhenSupported()
    {
        var controller = new FakeDesktopShellController(CreateSignedOutSnapshot())
        {
            ServerProbeResult = new DesktopServerProbeResult(
                new Uri("https://app.cottoncloud.dev/"),
                true,
                "Cotton Cloud",
                "instance-hash"),
        };
        var notificationService = new CollectingDesktopNotificationService();
        using ShellViewModel viewModel = CreateViewModel(controller, notificationService: notificationService);
        await viewModel.InitializeAsync();
        viewModel.ServerUrl = "app.cottoncloud.dev";
        viewModel.Username = "desktop@example.test";
        viewModel.Password = "password";
        await WaitForAsync(() => viewModel.IsSignInStepVisible);

        await ExecuteAsync(viewModel.SignInCommand);

        Assert.Multiple(() =>
        {
            Assert.That(notificationService.Notifications, Has.Count.EqualTo(1));
            Assert.That(notificationService.Notifications[0].Title, Is.EqualTo("Signed in"));
            Assert.That(notificationService.Notifications[0].Message, Is.EqualTo("desktop@example.test"));
        });
    }

    [Test]
    public async Task SignInWithBrowserCommand_UsesVerifiedServerAndAppliesSession()
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
        await WaitForAsync(() => viewModel.IsSignInStepVisible);

        await ExecuteAsync(viewModel.SignInWithBrowserCommand);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSignedIn, Is.True);
            Assert.That(viewModel.HeaderTitleText, Is.EqualTo("browser@example.test"));
            Assert.That(viewModel.HeaderStatusText, Is.EqualTo("Connected"));
            Assert.That(viewModel.Password, Is.Empty);
            Assert.That(viewModel.TotpCode, Is.Empty);
            Assert.That(viewModel.BrowserSignInStatus, Is.Empty);
            Assert.That(viewModel.IsBrowserSignInPending, Is.False);
            Assert.That(controller.BrowserSignInServerUrl, Is.EqualTo("https://app.cottoncloud.dev/"));
            Assert.That(controller.SignInRequest, Is.Null);
        });
    }

    [Test]
    public async Task SignInWithBrowserCommand_CanCancelPendingApproval()
    {
        var controller = new FakeDesktopShellController(CreateSignedOutSnapshot())
        {
            ServerProbeResult = new DesktopServerProbeResult(
                new Uri("https://app.cottoncloud.dev/"),
                true,
                "Cotton Cloud",
                "instance-hash"),
            BrowserSignInCompletion = new TaskCompletionSource<AuthSession>(
                TaskCreationOptions.RunContinuationsAsynchronously),
        };
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        viewModel.ServerUrl = "app.cottoncloud.dev";
        await WaitForAsync(() => viewModel.IsSignInStepVisible);

        viewModel.SignInWithBrowserCommand.Execute(null);
        await WaitForAsync(() => viewModel.IsBrowserSignInPending);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsBusy, Is.True);
            Assert.That(viewModel.BrowserSignInButtonText, Is.EqualTo("Waiting for approval"));
            Assert.That(viewModel.BrowserSignInStatus, Is.EqualTo("Approve this sign-in in your browser."));
            Assert.That(viewModel.IsPasswordSignInVisible, Is.False);
            Assert.That(viewModel.CancelBrowserSignInCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.SignInWithBrowserCommand.CanExecute(null), Is.False);
            Assert.That(controller.BrowserSignInServerUrl, Is.EqualTo("https://app.cottoncloud.dev/"));
        });

        await ExecuteAsync(viewModel.CancelBrowserSignInCommand);
        await WaitForAsync(() => viewModel.GlobalStatus == "Sign-in cancelled");

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSignedIn, Is.False);
            Assert.That(viewModel.IsBrowserSignInPending, Is.False);
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.BrowserSignInStatus, Is.Empty);
            Assert.That(viewModel.IsPasswordSignInVisible, Is.True);
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Sign-in cancelled"));
            Assert.That(viewModel.Activities.First().Details, Is.EqualTo("Browser sign-in cancelled"));
        });
    }

    [Test]
    public async Task SignInCommand_DoesNotShowNativeNotificationWhenDisabled()
    {
        var controller = new FakeDesktopShellController(CreateSignedOutSnapshot(enableNotifications: false))
        {
            ServerProbeResult = new DesktopServerProbeResult(
                new Uri("https://app.cottoncloud.dev/"),
                true,
                "Cotton Cloud",
                "instance-hash"),
        };
        var notificationService = new CollectingDesktopNotificationService();
        using ShellViewModel viewModel = CreateViewModel(controller, notificationService: notificationService);
        await viewModel.InitializeAsync();
        viewModel.ServerUrl = "app.cottoncloud.dev";
        viewModel.Username = "desktop@example.test";
        viewModel.Password = "password";
        await WaitForAsync(() => viewModel.IsSignInStepVisible);

        await ExecuteAsync(viewModel.SignInCommand);

        Assert.That(notificationService.Notifications, Is.Empty);
    }

    [Test]
    public async Task InitializeAsync_ShowsSessionRestoredNotificationWhenVisibleLaunchAllowsIt()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot());
        var notificationService = new CollectingDesktopNotificationService();
        using ShellViewModel viewModel = CreateViewModel(
            controller,
            notificationService: notificationService,
            notifyOnSessionRestore: true);

        await viewModel.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(notificationService.Notifications, Has.Count.EqualTo(1));
            Assert.That(notificationService.Notifications[0].Title, Is.EqualTo("Session restored"));
            Assert.That(notificationService.Notifications[0].Message, Is.EqualTo("vadim@example.com"));
        });
    }

    [Test]
    public async Task InitializeAsync_DoesNotShowSessionRestoredNotificationWhenStartupNoiseSuppressed()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot());
        var notificationService = new CollectingDesktopNotificationService();
        using ShellViewModel viewModel = CreateViewModel(
            controller,
            notificationService: notificationService,
            notifyOnSessionRestore: false);

        await viewModel.InitializeAsync();

        Assert.That(notificationService.Notifications, Is.Empty);
    }

    [Test]
    public async Task InitializeAsync_DoesNotShowSessionRestoredNotificationWhenNotificationsDisabled()
    {
        var controller = new FakeDesktopShellController(
            CreateSignedInSnapshotWithNotifications(enableNotifications: false));
        var notificationService = new CollectingDesktopNotificationService();
        using ShellViewModel viewModel = CreateViewModel(
            controller,
            notificationService: notificationService,
            notifyOnSessionRestore: true);

        await viewModel.InitializeAsync();

        Assert.That(notificationService.Notifications, Is.Empty);
    }

    [Test]
    public async Task SignInCommand_ShowsSetupErrorWhenAuthenticationFails()
    {
        var controller = new FakeDesktopShellController(CreateSignedOutSnapshot())
        {
            ServerProbeResult = new DesktopServerProbeResult(
                new Uri("https://app.cottoncloud.dev/"),
                true,
                "Cotton Cloud",
                "instance-hash"),
            SignInException = new InvalidOperationException("Invalid username, password, or two-factor code."),
        };
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        viewModel.ServerUrl = "app.cottoncloud.dev";
        viewModel.Username = "desktop@example.test";
        viewModel.Password = "wrong-password";
        await WaitForAsync(() => viewModel.IsSignInStepVisible);

        viewModel.SignInCommand.Execute(null);
        await WaitForAsync(() => viewModel.HasActionRequired);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSetupVisible, Is.True);
            Assert.That(viewModel.IsSignInStepVisible, Is.True);
            Assert.That(viewModel.IsSignedIn, Is.False);
            Assert.That(viewModel.ActionRequiredMessage, Is.EqualTo("Invalid username, password, or two-factor code."));
        });
    }

    [Test]
    public async Task SignInCommand_ShowsHumanTotpRequiredMessage()
    {
        var controller = new FakeDesktopShellController(CreateSignedOutSnapshot())
        {
            ServerProbeResult = new DesktopServerProbeResult(
                new Uri("https://app.cottoncloud.dev/"),
                true,
                "Cotton Cloud",
                "instance-hash"),
            SignInException = new CottonApiException(
                HttpStatusCode.Forbidden,
                "{\"success\":false,\"message\":\"Two-factor authentication code is required\"}",
                "Cotton API request POST /api/v1/auth/login failed with status 403 (Forbidden)."),
        };
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        viewModel.ServerUrl = "app.cottoncloud.dev";
        viewModel.Username = "desktop@example.test";
        viewModel.Password = "password";
        await WaitForAsync(() => viewModel.IsSignInStepVisible);

        viewModel.SignInCommand.Execute(null);
        await WaitForAsync(() => viewModel.HasActionRequired);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSignInStepVisible, Is.True);
            Assert.That(viewModel.ActionRequiredMessage, Is.EqualTo("Enter the 2FA code for this account."));
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Sign-in failed"));
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Sign in to continue."));
        });
    }

    [Test]
    public async Task SignInCommand_ShowsHumanInvalidPasswordMessage()
    {
        var controller = new FakeDesktopShellController(CreateSignedOutSnapshot())
        {
            ServerProbeResult = new DesktopServerProbeResult(
                new Uri("https://app.cottoncloud.dev/"),
                true,
                "Cotton Cloud",
                "instance-hash"),
            SignInException = new CottonApiException(
                HttpStatusCode.Forbidden,
                "{\"success\":false,\"message\":\"Invalid password\"}",
                "Cotton API request POST /api/v1/auth/login failed with status 403 (Forbidden)."),
        };
        using ShellViewModel viewModel = CreateViewModel(controller);
        await viewModel.InitializeAsync();
        viewModel.ServerUrl = "app.cottoncloud.dev";
        viewModel.Username = "desktop@example.test";
        viewModel.Password = "wrong-password";
        await WaitForAsync(() => viewModel.IsSignInStepVisible);

        viewModel.SignInCommand.Execute(null);
        await WaitForAsync(() => viewModel.HasActionRequired);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSignInStepVisible, Is.True);
            Assert.That(viewModel.IsSignedIn, Is.False);
            Assert.That(viewModel.ActionRequiredMessage, Is.EqualTo("Invalid username or password."));
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Sign-in failed"));
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Sign in to continue."));
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
            Assert.That(viewModel.IsDashboardChromeVisible, Is.True);
            Assert.That(viewModel.SignOutCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public async Task SignOutCommand_ShowsNativeNotificationWhenSupported()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(Guid.NewGuid(), "Documents", "Idle")));
        var notificationService = new CollectingDesktopNotificationService();
        using ShellViewModel viewModel = CreateViewModel(controller, notificationService: notificationService);
        await viewModel.InitializeAsync();

        await ExecuteAsync(viewModel.SignOutCommand);

        Assert.Multiple(() =>
        {
            Assert.That(notificationService.Notifications, Has.Count.EqualTo(1));
            Assert.That(notificationService.Notifications[0].Title, Is.EqualTo("Signed out"));
            Assert.That(notificationService.Notifications[0].Message, Is.EqualTo("Cotton Sync is signed out."));
        });
    }

    [Test]
    public async Task SessionRevoked_SignsOutAndShowsNativeNotificationWhenSupported()
    {
        Guid syncPairId = Guid.NewGuid();
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(syncPairId, "Documents", "Syncing")));
        var notificationService = new CollectingDesktopNotificationService();
        using ShellViewModel viewModel = CreateViewModel(controller, notificationService: notificationService);
        await viewModel.InitializeAsync();
        viewModel.Password = "password";
        viewModel.TotpCode = "123456";
        await ExecuteAsync(viewModel.ShowSettingsCommand);

        controller.ReportSessionRevoked(new DesktopSessionRevocationSnapshot(DateTime.UtcNow));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSignedIn, Is.False);
            Assert.That(viewModel.IsSetupVisible, Is.True);
            Assert.That(viewModel.AccountName, Is.EqualTo("Signed out"));
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Session expired"));
            Assert.That(viewModel.Password, Is.Empty);
            Assert.That(viewModel.TotpCode, Is.Empty);
            Assert.That(viewModel.IsSettingsVisible, Is.False);
            Assert.That(viewModel.SyncPairs.Single().Status, Is.EqualTo("Idle"));
            Assert.That(viewModel.SignOutCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.Activities.First().Kind, Is.EqualTo("Account"));
            Assert.That(viewModel.Activities.First().Details, Is.EqualTo("Session revoked by server"));
            Assert.That(notificationService.Notifications, Has.Count.EqualTo(1));
            Assert.That(notificationService.Notifications[0].Title, Is.EqualTo("Session expired"));
            Assert.That(notificationService.Notifications[0].Message, Is.EqualTo("Sign in again to continue syncing."));
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

    [Test]
    public void AppVersion_UsesInformationalVersion()
    {
        using ShellViewModel viewModel = CreateViewModel(new FakeDesktopShellController(CreateSignedOutSnapshot()));
        string informationalVersion = typeof(ShellViewModel)
            .Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;

        Assert.That(viewModel.AppVersion, Is.EqualTo(informationalVersion));
    }

    private static async Task ExecuteAsync(AsyncRelayCommand command, object? parameter = null)
    {
        Assert.That(command.CanExecute(parameter), Is.True);
        command.Execute(parameter);
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

    private static DesktopShellSnapshot CreateSignedOutSnapshot(bool enableNotifications = true)
    {
        return new DesktopShellSnapshot(
            null,
            null,
            null,
            false,
            enableNotifications,
            AppThemeMode.System,
            CreateTestDataPathSnapshot(),
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
        return CreateSignedInSnapshotWithNotifications(enableNotifications: true, syncPairs);
    }

    private static DesktopShellSnapshot CreateSignedInSnapshotWithNotifications(
        bool enableNotifications,
        params DesktopSyncPairSnapshot[] syncPairs)
    {
        return new DesktopShellSnapshot(
            null,
            "vadim@example.com",
            "vadim@example.com",
            false,
            enableNotifications,
            AppThemeMode.System,
            CreateTestDataPathSnapshot(),
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

    private static DesktopDataPathSnapshot CreateTestDataPathSnapshot()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), "cotton-sync-test-data");
        return new DesktopDataPathSnapshot(
            dataDirectory,
            Path.Combine(dataDirectory, "sync-app.db"),
            Path.Combine(dataDirectory, "sync-state.db"),
            Path.Combine(dataDirectory, "tokens.json"));
    }

    private static DesktopSyncPairSnapshot CreatePair(
        Guid id,
        string displayName,
        string status,
        DateTime? lastSyncedAtUtc = null,
        string? localPath = null,
        string? remotePath = null)
    {
        return new DesktopSyncPairSnapshot(
            id,
            displayName,
            localPath ?? "/home/vadim/" + displayName,
            remotePath ?? "/" + displayName,
            status,
            Guid.NewGuid(),
            lastSyncedAtUtc);
    }

    private static FakeDesktopShellController CreateTwoFolderSyncingController(Guid documentsPairId, Guid videosPairId)
    {
        return new FakeDesktopShellController(
            CreateSignedInSnapshot(
                CreatePair(documentsPairId, "Documents", "Syncing"),
                CreatePair(videosPairId, "Videos", "Syncing")));
    }

    private static void ReportTwoFolderCheckingProgress(
        FakeDesktopShellController controller,
        Guid documentsPairId,
        Guid videosPairId)
    {
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            documentsPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 0,
            FilesTotal: 10,
            CurrentPath: "Reports/report.txt",
            StartedAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc)));
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            videosPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 0,
            FilesTotal: 20,
            CurrentPath: "Videos/clip.mp4",
            StartedAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc)));
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            documentsPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 3,
            FilesTotal: 10,
            CurrentPath: "Reports/report.txt",
            StartedAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 4, 9, 0, 5, DateTimeKind.Utc)));
        controller.ReportRunProgress(new DesktopRunProgressSnapshot(
            videosPairId,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 5,
            FilesTotal: 20,
            CurrentPath: "Videos/clip.mp4",
            StartedAtUtc: new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc),
            IsCompleted: false,
            OccurredAtUtc: new DateTime(2026, 6, 4, 9, 0, 6, DateTimeKind.Utc)));
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

        public event EventHandler<DesktopSessionRevocationSnapshot>? SessionRevoked;

        public event EventHandler<DesktopTransferProgressSnapshot>? TransferProgressChanged;

        public event EventHandler<DesktopRunProgressSnapshot>? RunProgressChanged;

        public Guid? EnabledSyncPairId { get; private set; }

        public bool? EnabledSyncPairValue { get; private set; }

        public Guid? RemovedSyncPairId { get; private set; }

        public Guid? RenamedSyncPairId { get; private set; }

        public string? RenamedSyncPairDisplayName { get; private set; }

        public Guid? LocalFolderSyncPairId { get; private set; }

        public string? LocalFolderPath { get; private set; }

        public Guid? RemoteFolderSyncPairId { get; private set; }

        public string? RemoteFolderPath { get; private set; }

        public Guid RemoteFolderRootNodeId { get; set; } = Guid.NewGuid();

        public DesktopSyncPairRequest? AddedSyncPairRequest { get; private set; }

        public int SignOutCalls { get; private set; }

        public DesktopSelfTestSnapshot SelfTestSnapshot { get; set; } = new([]);

        public DesktopServerProbeResult? ServerProbeResult { get; set; }

        public DesktopSignInRequest? SignInRequest { get; private set; }

        public string? BrowserSignInServerUrl { get; private set; }

        public TaskCompletionSource<AuthSession>? BrowserSignInCompletion { get; set; }

        public Exception? LoadException { get; set; }

        public TaskCompletionSource<bool>? LoadCompletion { get; set; }

        public bool LoadStarted { get; private set; }

        public Dictionary<string, DesktopRemoteFolderListSnapshot> RemoteFoldersByPath { get; } = [];

        public List<string> ListRemoteFolderPaths { get; } = [];

        public List<(string ParentPath, string FolderName)> CreatedRemoteFolders { get; } = [];

        public List<string> ProbedServerUrls { get; } = [];

        public int SyncAllCalls { get; private set; }

        public int PauseAllCalls { get; private set; }

        public int ResumeAllCalls { get; private set; }

        public Exception? SyncAllException { get; set; }

        public TaskCompletionSource<bool>? SyncAllCompletion { get; set; }

        public TaskCompletionSource<bool>? PauseAllCompletion { get; set; }

        public int ExportDiagnosticsCalls { get; private set; }

        public string ExportDiagnosticsPath { get; set; } = "/tmp/cotton-sync-diagnostics.zip";

        public string? OpenedFolderPath { get; private set; }

        public Exception? SignInException { get; set; }

        public void ReportActivity(DesktopActivitySnapshot activity)
        {
            ActivityReported?.Invoke(this, activity);
        }

        public void ReportSessionRevoked(DesktopSessionRevocationSnapshot sessionRevocation)
        {
            SessionRevoked?.Invoke(this, sessionRevocation);
        }

        public void ReportStatus(DesktopSyncStatusSnapshot status)
        {
            StatusChanged?.Invoke(this, status);
        }

        public void ReportTransferProgress(DesktopTransferProgressSnapshot progress)
        {
            TransferProgressChanged?.Invoke(this, progress);
        }

        public void ReportRunProgress(DesktopRunProgressSnapshot progress)
        {
            RunProgressChanged?.Invoke(this, progress);
        }

        public async Task<DesktopShellSnapshot> LoadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadStarted = true;
            if (LoadException is not null)
            {
                throw LoadException;
            }

            if (LoadCompletion is not null)
            {
                await LoadCompletion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return _snapshot;
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

        public Task SetSyncPairLocalFolderAsync(
            Guid syncPairId,
            string localFolderPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LocalFolderSyncPairId = syncPairId;
            LocalFolderPath = localFolderPath;
            return Task.CompletedTask;
        }

        public Task<SyncPairSettings> SetSyncPairRemoteFolderAsync(
            Guid syncPairId,
            string remoteFolderPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RemoteFolderSyncPairId = syncPairId;
            RemoteFolderPath = remoteFolderPath;
            DesktopSyncPairSnapshot? existing = _snapshot.SyncPairs.FirstOrDefault(pair => pair.Id == syncPairId);
            return Task.FromResult(new SyncPairSettings
            {
                Id = syncPairId,
                DisplayName = existing?.DisplayName ?? "Documents",
                LocalRootPath = existing?.LocalPath ?? "/home/vadim/Documents",
                RemoteRootNodeId = RemoteFolderRootNodeId,
                RemoteDisplayPath = remoteFolderPath,
                IsEnabled = true,
                Mode = SyncPairMode.FullMirror,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            });
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
            if (SignInException is not null)
            {
                throw SignInException;
            }

            return Task.FromResult(new AuthSession(
                Guid.NewGuid(),
                request.Username,
                request.Username,
                false));
        }

        public Task<AuthSession> SignInWithBrowserAsync(
            string serverUrl,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BrowserSignInServerUrl = serverUrl;
            if (SignInException is not null)
            {
                throw SignInException;
            }

            AuthSession session = new(
                Guid.NewGuid(),
                "browser",
                "browser@example.test",
                false);
            return BrowserSignInCompletion is null
                ? Task.FromResult(session)
                : WaitForBrowserSignInAsync(BrowserSignInCompletion, cancellationToken);
        }

        private static async Task<AuthSession> WaitForBrowserSignInAsync(
            TaskCompletionSource<AuthSession> completion,
            CancellationToken cancellationToken)
        {
            using CancellationTokenRegistration registration = cancellationToken.Register(
                static state =>
                {
                    var taskCompletion = (TaskCompletionSource<AuthSession>)state!;
                    taskCompletion.TrySetCanceled();
                },
                completion);
            return await completion.Task.ConfigureAwait(false);
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

        public Task<DesktopRemoteFolderSnapshot> CreateRemoteFolderAsync(
            string parentPath,
            string folderName,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreatedRemoteFolders.Add((parentPath, folderName));
            string path = parentPath == "/"
                ? "/" + folderName.Trim()
                : parentPath.TrimEnd('/') + "/" + folderName.Trim();
            var folder = new DesktopRemoteFolderSnapshot(Guid.NewGuid(), folderName.Trim(), path);
            RemoteFoldersByPath[path] = new DesktopRemoteFolderListSnapshot(path, []);
            return Task.FromResult(folder);
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
            cancellationToken.ThrowIfCancellationRequested();
            AddedSyncPairRequest = request;
            return Task.FromResult(new SyncPairSettings
            {
                Id = Guid.NewGuid(),
                DisplayName = Path.GetFileName(request.LocalFolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                LocalRootPath = request.LocalFolderPath,
                RemoteRootNodeId = Guid.NewGuid(),
                RemoteDisplayPath = request.RemoteFolderPath,
                IsEnabled = true,
                Mode = SyncPairMode.FullMirror,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            });
        }

        public async Task SyncAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (SyncAllException is not null)
            {
                throw SyncAllException;
            }

            SyncAllCalls++;
            if (SyncAllCompletion is not null)
            {
                await SyncAllCompletion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task PauseAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PauseAllCalls++;
            if (PauseAllCompletion is not null)
            {
                await PauseAllCompletion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public Task ResumeAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResumeAllCalls++;
            return Task.CompletedTask;
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
        FakeLocalFolderPicker? localFolderPicker = null,
        IDesktopNotificationService? notificationService = null,
        IDesktopUiDispatcher? uiDispatcher = null,
        bool notifyOnSessionRestore = false)
    {
        return new ShellViewModel(
            controller,
            localFolderPicker ?? new FakeLocalFolderPicker(),
            notificationService ?? new FakeDesktopNotificationService(),
            new FakeDesktopThemeService(),
            uiDispatcher ?? new InlineDesktopUiDispatcher(),
            featureFlags,
            notifyOnSessionRestore);
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

    private sealed class CollectingDesktopNotificationService : IDesktopNotificationService
    {
        public bool IsSupported => true;

        public List<(string Title, string Message)> Notifications { get; } = [];

        public void Show(string title, string message)
        {
            Notifications.Add((title, message));
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

    private sealed class QueuedDesktopUiDispatcher : IDesktopUiDispatcher
    {
        private readonly Queue<Action> _actions = [];

        public int PostedActionCount { get; private set; }

        public int PendingActionCount => _actions.Count;

        public bool CheckAccess()
        {
            return false;
        }

        public void Post(Action action)
        {
            PostedActionCount++;
            _actions.Enqueue(action);
        }

        public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            action();
            return Task.CompletedTask;
        }

        public void DrainAll()
        {
            while (_actions.Count > 0)
            {
                _actions.Dequeue()();
            }
        }
    }
}
