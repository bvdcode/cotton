// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Text.RegularExpressions;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Captures Linux container boundary signals that affect Cotton secret exposure.
    /// </summary>
    public record LinuxContainerSecuritySnapshot(
        bool? RootFilesystemReadOnly,
        bool DockerSocketMounted,
        bool? HostPidNamespaceLikely,
        string? ProcOneCommandLine,
        string? CoreDumpSoftLimit,
        string? CoreDumpHardLimit,
        bool? CoreDumpSoftLimitDisabled,
        string? CorePattern,
        string? AppArmorProfile,
        string? SelinuxContext,
        bool? SelinuxEnforcing);

    /// <summary>
    /// Reads low-level Linux container hardening facts from procfs and sysfs.
    /// </summary>
    public static class LinuxContainerSecurity
    {
        private static readonly string[] DockerSocketPaths =
        [
            "/var/run/docker.sock",
            "/run/docker.sock",
        ];

        private static readonly Regex MultiWhitespace = new(@"\s{2,}", RegexOptions.Compiled);

        /// <summary>
        /// Builds a snapshot from the current Linux runtime.
        /// </summary>
        public static LinuxContainerSecuritySnapshot Snapshot(bool isContainer)
        {
            if (!OperatingSystem.IsLinux())
            {
                return new LinuxContainerSecuritySnapshot(
                    null,
                    false,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }

            IReadOnlyList<LinuxMountInfoEntry> mounts = ReadMountInfo();
            CoreLimitSnapshot coreLimit = ReadCoreLimit();
            string? mandatoryAccessControlProfile = TryReadTrimmed("/proc/self/attr/current");

            return new LinuxContainerSecuritySnapshot(
                TryGetRootFilesystemReadOnly(mounts),
                IsDockerSocketMounted(mounts),
                TryDetectHostPidNamespace(isContainer),
                TryReadCommandLine("/proc/1/cmdline"),
                coreLimit.SoftLimit,
                coreLimit.HardLimit,
                IsLimitDisabled(coreLimit.SoftLimit),
                TryReadTrimmed("/proc/sys/kernel/core_pattern"),
                ClassifyAppArmorProfile(mandatoryAccessControlProfile),
                ClassifySelinuxContext(mandatoryAccessControlProfile),
                TryReadSelinuxEnforcing());
        }

        private static IReadOnlyList<LinuxMountInfoEntry> ReadMountInfo()
        {
            const string mountInfoPath = "/proc/self/mountinfo";
            if (!File.Exists(mountInfoPath))
            {
                return [];
            }

            var entries = new List<LinuxMountInfoEntry>();
            foreach (string line in File.ReadLines(mountInfoPath))
            {
                LinuxMountInfoEntry? entry = TryParseMountInfo(line);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }

        private static LinuxMountInfoEntry? TryParseMountInfo(string line)
        {
            string[] parts = line.Split(' ');
            if (parts.Length < 6)
            {
                return null;
            }

            int separatorIndex = Array.IndexOf(parts, "-");
            string mountSource = separatorIndex >= 0 && separatorIndex + 2 < parts.Length
                ? DecodeMountInfoPath(parts[separatorIndex + 2])
                : string.Empty;

            return new LinuxMountInfoEntry(
                DecodeMountInfoPath(parts[4]),
                parts[5].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                mountSource);
        }

        private static string DecodeMountInfoPath(string value)
        {
            return value
                .Replace(@"\040", " ", StringComparison.Ordinal)
                .Replace(@"\011", "\t", StringComparison.Ordinal)
                .Replace(@"\012", "\n", StringComparison.Ordinal)
                .Replace(@"\134", @"\", StringComparison.Ordinal);
        }

        private static bool? TryGetRootFilesystemReadOnly(IReadOnlyList<LinuxMountInfoEntry> mounts)
        {
            LinuxMountInfoEntry? root = mounts.FirstOrDefault(mount => mount.MountPoint == "/");
            if (root is null)
            {
                return null;
            }

            return root.Options.Contains("ro", StringComparer.Ordinal);
        }

        private static bool IsDockerSocketMounted(IReadOnlyList<LinuxMountInfoEntry> mounts)
        {
            return DockerSocketPaths.Any(path =>
                mounts.Any(mount =>
                    string.Equals(mount.MountPoint, path, StringComparison.Ordinal)
                    || string.Equals(mount.MountSource, path, StringComparison.Ordinal)
                    || mount.MountSource.EndsWith("/docker.sock", StringComparison.Ordinal))
                || PathExists(path));
        }

        private static bool? TryDetectHostPidNamespace(bool isContainer)
        {
            if (!isContainer)
            {
                return null;
            }

            string? procOneCommand = TryReadCommandLine("/proc/1/cmdline");
            if (string.IsNullOrWhiteSpace(procOneCommand))
            {
                return null;
            }

            string normalized = procOneCommand.ToLowerInvariant();
            if (normalized.Contains("systemd", StringComparison.Ordinal)
                || normalized.Contains("/sbin/init", StringComparison.Ordinal)
                || normalized == "init"
                || normalized.StartsWith("init ", StringComparison.Ordinal))
            {
                return true;
            }

            if (normalized.Contains("dotnet", StringComparison.Ordinal)
                || normalized.Contains("cotton", StringComparison.Ordinal)
                || normalized.Contains("docker-entrypoint", StringComparison.Ordinal)
                || normalized.Contains("tini", StringComparison.Ordinal)
                || normalized.Contains("dumb-init", StringComparison.Ordinal)
                || normalized.Contains("s6-svscan", StringComparison.Ordinal))
            {
                return false;
            }

            return null;
        }

        private static CoreLimitSnapshot ReadCoreLimit()
        {
            const string limitsPath = "/proc/self/limits";
            if (!File.Exists(limitsPath))
            {
                return CoreLimitSnapshot.Empty;
            }

            foreach (string line in File.ReadLines(limitsPath))
            {
                if (!line.StartsWith("Max core file size", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] parts = MultiWhitespace.Split(line.Trim());
                if (parts.Length >= 3)
                {
                    return new CoreLimitSnapshot(parts[1], parts[2]);
                }
            }

            return CoreLimitSnapshot.Empty;
        }

        private static bool? IsLimitDisabled(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (value.Equals("unlimited", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
                ? parsed == 0
                : null;
        }

        private static string? TryReadTrimmed(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string? TryReadCommandLine(string path)
        {
            string? raw = TryReadTrimmed(path);
            if (string.IsNullOrEmpty(raw))
            {
                return raw;
            }

            return raw.Replace('\0', ' ').Trim();
        }

        private static bool PathExists(string path)
        {
            try
            {
                _ = File.GetAttributes(path);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static string? ClassifyAppArmorProfile(string? profile)
        {
            if (string.IsNullOrWhiteSpace(profile) || profile.Contains(':', StringComparison.Ordinal))
            {
                return null;
            }

            return profile;
        }

        private static string? ClassifySelinuxContext(string? profile)
        {
            if (string.IsNullOrWhiteSpace(profile) || !profile.Contains(':', StringComparison.Ordinal))
            {
                return null;
            }

            return profile;
        }

        private static bool? TryReadSelinuxEnforcing()
        {
            string? raw = TryReadTrimmed("/sys/fs/selinux/enforce");
            return raw switch
            {
                "0" => false,
                "1" => true,
                _ => null,
            };
        }

        private record LinuxMountInfoEntry(
            string MountPoint,
            IReadOnlyList<string> Options,
            string MountSource);

        private record CoreLimitSnapshot(string? SoftLimit, string? HardLimit)
        {
            public static CoreLimitSnapshot Empty { get; } = new(null, null);
        }
    }
}
