// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov | bvdcode | belov.us

using System.Management;

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal class SystemHelpers
    {
        internal static string GetCpuModel()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture + " - " +
                           new System.Management.ManagementObjectSearcher("select Name from Win32_Processor")
                           .Get().Cast<ManagementObject>().First()["Name"].ToString();
                }

                if (OperatingSystem.IsLinux())
                {
                    return System.IO.File.ReadAllLines("/proc/cpuinfo")
                        .FirstOrDefault(l => l.StartsWith("model name"))?
                        .Split(':')[1].Trim() ?? "Unknown CPU";
                }

                if (OperatingSystem.IsMacOS())
                {
                    return System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "sysctl",
                        Arguments = "-n machdep.cpu.brand_string",
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    })!.StandardOutput.ReadToEnd().Trim();
                }

                return "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
