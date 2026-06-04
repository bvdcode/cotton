// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

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
            Assert.That(selected.ToggleEnabledLabel, Is.EqualTo("Enable"));
            Assert.That(selected.ToggleEnabledIcon, Is.EqualTo("▶"));
            Assert.That(selected.ModeLabel, Is.EqualTo("Full mirror"));
            Assert.That(selected.Status, Is.EqualTo("Disabled"));
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Folder disabled"));
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
            Assert.That(viewModel.IsRemoveSyncPairConfirmationVisible, Is.False);
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
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Sync requested"));
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
            Assert.That(viewModel.StatusCardTitle, Is.EqualTo("Sync needs attention"));
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
            Assert.That(viewModel.SelectedRemoteFolder?.Path, Is.EqualTo("/Documents"));
            Assert.That(controller.ListRemoteFolderPaths, Is.EqualTo(new[] { "/" }));
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
            Assert.That(viewModel.SelectedSettingsTabIndex, Is.EqualTo(3));
            Assert.That(viewModel.GlobalStatus, Is.EqualTo("Self-test passed"));
            Assert.That(viewModel.HasSelfTestItems, Is.True);
            Assert.That(viewModel.SelfTestItems, Has.Count.EqualTo(2));
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
            Assert.That(viewModel.StatusCardTitle, Is.EqualTo("Sync needs attention"));
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Fix the issue below to continue syncing."));
            Assert.That(viewModel.ActionRequiredMessage, Is.EqualTo("Sync API unavailable."));
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
            Assert.That(enabledPair.Status, Is.EqualTo("Sync requested"));
            Assert.That(enabledPair.CurrentOperation, Is.EqualTo("Waiting to sync changes"));
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
    public async Task Initialize_ShowsFirstSyncPendingUntilPairHasBaseline()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(Guid.NewGuid(), "Documents", "Idle")));
        using ShellViewModel viewModel = CreateViewModel(controller);

        await viewModel.InitializeAsync();

        Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Waiting for first sync."));
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

        Assert.That(viewModel.CurrentProgressText, Is.EqualTo("All folders are up to date."));
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
            Assert.That(viewModel.CurrentProgressText, Is.EqualTo("All folders are up to date."));
        });
    }

    [Test]
    public async Task Initialize_AsksToEnableFolderWhenAllPairsAreDisabled()
    {
        var controller = new FakeDesktopShellController(CreateSignedInSnapshot(CreatePair(Guid.NewGuid(), "Documents", "Disabled")));
        using ShellViewModel viewModel = CreateViewModel(controller);

        await viewModel.InitializeAsync();

        Assert.That(viewModel.CurrentProgressText, Is.EqualTo("Enable a folder to start syncing."));
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

    private static DesktopSyncPairSnapshot CreatePair(
        Guid id,
        string displayName,
        string status,
        DateTime? lastSyncedAtUtc = null)
    {
        return new DesktopSyncPairSnapshot(
            id,
            displayName,
            "/home/vadim/" + displayName,
            "/" + displayName,
            status,
            Guid.NewGuid(),
            lastSyncedAtUtc);
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

        public int PauseAllCalls { get; private set; }

        public int ResumeAllCalls { get; private set; }

        public Exception? SyncAllException { get; set; }

        public int ExportDiagnosticsCalls { get; private set; }

        public string ExportDiagnosticsPath { get; set; } = "/tmp/cotton-sync-diagnostics.zip";

        public string? OpenedFolderPath { get; private set; }

        public Exception? SignInException { get; set; }

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
            if (SyncAllException is not null)
            {
                throw SyncAllException;
            }

            SyncAllCalls++;
            return Task.CompletedTask;
        }

        public Task PauseAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PauseAllCalls++;
            return Task.CompletedTask;
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
