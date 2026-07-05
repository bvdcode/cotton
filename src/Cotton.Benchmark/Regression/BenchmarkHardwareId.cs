// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text;

namespace Cotton.Benchmark.Regression
{
    internal static class BenchmarkHardwareId
    {
        public static string Create(BenchmarkRunDocument runDocument)
        {
            if (runDocument.Environment.TryGetValue("cpu", out string? cpu))
            {
                string? knownId = TryCreateKnownCpuId(cpu);
                if (knownId is not null)
                {
                    return knownId;
                }
            }

            return ShortenHardwareKey(runDocument.HardwareKey);
        }

        private static string? TryCreateKnownCpuId(string cpu)
        {
            if (cpu.Contains("N100", StringComparison.OrdinalIgnoreCase))
            {
                return "intel-n100";
            }

            if (cpu.Contains("J3355", StringComparison.OrdinalIgnoreCase))
            {
                return "celeron-j3355";
            }

            if (cpu.Contains("E-2236", StringComparison.OrdinalIgnoreCase))
            {
                return "xeon-e-2236";
            }

            if (cpu.Contains("i5-12450H", StringComparison.OrdinalIgnoreCase))
            {
                return "core-i5-12450h";
            }

            if (cpu.Contains("i7-14700F", StringComparison.OrdinalIgnoreCase))
            {
                return "core-i7-14700f";
            }

            if (cpu.Contains("i9-13900K", StringComparison.OrdinalIgnoreCase))
            {
                return "core-i9-13900k";
            }

            return null;
        }

        private static string ShortenHardwareKey(string hardwareKey)
        {
            const string LinuxPrefix = "linux-x64-";
            const string WindowsPrefix = "windows-x64-";
            const string DotNetSuffix = "-dotnet10";

            string shortened = hardwareKey;
            if (shortened.StartsWith(LinuxPrefix, StringComparison.Ordinal))
            {
                shortened = shortened[LinuxPrefix.Length..];
            }
            else if (shortened.StartsWith(WindowsPrefix, StringComparison.Ordinal))
            {
                shortened = shortened[WindowsPrefix.Length..];
            }

            if (shortened.EndsWith(DotNetSuffix, StringComparison.Ordinal))
            {
                shortened = shortened[..^DotNetSuffix.Length];
            }

            return Sanitize(shortened);
        }

        private static string Sanitize(string value)
        {
            var builder = new StringBuilder(value.Length);
            bool previousWasSeparator = false;

            foreach (char character in value.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
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
