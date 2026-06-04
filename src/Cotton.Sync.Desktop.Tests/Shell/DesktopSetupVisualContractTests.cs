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
            "<Grid RowDefinitions=\"*,Auto\"");

        Assert.Multiple(() =>
        {
            Assert.That(CountOccurrences(emptyFoldersState, "ShowAddSyncPairCommand"), Is.EqualTo(1));
            Assert.That(CountOccurrences(emptyFoldersState, "Content=\"+\""), Is.EqualTo(1));
            Assert.That(emptyFoldersState, Does.Not.Contain("<TextBlock Text=\"+\""));
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
            "<TextBlock Text=\"Diagnostics\"",
            "<TextBlock Text=\"Cotton Sync Desktop\"");

        Assert.Multiple(() =>
        {
            Assert.That(diagnosticsSection, Does.Contain("ItemsSource=\"{Binding SelfTestItems}\""));
            Assert.That(diagnosticsSection, Does.Not.Contain("MaxHeight=\"118\""));
            Assert.That(diagnosticsSection, Does.Not.Contain("<ScrollViewer"));
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
