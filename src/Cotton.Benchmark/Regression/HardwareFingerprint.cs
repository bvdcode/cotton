// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Runtime.InteropServices;
using System.Text;

namespace Cotton.Benchmark.Regression
{
    internal sealed class HardwareFingerprint
    {
        public string Key { get; init; } = string.Empty;

        public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
    }

    internal sealed class HardwareFingerprintProvider
    {
        public HardwareFingerprint Create()
        {
            var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
                ["cpu"] = GetCpuModel(),
                ["dotnet"] = Environment.Version.ToString(),
                ["logicalProcessors"] = Environment.ProcessorCount.ToString(),
                ["os"] = RuntimeInformation.OSDescription.Trim(),
                ["runtime"] = RuntimeInformation.FrameworkDescription.Trim()
            };

            string key = string.Join(
                '-',
                Sanitize(GetOsFamily()),
                Sanitize(properties["architecture"]),
                Sanitize(properties["cpu"]),
                $"dotnet{Environment.Version.Major}");

            return new HardwareFingerprint
            {
                Key = key,
                Properties = properties
            };
        }

        private static string GetCpuModel()
        {
            string? linuxCpu = TryReadLinuxCpuModel();
            if (!string.IsNullOrWhiteSpace(linuxCpu))
            {
                return linuxCpu;
            }

            string? processorIdentifier = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
            if (!string.IsNullOrWhiteSpace(processorIdentifier))
            {
                return processorIdentifier;
            }

            return "unknown-cpu";
        }

        private static string? TryReadLinuxCpuModel()
        {
            const string cpuInfoPath = "/proc/cpuinfo";
            if (!File.Exists(cpuInfoPath))
            {
                return null;
            }

            foreach (string line in File.ReadLines(cpuInfoPath))
            {
                int separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
                if (separatorIndex < 0)
                {
                    continue;
                }

                string key = line[..separatorIndex].Trim();
                if (!key.Equals("model name", StringComparison.OrdinalIgnoreCase)
                    && !key.Equals("Hardware", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return line[(separatorIndex + 1)..].Trim();
            }

            return null;
        }

        private static string GetOsFamily()
        {
            if (OperatingSystem.IsLinux())
            {
                return "linux";
            }

            if (OperatingSystem.IsWindows())
            {
                return "windows";
            }

            if (OperatingSystem.IsMacOS())
            {
                return "macos";
            }

            return "unknown-os";
        }

        private static string Sanitize(string value)
        {
            var builder = new StringBuilder(value.Length);
            bool previousWasSeparator = false;

            foreach (char c in value.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                    previousWasSeparator = false;
                }
                else if (!previousWasSeparator)
                {
                    builder.Append('-');
                    previousWasSeparator = true;
                }
            }

            return builder.ToString().Trim('-');
        }
    }
}
