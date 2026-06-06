// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Tests.Shell;

public sealed class DesktopSetupVisualContractTests
{
    [Test]
    public void Application_DefaultsToDarkThemeForFirstRun()
    {
        string appXaml = File.ReadAllText(GetDesktopFilePath("App.axaml"));

        Assert.That(appXaml, Does.Contain("RequestedThemeVariant=\"Dark\""));
    }

    [Test]
    public void Application_RegistersDesktopIconLibrary()
    {
        string appXaml = File.ReadAllText(GetDesktopFilePath("App.axaml"));

        Assert.Multiple(() =>
        {
            Assert.That(appXaml, Does.Contain("Material.Icons.Avalonia"));
            Assert.That(appXaml, Does.Contain("materialIcons:MaterialIconStyles"));
        });
    }

    [Test]
    public void RefreshActions_UseUncircledIcon()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));

        Assert.Multiple(() =>
        {
            Assert.That(mainWindowXaml, Does.Not.Contain("Kind=\"RefreshCircle\""));
            Assert.That(CountOccurrences(mainWindowXaml, "Kind=\"Refresh\""), Is.EqualTo(3));
        });
    }

    [Test]
    public void SetupView_DoesNotRenderNumberedStepper()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string appXaml = File.ReadAllText(GetDesktopFilePath("App.axaml"));

        Assert.Multiple(() =>
        {
            Assert.That(mainWindowXaml, Does.Not.Contain("setupStepBadge"));
            Assert.That(mainWindowXaml, Does.Not.Contain("setupStepLabel"));
            Assert.That(appXaml, Does.Not.Contain("setupStepBadge"));
            Assert.That(appXaml, Does.Not.Contain("setupStepLabel"));
        });
    }

    [Test]
    public void FoldersHeader_HasSingleCompactAddFolderCommand()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string foldersHeader = GetSlice(
            mainWindowXaml,
            "<TextBlock Text=\"Folders\"",
            "<Grid Grid.Row=\"1\">");

        Assert.Multiple(() =>
        {
            Assert.That(foldersHeader, Does.Not.Contain("Sync roots"));
            Assert.That(foldersHeader, Does.Contain("ShowAddSyncPairCommand"));
            Assert.That(foldersHeader, Does.Contain("ToolTip.Tip=\"Add sync folder\""));
            Assert.That(foldersHeader, Does.Not.Contain("IsVisible=\"{Binding HasSyncPairs}\""));
            Assert.That(foldersHeader, Does.Contain("Classes=\"icon primary\""));
        });
    }

    [Test]
    public void EmptyFoldersState_DoesNotDuplicateAddFolderCommand()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string emptyFoldersState = GetSlice(
            mainWindowXaml,
            "IsVisible=\"{Binding HasNoSyncPairs}\"",
            "<Grid RowDefinitions=\"Auto,Auto\"");

        Assert.Multiple(() =>
        {
            Assert.That(CountOccurrences(emptyFoldersState, "ShowAddSyncPairCommand"), Is.Zero);
            Assert.That(CountOccurrences(emptyFoldersState, "Content=\"+\""), Is.Zero);
            Assert.That(emptyFoldersState, Does.Contain("Text=\"No folders yet\""));
            Assert.That(emptyFoldersState, Does.Not.Contain("<TextBlock Text=\"+\""));
        });
    }

    [Test]
    public void DashboardFolders_ExposeSelectedPairManagementActions()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string foldersSection = GetSlice(
            mainWindowXaml,
            "<TextBlock Text=\"Folders\"",
            "<TextBlock Text=\"Activity\"");

        Assert.Multiple(() =>
        {
            Assert.That(foldersSection, Does.Contain("ShowSelectedSyncPairEditorCommand"));
            Assert.That(foldersSection, Does.Contain("CommandParameter=\"{Binding}\""));
            Assert.That(foldersSection, Does.Contain("<ItemsControl ItemsSource=\"{Binding SyncPairs}\">"));
            Assert.That(foldersSection, Does.Not.Contain("SelectedItem=\"{Binding SelectedSyncPair}\""));
            Assert.That(foldersSection, Does.Contain("IsVisible=\"{Binding IsEditorVisible}\""));
            Assert.That(foldersSection, Does.Not.Contain("IsVisible=\"{Binding IsSelectedSyncPairEditorVisible}\""));
            Assert.That(foldersSection, Does.Contain("IsVisible=\"{Binding IsHeaderStatusVisible}\""));
            Assert.That(foldersSection, Does.Contain("Text=\"{Binding EditableDisplayName}\""));
            Assert.That(foldersSection, Does.Not.Contain("SelectedSyncPairEditableDisplayName"));
            Assert.That(foldersSection, Does.Contain("SaveSelectedSyncPairNameCommand"));
            Assert.That(foldersSection, Does.Contain("ToggleSelectedSyncPairEnabledCommand"));
            Assert.That(foldersSection, Does.Not.Contain("ChangeSelectedSyncPairLocalFolderCommand"));
            Assert.That(foldersSection, Does.Not.Contain("ChangeSelectedSyncPairRemoteFolderCommand"));
            Assert.That(foldersSection, Does.Contain("RemoveSelectedSyncPairCommand"));
            Assert.That(foldersSection, Does.Not.Contain("CancelSelectedSyncPairEditorCommand"));
            Assert.That(foldersSection, Does.Contain("IsRemoveSyncPairConfirmationVisible"));
            Assert.That(foldersSection, Does.Contain("CancelRemoveSyncPairCommand"));
            Assert.That(foldersSection, Does.Contain("ConfirmRemoveSelectedSyncPairCommand"));
            Assert.That(foldersSection, Does.Contain("ToolTip.Tip=\"Rename, enable, or remove\""));
            Assert.That(foldersSection, Does.Contain("ToolTip.Tip=\"Open local folder\""));
            Assert.That(foldersSection, Does.Not.Contain("ToolTip.Tip=\"Change local folder\""));
            Assert.That(foldersSection, Does.Not.Contain("ToolTip.Tip=\"Change cloud folder\""));
            Assert.That(CountOccurrences(foldersSection, "Classes=\"inlineChange\""), Is.Zero);
            Assert.That(CountOccurrences(foldersSection, "ToolTip.Tip=\"Open local folder\""), Is.EqualTo(1));
            Assert.That(foldersSection, Does.Not.Contain("ToolTip.Tip=\"Open selected local folder\""));
            Assert.That(foldersSection, Does.Not.Contain("ModeLabel"));
            Assert.That(foldersSection, Does.Not.Contain("SelectedSyncPair.ModeLabel"));
            Assert.That(foldersSection, Does.Contain("materialIcons:MaterialIcon"));
            Assert.That(foldersSection, Does.Contain("Kind=\"ContentSaveOutline\""));
            Assert.That(foldersSection, Does.Contain("Kind=\"FolderOffOutline\""));
            Assert.That(foldersSection, Does.Contain("Kind=\"FolderCheckOutline\""));
            Assert.That(foldersSection, Does.Not.Contain("Kind=\"PauseCircleOutline\""));
            Assert.That(foldersSection, Does.Not.Contain("Kind=\"PlayCircleOutline\""));
            Assert.That(foldersSection, Does.Contain("Kind=\"TrashCanOutline\""));
            Assert.That(foldersSection, Does.Not.Contain("<Path Data="));
            Assert.That(foldersSection, Does.Not.Contain("Content=\"{Binding ToggleEnabledIcon}\""));
            Assert.That(foldersSection, Does.Not.Contain("Content=\"💾\""));
            Assert.That(foldersSection, Does.Not.Contain("Content=\"🗑\""));
            Assert.That(foldersSection, Does.Not.Contain("Content=\"-\""));
            Assert.That(foldersSection, Does.Not.Contain("ToolTip.Tip=\"Close folder controls\""));
            Assert.That(foldersSection, Does.Contain("Text=\"{Binding CurrentOperation}\""));
            Assert.That(foldersSection, Does.Contain("IsVisible=\"{Binding HasCurrentOperation}\""));
            Assert.That(foldersSection, Does.Contain("Value=\"{Binding CurrentProgressValue}\""));
            Assert.That(foldersSection, Does.Contain("IsIndeterminate=\"{Binding IsCurrentProgressIndeterminate}\""));
            Assert.That(foldersSection, Does.Contain("IsVisible=\"{Binding HasCurrentProgress}\""));
        });
    }

    [Test]
    public void DashboardFolders_LeavesRoomForExpandedInlineControls()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string foldersSection = GetSlice(
            mainWindowXaml,
            "<TextBlock Text=\"Folders\"",
            "<TextBlock Text=\"Activity\"");

        Assert.Multiple(() =>
        {
            Assert.That(foldersSection, Does.Not.Contain("<ScrollViewer MaxHeight=\"216\""));
            Assert.That(foldersSection, Does.Not.Contain("<ScrollViewer MaxHeight=\"236\""));
            Assert.That(foldersSection, Does.Not.Contain("MaxHeight=\"300\""));
            Assert.That(foldersSection, Does.Contain("<ScrollViewer VerticalScrollBarVisibility=\"Auto\""));
            Assert.That(foldersSection, Does.Contain("<ItemsControl ItemsSource=\"{Binding SyncPairs}\">"));
        });
    }

    [Test]
    public void SetupErrorArea_ReservesSpaceWithoutReflowingTheForm()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string setupErrorArea = GetSlice(
            mainWindowXaml,
            "ToolTip.Tip=\"{Binding ActionRequiredMessage}\"",
            "<StackPanel Spacing=\"8\"");

        Assert.Multiple(() =>
        {
            Assert.That(setupErrorArea, Does.Contain("Opacity=\"{Binding ActionRequiredOpacity}\""));
            Assert.That(setupErrorArea, Does.Not.Contain("IsVisible=\"{Binding HasActionRequired}\""));
        });
    }

    [Test]
    public void SignInInputs_SubmitOnEnterAndReturnKeys()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string mainWindowCode = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml.cs"));
        string signInStep = GetSlice(
            mainWindowXaml,
            "IsVisible=\"{Binding IsSignInStepVisible}\"",
            "<Button Content=\"Sign in\"");

        Assert.Multiple(() =>
        {
            Assert.That(CountOccurrences(signInStep, "KeyDown=\"SignInInput_KeyDown\""), Is.EqualTo(3));
            Assert.That(mainWindowCode, Does.Contain("e.Key != Key.Enter && e.Key != Key.Return"));
            Assert.That(mainWindowCode, Does.Contain("viewModel.SignInCommand.Execute(null);"));
        });
    }

    [Test]
    public void SettingsDiagnostics_ScrollsWholeTabWithoutNestedSelfTestScrolling()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string diagnosticsSection = GetSlice(
            mainWindowXaml,
            "<TabItem Header=\"Diagnostics\"",
            "</TabItem>");
        int selfTestIndex = diagnosticsSection.IndexOf(
            "ItemsSource=\"{Binding SelfTestItems}\"",
            StringComparison.Ordinal);
        int diagnosticsIndex = diagnosticsSection.IndexOf(
            "ItemsSource=\"{Binding DiagnosticsItems}\"",
            StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(selfTestIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(diagnosticsIndex, Is.GreaterThan(selfTestIndex));
            Assert.That(diagnosticsSection, Does.Not.Contain("MaxHeight=\"118\""));
            Assert.That(diagnosticsSection, Does.Contain("<ScrollViewer Margin=\"0,10,0,0\""));
        });
    }

    [Test]
    public void SettingsOverlay_StretchesWithinDashboardWindow()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string settingsOverlay = GetSlice(
            mainWindowXaml,
            "IsVisible=\"{Binding IsSettingsVisible}\"",
            "</Window>");

        Assert.Multiple(() =>
        {
            Assert.That(settingsOverlay, Does.Not.Contain("MaxWidth=\"372\""));
            Assert.That(settingsOverlay, Does.Contain("HorizontalAlignment=\"Stretch\""));
            Assert.That(settingsOverlay, Does.Contain("VerticalAlignment=\"Stretch\""));
            Assert.That(settingsOverlay, Does.Contain("RowDefinitions=\"Auto,*\""));
            Assert.That(settingsOverlay, Does.Contain("<TabControl Grid.Row=\"1\""));
            Assert.That(settingsOverlay, Does.Contain("Classes=\"settingsTabs\""));
            Assert.That(settingsOverlay, Does.Contain("SelectedIndex=\"{Binding SelectedSettingsTabIndex}\""));
            Assert.That(settingsOverlay, Does.Not.Contain("<Border Width=\"372\""));
            Assert.That(settingsOverlay, Does.Not.Contain("MaxHeight=\"432\""));
            Assert.That(settingsOverlay, Does.Not.Contain("<ScrollViewer Grid.Row=\"1\""));
        });
    }

    [Test]
    public void DashboardContent_StretchesActivityIntoRemainingSpace()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string dashboardView = GetSlice(
            mainWindowXaml,
            "IsVisible=\"{Binding IsDashboardVisible}\">",
            "IsVisible=\"{Binding IsAddSyncPairWizardVisible}\">");

        Assert.Multiple(() =>
        {
            Assert.That(dashboardView, Does.Contain("RowDefinitions=\"Auto,*,*\""));
            Assert.That(dashboardView, Does.Contain("<StackPanel Grid.Row=\"0\""));
            Assert.That(dashboardView, Does.Contain("IsVisible=\"{Binding IsDashboardChromeVisible}\""));
            Assert.That(dashboardView, Does.Contain("<Border Grid.Row=\"1\""));
            Assert.That(dashboardView, Does.Contain("<Border Grid.Row=\"2\""));
            Assert.That(dashboardView, Does.Contain("VerticalScrollBarVisibility=\"Auto\""));
            Assert.That(dashboardView, Does.Not.Contain("<ScrollViewer Grid.Row=\"0\""));
            Assert.That(dashboardView, Does.Not.Contain("MaxHeight=\"332\""));
            Assert.That(dashboardView, Does.Not.Contain("MaxHeight=\"300\""));
            Assert.That(dashboardView, Does.Not.Contain("MaxHeight=\"320\""));
            Assert.That(dashboardView, Does.Not.Contain("<ScrollViewer MaxHeight=\"216\""));
            Assert.That(dashboardView, Does.Not.Contain("<ScrollViewer Margin=\"10\""));
            Assert.That(dashboardView, Does.Not.Contain("RowDefinitions=\"Auto,Auto,Auto,Auto,*\""));
            Assert.That(dashboardView, Does.Not.Contain("RowDefinitions=\"Auto,132\""));
        });
    }

    [Test]
    public void ActivityRows_UseShortEventKindAsTitle()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string activitySection = GetSlice(
            mainWindowXaml,
            "<TextBlock Text=\"Activity\"",
            "IsVisible=\"{Binding IsAddSyncPairWizardVisible}\">");

        Assert.Multiple(() =>
        {
            Assert.That(activitySection, Does.Contain("Text=\"{Binding Kind}\""));
            Assert.That(activitySection, Does.Contain("FontWeight=\"SemiBold\""));
            Assert.That(activitySection, Does.Contain("Text=\"{Binding Details}\""));
            Assert.That(activitySection, Does.Contain("ToolTip.Tip=\"{Binding Details}\""));
            Assert.That(activitySection, Does.Contain("TextWrapping=\"NoWrap\""));
            Assert.That(activitySection, Does.Contain("MaxLines=\"1\""));
            Assert.That(activitySection, Does.Contain("Text=\"{Binding Path}\""));
            Assert.That(activitySection, Does.Contain("ToolTip.Tip=\"{Binding Path}\""));
            Assert.That(activitySection, Does.Contain("IsVisible=\"{Binding HasPath}\""));
        });
    }

    [Test]
    public void CloseIconButtons_UseMaterialCloseIcon()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));

        Assert.Multiple(() =>
        {
            Assert.That(mainWindowXaml, Does.Not.Contain("Content=\"x\""));
            Assert.That(mainWindowXaml, Does.Not.Contain("Content=\"×\""));
            Assert.That(CountOccurrences(mainWindowXaml, "Kind=\"CloseCircleOutline\""), Is.EqualTo(3));
        });
    }

    [Test]
    public void MoreIconButtons_UseMaterialMenuIcon()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));

        Assert.Multiple(() =>
        {
            Assert.That(mainWindowXaml, Does.Not.Contain("Content=\"...\""));
            Assert.That(mainWindowXaml, Does.Not.Contain("Content=\"…\""));
            Assert.That(CountOccurrences(mainWindowXaml, "Kind=\"DotsVertical\""), Is.EqualTo(2));
        });
    }

    [Test]
    public void FoldersPanel_UsesSingleHeaderAddAction()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string foldersPanel = GetSlice(
            mainWindowXaml,
            "<TextBlock Text=\"Folders\"",
            "<TextBlock Text=\"Activity\"");

        Assert.Multiple(() =>
        {
            Assert.That(CountOccurrences(foldersPanel, "Command=\"{Binding ShowAddSyncPairCommand}\""), Is.EqualTo(1));
            Assert.That(foldersPanel, Does.Contain("ToolTip.Tip=\"Add sync folder\""));
            Assert.That(foldersPanel, Does.Contain("Text=\"No folders yet\""));
            Assert.That(foldersPanel, Does.Not.Contain("Text=\"Add a folder\""));
        });
    }

    [Test]
    public void SettingsOverlay_UsesTabbedSections()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string settingsOverlay = GetSlice(
            mainWindowXaml,
            "IsVisible=\"{Binding IsSettingsVisible}\"",
            "</Window>");

        Assert.Multiple(() =>
        {
            Assert.That(settingsOverlay, Does.Contain("Text=\"Account, startup, preferences, diagnostics\""));
            Assert.That(settingsOverlay, Does.Not.Contain("Text=\"Account, startup, and diagnostics\""));
            Assert.That(settingsOverlay, Does.Contain("<TabItem Header=\"Account\">"));
            Assert.That(settingsOverlay, Does.Contain("<TabItem Header=\"Startup\">"));
            Assert.That(settingsOverlay, Does.Contain("<TabItem Header=\"Preferences\">"));
            Assert.That(settingsOverlay, Does.Contain("<TabItem Header=\"Diagnostics\">"));
            Assert.That(settingsOverlay, Does.Not.Contain("Header=\"Start\""));
            Assert.That(settingsOverlay, Does.Not.Contain("Header=\"Prefs\""));
            Assert.That(settingsOverlay, Does.Not.Contain("Header=\"Diag\""));
        });
    }

    [Test]
    public void SettingsStart_HidesUnsupportedAutostartAction()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string startSection = GetSlice(
            mainWindowXaml,
            "<TabItem Header=\"Startup\"",
            "</TabItem>");

        Assert.Multiple(() =>
        {
            Assert.That(startSection, Does.Contain("Content=\"Launch on startup\""));
            Assert.That(startSection, Does.Contain("IsVisible=\"{Binding IsStartWithOperatingSystemSupported}\""));
            Assert.That(startSection, Does.Not.Contain("IsEnabled=\"{Binding IsStartWithOperatingSystemSupported}\""));
            Assert.That(startSection, Does.Contain("Text=\"{Binding AutostartStatusText}\""));
        });
    }

    [Test]
    public void SettingsAccountTab_IncludesAboutSectionWithoutAddingExtraTab()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string settingsOverlay = GetSlice(
            mainWindowXaml,
            "IsVisible=\"{Binding IsSettingsVisible}\"",
            "</Window>");
        string accountTab = GetSlice(
            settingsOverlay,
            "<TabItem Header=\"Account\">",
            "<TabItem Header=\"Startup\">");

        Assert.Multiple(() =>
        {
            Assert.That(accountTab, Does.Contain("Text=\"About\""));
            Assert.That(accountTab, Does.Contain("Text=\"{Binding AppVersion}\""));
            Assert.That(accountTab, Does.Contain("Text=\"Device name\""));
            Assert.That(accountTab, Does.Contain("Text=\"{Binding DeviceName}\""));
            Assert.That(accountTab, Does.Not.Contain("Text=\"Cotton Sync Desktop\""));
            Assert.That(CountOccurrences(settingsOverlay, "<TabItem Header="), Is.EqualTo(4));
        });
    }

    [Test]
    public void DashboardActionRows_UseIconButtonsForNarrowActions()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string dashboardHeader = GetSlice(
            mainWindowXaml,
            "Text=\"{Binding HeaderTitleText}\"",
            "<Grid Grid.Row=\"2\"");
        string actionRequiredRow = GetSlice(
            mainWindowXaml,
            "<TextBlock Text=\"Action required\"",
            "<Border MaxHeight=\"94\"");
        string conflictsHeader = GetSlice(
            mainWindowXaml,
            "<TextBlock Text=\"Conflicts\"",
            "<ScrollViewer Grid.Row=\"1\"");
        string conflictsSection = GetSlice(
            mainWindowXaml,
            "<TextBlock Text=\"Conflicts\"",
            "<TextBlock Text=\"Folders\"");

        Assert.Multiple(() =>
        {
            Assert.That(dashboardHeader, Does.Contain("Text=\"{Binding HeaderTitleText}\""));
            Assert.That(dashboardHeader, Does.Not.Contain("<TextBlock Text=\"Cotton Sync\""));
            Assert.That(dashboardHeader, Does.Contain("ToolTip.Tip=\"Sync now\""));
            Assert.That(dashboardHeader, Does.Contain("Kind=\"Refresh\""));
            Assert.That(dashboardHeader, Does.Contain("IsVisible=\"{Binding CanSyncNow}\""));
            Assert.That(dashboardHeader, Does.Not.Contain("Content=\"Sync\""));
            Assert.That(actionRequiredRow, Does.Contain("Kind=\"Refresh\""));
            Assert.That(actionRequiredRow, Does.Contain("Kind=\"CheckCircleOutline\""));
            Assert.That(actionRequiredRow, Does.Contain("MaxLines=\"3\""));
            Assert.That(actionRequiredRow, Does.Contain("TextWrapping=\"Wrap\""));
            Assert.That(actionRequiredRow, Does.Contain("ToolTip.Tip=\"{Binding ActionRequiredMessage}\""));
            Assert.That(actionRequiredRow, Does.Not.Contain("Content=\"Retry\""));
            Assert.That(actionRequiredRow, Does.Not.Contain("Content=\"Check\""));
            Assert.That(conflictsHeader, Does.Contain("Kind=\"Refresh\""));
            Assert.That(conflictsHeader, Does.Not.Contain("OpenConflictCommand"));
            Assert.That(conflictsHeader, Does.Not.Contain("Open selected conflict location"));
            Assert.That(conflictsSection, Does.Contain("OpenConflictCommand"));
            Assert.That(conflictsSection, Does.Contain("CommandParameter=\"{Binding}\""));
            Assert.That(conflictsSection, Does.Contain("ToolTip.Tip=\"Open conflict location\""));
            Assert.That(conflictsSection, Does.Contain("Kind=\"ArrowTopRight\""));
            Assert.That(conflictsHeader, Does.Not.Contain("Content=\"Retry\""));
            Assert.That(conflictsHeader, Does.Not.Contain("Content=\"Open\""));
        });
    }

    [Test]
    public void AddFolderWizard_WrapsActionRequiredMessage()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string wizardError = GetSlice(
            mainWindowXaml,
            "ToolTip.Tip=\"{Binding ActionRequiredMessage}\"",
            "<Grid Grid.Row=\"2\">");

        Assert.Multiple(() =>
        {
            Assert.That(wizardError, Does.Contain("Text=\"{Binding ActionRequiredMessage}\""));
            Assert.That(wizardError, Does.Contain("MaxLines=\"3\""));
            Assert.That(wizardError, Does.Contain("TextWrapping=\"Wrap\""));
            Assert.That(wizardError, Does.Contain("TextTrimming=\"CharacterEllipsis\""));
        });
    }

    [Test]
    public void StatusCard_UsesAttentionStateForActionRequired()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string statusCard = GetSlice(
            mainWindowXaml,
            "Classes=\"statusCard\"",
            "<TextBlock Text=\"Action required\"");

        Assert.Multiple(() =>
        {
            Assert.That(mainWindowXaml, Does.Contain("Text=\"{Binding HeaderTitleText}\""));
            Assert.That(mainWindowXaml, Does.Contain("Text=\"{Binding HeaderStatusText}\""));
            Assert.That(mainWindowXaml, Does.Not.Contain("Text=\"{Binding GlobalStatus}\""));
            Assert.That(statusCard, Does.Contain("IsVisible=\"{Binding IsStatusCardVisible}\""));
            Assert.That(statusCard, Does.Contain("Classes.actionRequired=\"{Binding HasStatusAttention}\""));
            Assert.That(statusCard, Does.Not.Contain("Classes.actionRequired=\"{Binding HasActionRequired}\""));
            Assert.That(statusCard, Does.Contain("Text=\"{Binding StatusCardDetailText}\""));
            Assert.That(statusCard, Does.Contain("IsVisible=\"{Binding HasStatusCardDetail}\""));
            Assert.That(statusCard, Does.Not.Contain("Text=\"{Binding AccountName}\""));
            Assert.That(statusCard, Does.Not.Contain("Text=\"{Binding CurrentProgressText}\""));
        });
    }

    [Test]
    public void DashboardProgressCards_ExposeRunAndTransferProgress()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string dashboardView = GetSlice(
            mainWindowXaml,
            "IsVisible=\"{Binding IsDashboardVisible}\">",
            "<TextBlock Text=\"Action required\"");

        Assert.Multiple(() =>
        {
            Assert.That(dashboardView, Does.Contain("IsVisible=\"{Binding HasCurrentWorkProgress}\""));
            Assert.That(dashboardView, Does.Contain("Text=\"{Binding CurrentWorkProgressTitle}\""));
            Assert.That(dashboardView, Does.Contain("Text=\"{Binding CurrentWorkProgressDetails}\""));
            Assert.That(dashboardView, Does.Contain("Text=\"{Binding CurrentWorkProgressSecondaryDetails}\""));
            Assert.That(dashboardView, Does.Contain("IsVisible=\"{Binding HasCurrentWorkProgressSecondaryDetails}\""));
            Assert.That(dashboardView, Does.Contain("Value=\"{Binding CurrentWorkProgressValue}\""));
            Assert.That(dashboardView, Does.Contain("IsIndeterminate=\"{Binding IsCurrentWorkProgressIndeterminate}\""));
            Assert.That(dashboardView, Does.Not.Contain("IsVisible=\"{Binding HasCurrentRunProgress}\""));
            Assert.That(dashboardView, Does.Not.Contain("IsVisible=\"{Binding HasCurrentTransfer}\""));
        });
    }

    [Test]
    public void DashboardNotifications_UseDashboardVisibilityGate()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string notificationsView = GetSlice(
            mainWindowXaml,
            "IsVisible=\"{Binding HasDashboardNotifications}\"",
            "<Border Padding=\"10\"\n                MaxHeight=\"116\"");

        Assert.Multiple(() =>
        {
            Assert.That(mainWindowXaml, Does.Contain("IsVisible=\"{Binding HasDashboardNotifications}\""));
            Assert.That(notificationsView, Does.Contain("IsVisible=\"{Binding IsDashboardVisible}\""));
            Assert.That(mainWindowXaml, Does.Not.Contain("IsVisible=\"{Binding HasNotifications}\""));
        });
    }

    [Test]
    public void SettingsDiagnostics_UsesClearDiagnosticsActions()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string diagnosticsSection = GetSlice(
            mainWindowXaml,
            "<TabItem Header=\"Diagnostics\"",
            "</TabItem>");

        Assert.Multiple(() =>
        {
            Assert.That(diagnosticsSection, Does.Contain("Content=\"Run checks\""));
            Assert.That(diagnosticsSection, Does.Contain("Content=\"Export logs\""));
            Assert.That(diagnosticsSection, Does.Contain("Content=\"Open data\""));
            Assert.That(diagnosticsSection, Does.Not.Contain("Content=\"Export diagnostics\""));
            Assert.That(diagnosticsSection, Does.Not.Contain("Content=\"Export bundle\""));
            Assert.That(diagnosticsSection, Does.Contain("ToolTip.Tip=\"Export logs and diagnostic state\""));
            Assert.That(diagnosticsSection, Does.Contain("ToolTip.Tip=\"Open app data folder\""));
            Assert.That(diagnosticsSection, Does.Contain("Text=\"Logs exported to\""));
            Assert.That(diagnosticsSection, Does.Contain("IsVisible=\"{Binding HasDataDirectory}\""));
            Assert.That(diagnosticsSection, Does.Contain("OpenDataFolderCommand"));
            Assert.That(diagnosticsSection, Does.Contain("LastDiagnosticsBundlePath"));
            Assert.That(diagnosticsSection, Does.Contain("OpenDiagnosticsBundleFolderCommand"));
        });
    }

    [Test]
    public void AddFolderWizard_StretchesWithinDashboardWindow()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string addFolderWizard = GetSlice(
            mainWindowXaml,
            "IsVisible=\"{Binding IsAddSyncPairWizardVisible}\"",
            "IsVisible=\"{Binding IsSettingsVisible}\"");

        Assert.Multiple(() =>
        {
            Assert.That(addFolderWizard, Does.Contain("MaxWidth=\"372\""));
            Assert.That(addFolderWizard, Does.Contain("HorizontalAlignment=\"Stretch\""));
            Assert.That(addFolderWizard, Does.Contain("ToolTip.Tip=\"{Binding ActionRequiredMessage}\""));
            Assert.That(addFolderWizard, Does.Not.Contain("<Border Width=\"372\""));
        });
    }

    [Test]
    public void CloudFolderPicker_UsesCompactIconNavigationButtons()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string cloudFolderPicker = GetSlice(
            mainWindowXaml,
            "IsVisible=\"{Binding IsAddSyncPairCloudStepVisible}\"",
            "IsVisible=\"{Binding HasNoRemoteFolders}\"");

        Assert.Multiple(() =>
        {
            Assert.That(cloudFolderPicker, Does.Contain("Kind=\"ArrowLeftCircleOutline\""));
            Assert.That(cloudFolderPicker, Does.Contain("ToolTip.Tip=\"Create cloud folder\""));
            Assert.That(cloudFolderPicker, Does.Contain("ShowCreateRemoteFolderCommand"));
            Assert.That(cloudFolderPicker, Does.Contain("Kind=\"FolderPlusOutline\""));
            Assert.That(cloudFolderPicker, Does.Contain("CreateRemoteFolderCommand"));
            Assert.That(cloudFolderPicker, Does.Contain("Kind=\"ArrowRightCircleOutline\""));
            Assert.That(cloudFolderPicker, Does.Contain("Kind=\"CheckCircleOutline\""));
            Assert.That(cloudFolderPicker, Does.Contain("Kind=\"CloseCircleOutline\""));
            Assert.That(CountOccurrences(cloudFolderPicker, "Classes=\"icon\""), Is.EqualTo(4));
            Assert.That(cloudFolderPicker, Does.Not.Contain("Content=\"←\""));
            Assert.That(cloudFolderPicker, Does.Not.Contain("Content=\"→\""));
            Assert.That(cloudFolderPicker, Does.Not.Contain("Content=\"^\""));
            Assert.That(cloudFolderPicker, Does.Not.Contain("Content=\">\""));
            Assert.That(cloudFolderPicker, Does.Not.Contain("Content=\"Up\""));
            Assert.That(cloudFolderPicker, Does.Not.Contain("Content=\"Open\""));
        });
    }

    [Test]
    public void AddFolderWizard_UsesFolderSelectionPrimaryAction()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string addFolderWizard = GetSlice(
            mainWindowXaml,
            "IsVisible=\"{Binding IsAddSyncPairWizardVisible}\"",
            "IsVisible=\"{Binding IsSettingsVisible}\"");

        Assert.Multiple(() =>
        {
            Assert.That(addFolderWizard, Does.Contain("Content=\"{Binding RemoteFolderWizardPrimaryActionText}\""));
            Assert.That(addFolderWizard, Does.Contain("ToolTip.Tip=\"{Binding RemoteFolderWizardPrimaryActionToolTip}\""));
            Assert.That(addFolderWizard, Does.Contain("UseRemoteFolderCommand"));
            Assert.That(addFolderWizard, Does.Contain("IsAddSyncPairLocalSummaryVisible"));
            Assert.That(addFolderWizard, Does.Not.Contain("Command=\"{Binding AddSyncPairCommand}\""));
            Assert.That(addFolderWizard, Does.Not.Contain("Content=\"Sync\""));
        });
    }

    private static string GetDesktopFilePath(string fileName)
    {
        string directory = TestContext.CurrentContext.TestDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            string candidate = Path.Combine(directory, "src", "Cotton.Sync.Desktop", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            string? parent = Directory.GetParent(directory)?.FullName;
            if (parent == directory)
            {
                break;
            }

            directory = parent ?? string.Empty;
        }

        throw new FileNotFoundException(fileName + " was not found from the test directory.");
    }

    private static string GetSlice(string text, string startMarker, string endMarker)
    {
        int start = text.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException(startMarker + " was not found.");
        }

        int end = text.IndexOf(endMarker, start, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidOperationException(endMarker + " was not found.");
        }

        return text[start..end];
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int currentIndex = 0;
        while (currentIndex < text.Length)
        {
            int nextIndex = text.IndexOf(value, currentIndex, StringComparison.Ordinal);
            if (nextIndex < 0)
            {
                return count;
            }

            count++;
            currentIndex = nextIndex + value.Length;
        }

        return count;
    }
}
