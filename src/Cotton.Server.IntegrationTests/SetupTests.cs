// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Server.IntegrationTests.Helpers;
using NUnit.Framework;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    [Order(1)]
    internal class SetupTests
    {
        private readonly StringBuilder report = new();

        [SetUp]
        public void SetUp()
        {
            report.AppendLine("=== System Information ===");

            // get cpu info
            report.AppendLine("CPU Model: " + SystemHelpers.GetCpuModel());
            report.AppendLine($"CPU Processor Count: {Environment.ProcessorCount}");
            report.AppendLine("Available Memory: " + (GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024)) + " MB");
            report.AppendLine("Processor Architecture: " + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);

            // Get OS info
            report.AppendLine($"OS Version: {Environment.OSVersion}");
            report.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
            report.AppendLine($"64-bit Process: {Environment.Is64BitProcess}");
            report.AppendLine("Machine Name: " + Environment.MachineName);
            report.AppendLine("User Name: " + Environment.UserName);
            report.AppendLine("Current Directory: " + Environment.CurrentDirectory);
            report.AppendLine("Tick Count: " + Environment.TickCount);
            report.AppendLine("CLR Version: " + Environment.Version);
            report.AppendLine("OS Description: " + System.Runtime.InteropServices.RuntimeInformation.OSDescription);
            report.AppendLine("OS Architecture: " + System.Runtime.InteropServices.RuntimeInformation.OSArchitecture);
            report.AppendLine("Environment: " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        }

        [Test]
        [Order(100)]
        public void FinalReport()
        {
            Assert.Pass(report.ToString());
        }
    }
}
