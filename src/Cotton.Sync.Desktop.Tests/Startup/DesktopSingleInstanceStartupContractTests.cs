// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Tests.Startup;

public sealed class DesktopSingleInstanceStartupContractTests
{
    [Test]
    public void Program_RequestsExistingInstanceActivationWhenLockIsHeld()
    {
        string program = File.ReadAllText(GetDesktopFilePath("Program.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(program, Does.Contain("singleInstance is null"));
            Assert.That(program, Does.Contain("DesktopSingleInstanceActivation"));
            Assert.That(program, Does.Contain("TryRequestShowAsync(paths.SingleInstanceLockPath)"));
        });
    }

    [Test]
    public void App_StartsActivationServerForRunningInstance()
    {
        string app = File.ReadAllText(GetDesktopFilePath("App.axaml.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(app, Does.Contain("DesktopSingleInstanceActivationServer"));
            Assert.That(app, Does.Contain("DesktopSingleInstanceActivation.StartServer"));
            Assert.That(app, Does.Contain("window.ShowShell"));
            Assert.That(app, Does.Contain("_singleInstanceActivationServer?.Dispose()"));
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
