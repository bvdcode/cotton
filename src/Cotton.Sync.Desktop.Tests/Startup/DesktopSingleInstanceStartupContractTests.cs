// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Tests.Startup
{

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
        public void Program_HoldsInstallerRuntimeMutexForWindowsSetupDetection()
        {
            string program = File.ReadAllText(GetDesktopFilePath("Program.cs"));
            string installerMutex = File.ReadAllText(GetDesktopFilePath(Path.Combine("Platform", "DesktopInstallerRuntimeMutex.cs")));

            Assert.Multiple(() =>
            {
                Assert.That(program, Does.Contain("DesktopInstallerRuntimeMutex.CreateForCurrentPlatform()"));
                Assert.That(installerMutex, Does.Contain("MutexName"));
                Assert.That(installerMutex, Does.Contain("CottonSyncDesktop_B671C18E_1E77_437C_AB9B_5C5C9D877E18"));
                Assert.That(installerMutex, Does.Contain("OperatingSystem.IsWindows()"));
            });
        }

        [Test]
        public void Program_InstallsTraceAndCrashLoggingBeforeCommandLineModes()
        {
            string program = File.ReadAllText(GetDesktopFilePath("Program.cs"));
            int traceLoggingIndex = program.IndexOf("DesktopTraceLogging.Install(paths)", StringComparison.Ordinal);
            int crashReporterIndex = program.IndexOf("DesktopUnhandledExceptionReporter.Install()", StringComparison.Ordinal);
            int selfTestIndex = program.IndexOf("startupOptions.RunSelfTest", StringComparison.Ordinal);
            int exportIndex = program.IndexOf("startupOptions.ExportDiagnostics", StringComparison.Ordinal);

            Assert.Multiple(() =>
            {
                Assert.That(traceLoggingIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(crashReporterIndex, Is.GreaterThan(traceLoggingIndex));
                Assert.That(selfTestIndex, Is.GreaterThan(crashReporterIndex));
                Assert.That(exportIndex, Is.GreaterThan(crashReporterIndex));
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

        private static string GetDesktopFilePath(string relativePath)
        {
            string directory = TestContext.CurrentContext.TestDirectory;
            while (!string.IsNullOrWhiteSpace(directory))
            {
                string candidate = Path.Combine(directory, "src", "Cotton.Sync.Desktop", relativePath);
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

            throw new FileNotFoundException(relativePath + " was not found from the test directory.");
        }
    }
}
