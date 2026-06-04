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
}
