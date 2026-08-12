// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Dto;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Reads CPU feature diagnostics visible to the server process.
    /// </summary>
    public static class CpuFeatureDiagnostics
    {
        private const string ProcCpuInfoPath = "/proc/cpuinfo";

        /// <summary>
        /// Gets a CPU feature snapshot.
        /// </summary>
        public static CpuFeatureDiagnosticsDto Snapshot()
        {
            LinuxCpuInfo cpuInfo = ReadLinuxCpuInfo();
            CpuFeatureAvailabilityDto aesNi = CreateRuntimeAndLinuxFeature(Aes.IsSupported, cpuInfo, "aes");
            CpuFeatureAvailabilityDto pclmulqdq = CreateRuntimeAndLinuxFeature(
                Pclmulqdq.IsSupported,
                cpuInfo,
                "pclmulqdq");

            return new CpuFeatureDiagnosticsDto
            {
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                LogicalProcessorCount = Environment.ProcessorCount,
                VendorId = cpuInfo.VendorId,
                ModelName = cpuInfo.ModelName,
                AesGcmHardwareAccelerationLikely = aesNi.RuntimeSupported == true && pclmulqdq.RuntimeSupported == true,
                AesNi = aesNi,
                Pclmulqdq = pclmulqdq,
                Vaes = CreateLinuxOnlyFeature(cpuInfo, "vaes"),
                Vpclmulqdq = CreateLinuxOnlyFeature(cpuInfo, "vpclmulqdq"),
                Avx2 = CreateRuntimeAndLinuxFeature(Avx2.IsSupported, cpuInfo, "avx2"),
                Tme = CreateLinuxOnlyFeature(cpuInfo, "tme"),
                TmeMk = CreateLinuxOnlyFeature(cpuInfo, "mktme", "tme-mk"),
                Pconfig = CreateLinuxOnlyFeature(cpuInfo, "pconfig"),
                LinuxCpuFlags = cpuInfo.Flags,
            };
        }

        private static CpuFeatureAvailabilityDto CreateRuntimeAndLinuxFeature(
            bool runtimeSupported,
            LinuxCpuInfo cpuInfo,
            params string[] linuxFlags)
        {
            return new CpuFeatureAvailabilityDto
            {
                RuntimeSupported = runtimeSupported,
                LinuxFlagPresent = cpuInfo.TryHasAnyFlag(linuxFlags),
            };
        }

        private static CpuFeatureAvailabilityDto CreateLinuxOnlyFeature(
            LinuxCpuInfo cpuInfo,
            params string[] linuxFlags)
        {
            return new CpuFeatureAvailabilityDto
            {
                RuntimeSupported = null,
                LinuxFlagPresent = cpuInfo.TryHasAnyFlag(linuxFlags),
            };
        }

        private static LinuxCpuInfo ReadLinuxCpuInfo()
        {
            if (!OperatingSystem.IsLinux() || !File.Exists(ProcCpuInfoPath))
            {
                return LinuxCpuInfo.Empty;
            }

            string? vendorId = null;
            string? modelName = null;
            var flags = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string line in File.ReadLines(ProcCpuInfoPath))
            {
                string[] parts = line.Split(':', 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                string key = parts[0].Trim();
                string value = parts[1].Trim();
                vendorId ??= TryReadField(key, value, "vendor_id", "CPU implementer");
                modelName ??= TryReadField(key, value, "model name", "Hardware", "Processor");

                if (key.Equals("flags", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("Features", StringComparison.OrdinalIgnoreCase))
                {
                    AddFlags(flags, value);
                }
            }

            return new LinuxCpuInfo(
                vendorId,
                modelName,
                [.. flags.Select(flag => flag.ToLowerInvariant())],
                flags.Count > 0);
        }

        private static string? TryReadField(string key, string value, params string[] acceptedKeys)
        {
            return acceptedKeys.Any(acceptedKey => key.Equals(acceptedKey, StringComparison.OrdinalIgnoreCase))
                ? value
                : null;
        }

        private static void AddFlags(ISet<string> flags, string rawFlags)
        {
            foreach (string flag in rawFlags.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                flags.Add(flag);
            }
        }

        private record LinuxCpuInfo(
            string? VendorId,
            string? ModelName,
            IReadOnlyList<string> Flags,
            bool HasFlags)
        {
            public static LinuxCpuInfo Empty { get; } = new(null, null, [], false);

            public bool? TryHasAnyFlag(params string[] flags)
            {
                if (!HasFlags)
                {
                    return null;
                }

                return flags.Any(flag => Flags.Contains(flag, StringComparer.OrdinalIgnoreCase));
            }
        }
    }
}
