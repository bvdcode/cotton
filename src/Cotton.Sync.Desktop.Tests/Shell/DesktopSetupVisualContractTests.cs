// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Tests.Shell;

public sealed class DesktopSetupVisualContractTests
{
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
    public void FoldersHeader_DoesNotDuplicateAddFolderCommand()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string foldersHeader = GetSlice(
            mainWindowXaml,
            "<TextBlock Text=\"Folders\"",
            "<Grid Grid.Row=\"1\">");

        Assert.Multiple(() =>
        {
            Assert.That(foldersHeader, Does.Not.Contain("ShowAddSyncPairCommand"));
            Assert.That(mainWindowXaml, Does.Contain("ToolTip.Tip=\"Add another sync folder\""));
        });
    }

    [Test]
    public void EmptyFoldersState_HasSingleAddFolderCommand()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string emptyFoldersState = GetSlice(
            mainWindowXaml,
            "IsVisible=\"{Binding HasNoSyncPairs}\"",
            "<Grid RowDefinitions=\"*,Auto,Auto\"");

        Assert.Multiple(() =>
        {
            Assert.That(CountOccurrences(emptyFoldersState, "ShowAddSyncPairCommand"), Is.EqualTo(1));
            Assert.That(CountOccurrences(emptyFoldersState, "Content=\"+\""), Is.EqualTo(1));
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
            Assert.That(foldersSection, Does.Contain("SelectedSyncPairEditableDisplayName"));
            Assert.That(foldersSection, Does.Contain("SaveSelectedSyncPairNameCommand"));
            Assert.That(foldersSection, Does.Contain("ToggleSelectedSyncPairEnabledCommand"));
            Assert.That(foldersSection, Does.Contain("RemoveSelectedSyncPairCommand"));
            Assert.That(foldersSection, Does.Contain("ToolTip.Tip=\"Open selected local folder\""));
            Assert.That(foldersSection, Does.Contain("SelectedSyncPair.ToggleEnabledIcon"));
            Assert.That(foldersSection, Does.Contain("SelectedSyncPair.ModeLabel"));
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
    public void SettingsDiagnostics_DoesNotNestSelfTestScrolling()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string diagnosticsSection = GetSlice(
            mainWindowXaml,
            "<TabItem Header=\"Diagnostics\">",
            "</TabItem>");

        Assert.Multiple(() =>
        {
            Assert.That(diagnosticsSection, Does.Contain("ItemsSource=\"{Binding SelfTestItems}\""));
            Assert.That(diagnosticsSection, Does.Not.Contain("MaxHeight=\"118\""));
            Assert.That(diagnosticsSection, Does.Not.Contain("<ScrollViewer"));
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
            Assert.That(settingsOverlay, Does.Contain("MaxWidth=\"372\""));
            Assert.That(settingsOverlay, Does.Contain("HorizontalAlignment=\"Stretch\""));
            Assert.That(settingsOverlay, Does.Contain("VerticalAlignment=\"Stretch\""));
            Assert.That(settingsOverlay, Does.Contain("RowDefinitions=\"Auto,*\""));
            Assert.That(settingsOverlay, Does.Contain("<TabControl Grid.Row=\"1\">"));
            Assert.That(settingsOverlay, Does.Not.Contain("<Border Width=\"372\""));
            Assert.That(settingsOverlay, Does.Not.Contain("MaxHeight=\"432\""));
            Assert.That(settingsOverlay, Does.Not.Contain("<ScrollViewer Grid.Row=\"1\""));
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
            Assert.That(settingsOverlay, Does.Contain("<TabItem Header=\"Account\">"));
            Assert.That(settingsOverlay, Does.Contain("<TabItem Header=\"Startup\">"));
            Assert.That(settingsOverlay, Does.Contain("<TabItem Header=\"Prefs\">"));
            Assert.That(settingsOverlay, Does.Contain("<TabItem Header=\"Diagnostics\">"));
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
            Assert.That(CountOccurrences(settingsOverlay, "<TabItem Header="), Is.EqualTo(4));
        });
    }

    [Test]
    public void DashboardActionRows_UseIconButtonsForNarrowActions()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string dashboardHeader = GetSlice(
            mainWindowXaml,
            "<TextBlock Text=\"Cotton Sync\"",
            "<Grid Grid.Row=\"2\"");
        string actionRequiredRow = GetSlice(
            mainWindowXaml,
            "<TextBlock Text=\"Action required\"",
            "<Border Grid.Row=\"2\"");
        string conflictsHeader = GetSlice(
            mainWindowXaml,
            "<TextBlock Text=\"Conflicts\"",
            "<ScrollViewer Grid.Row=\"1\"");

        Assert.Multiple(() =>
        {
            Assert.That(dashboardHeader, Does.Contain("ToolTip.Tip=\"Sync now\""));
            Assert.That(dashboardHeader, Does.Contain("Content=\"↻\""));
            Assert.That(dashboardHeader, Does.Not.Contain("Content=\"Sync\""));
            Assert.That(actionRequiredRow, Does.Contain("Content=\"↻\""));
            Assert.That(actionRequiredRow, Does.Contain("Content=\"✓\""));
            Assert.That(actionRequiredRow, Does.Not.Contain("Content=\"Retry\""));
            Assert.That(actionRequiredRow, Does.Not.Contain("Content=\"Check\""));
            Assert.That(conflictsHeader, Does.Contain("Content=\"↻\""));
            Assert.That(conflictsHeader, Does.Contain("Content=\"↗\""));
            Assert.That(conflictsHeader, Does.Not.Contain("Content=\"Retry\""));
            Assert.That(conflictsHeader, Does.Not.Contain("Content=\"Open\""));
        });
    }

    [Test]
    public void SettingsDiagnostics_UsesCompactExportAction()
    {
        string mainWindowXaml = File.ReadAllText(GetDesktopFilePath("MainWindow.axaml"));
        string diagnosticsSection = GetSlice(
            mainWindowXaml,
            "<TabItem Header=\"Diagnostics\">",
            "</TabItem>");

        Assert.Multiple(() =>
        {
            Assert.That(diagnosticsSection, Does.Contain("Content=\"Export\""));
            Assert.That(diagnosticsSection, Does.Not.Contain("Content=\"Export diagnostics\""));
            Assert.That(diagnosticsSection, Does.Contain("ToolTip.Tip=\"Export diagnostics bundle\""));
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
            Assert.That(cloudFolderPicker, Does.Contain("Content=\"←\""));
            Assert.That(cloudFolderPicker, Does.Contain("Content=\"→\""));
            Assert.That(CountOccurrences(cloudFolderPicker, "Classes=\"icon\""), Is.EqualTo(2));
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
            Assert.That(addFolderWizard, Does.Contain("Content=\"Use this folder\""));
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
