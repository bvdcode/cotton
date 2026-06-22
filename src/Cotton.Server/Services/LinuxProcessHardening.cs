// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Runtime.InteropServices;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Reports process hardening status.
    /// </summary>
    public record ProcessHardeningStatus(
        bool Requested,
        bool Applied,
        string? Error,
        int? DumpableAfter);

    /// <summary>
    /// Represents linux process hardening.
    /// </summary>
    public static class LinuxProcessHardening
    {
        /// <summary>
        /// Defines the environment variable.
        /// </summary>
        public const string EnvironmentVariable = "COTTON_PROCESS_HARDENING";

        private const int PR_GET_DUMPABLE = 3;
        private const int PR_SET_DUMPABLE = 4;

        [DllImport("libc", SetLastError = true)]
        private static extern int prctl(int option, ulong arg2, ulong arg3, ulong arg4, ulong arg5);

        [DllImport("libc")]
        private static extern uint geteuid();

        /// <summary>
        /// Applies from environment.
        /// </summary>
        public static ProcessHardeningStatus ApplyFromEnvironment()
        {
            bool requested = IsEnabled(Environment.GetEnvironmentVariable(EnvironmentVariable));
            if (!requested)
            {
                return new ProcessHardeningStatus(false, false, null, TryGetDumpable());
            }

            if (!OperatingSystem.IsLinux())
            {
                return new ProcessHardeningStatus(true, false, "Process dump hardening is only supported on Linux.", null);
            }

            int result = prctl(PR_SET_DUMPABLE, 0, 0, 0, 0);
            if (result != 0)
            {
                int errno = Marshal.GetLastPInvokeError();
                return new ProcessHardeningStatus(
                    true,
                    false,
                    $"prctl(PR_SET_DUMPABLE, 0) failed with errno {errno}.",
                    TryGetDumpable());
            }

            return new ProcessHardeningStatus(true, true, null, TryGetDumpable());
        }

        /// <summary>
        /// Attempts to get dumpable.
        /// </summary>
        public static int? TryGetDumpable()
        {
            if (!OperatingSystem.IsLinux())
            {
                return null;
            }

            int result = prctl(PR_GET_DUMPABLE, 0, 0, 0, 0);
            return result >= 0 ? result : null;
        }

        /// <summary>
        /// Attempts to get effective user id.
        /// </summary>
        public static uint? TryGetEffectiveUserId()
        {
            if (!OperatingSystem.IsLinux())
            {
                return null;
            }

            return geteuid();
        }

        /// <summary>
        /// Reads Linux process status from /proc.
        /// </summary>
        public static LinuxProcStatus SnapshotProcStatus()
        {
            if (!OperatingSystem.IsLinux())
            {
                return LinuxProcStatus.Empty;
            }

            const string procStatusPath = "/proc/self/status";
            if (!File.Exists(procStatusPath))
            {
                return LinuxProcStatus.Empty;
            }

            Dictionary<string, string> values = File
                .ReadLines(procStatusPath)
                .Select(line => line.Split(':', 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1].Trim(), StringComparer.Ordinal);

            int? noNewPrivs = TryReadInt(values, "NoNewPrivs");
            int? seccomp = TryReadInt(values, "Seccomp");
            int? seccompFilters = TryReadInt(values, "Seccomp_filters");
            string? capEff = values.TryGetValue("CapEff", out string? rawCapEff) ? rawCapEff : null;

            return new LinuxProcStatus(
                noNewPrivs,
                seccomp,
                seccompFilters,
                capEff,
                HasCapability(capEff, 19));
        }

        private static bool IsEnabled(string? value)
        {
            return value is not null
                && (value.Equals("1", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("on", StringComparison.OrdinalIgnoreCase));
        }

        private static int? TryReadInt(IReadOnlyDictionary<string, string> values, string key)
        {
            return values.TryGetValue(key, out string? value)
                && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                    ? parsed
                    : null;
        }

        private static bool? HasCapability(string? capEffHex, int capability)
        {
            if (string.IsNullOrWhiteSpace(capEffHex)
                || !ulong.TryParse(capEffHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong capEff))
            {
                return null;
            }

            return (capEff & (1UL << capability)) != 0;
        }
    }

    /// <summary>
    /// Reports linux proc status.
    /// </summary>
    public record LinuxProcStatus(
        int? NoNewPrivileges,
        int? SeccompMode,
        int? SeccompFilters,
        string? EffectiveCapabilitiesHex,
        bool? HasSysPtraceCapability)
    {
        /// <summary>
        /// Creates an empty value object.
        /// </summary>
        public static LinuxProcStatus Empty { get; } = new(null, null, null, null, null);
    }
}
