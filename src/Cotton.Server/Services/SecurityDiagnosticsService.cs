// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Extensions;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Cotton.Server.Services
{
    public class SecurityDiagnosticsService(
        CottonDbContext dbContext,
        ProcessHardeningStatus hardeningStatus,
        MasterKeyRuntimeState masterKeyRuntimeState,
        DatabaseIntegrityDiagnosticsService databaseIntegrityDiagnostics,
        TempDirectoryProbe tempDirectoryProbe,
        SettingsProvider settingsProvider)
    {
        public async Task<SecurityDiagnosticsDto> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            LinuxProcStatus procStatus = LinuxProcessHardening.SnapshotProcStatus();
            uint? effectiveUserId = LinuxProcessHardening.TryGetEffectiveUserId();
            int? dumpable = LinuxProcessHardening.TryGetDumpable() ?? hardeningStatus.DumpableAfter;
            string? dotnetEnableDiagnostics = Environment.GetEnvironmentVariable("DOTNET_EnableDiagnostics");
            string? comPlusEnableDiagnostics = Environment.GetEnvironmentVariable("COMPlus_EnableDiagnostics");
            bool dotnetDiagnosticsDisabled = IsZero(dotnetEnableDiagnostics) || IsZero(comPlusEnableDiagnostics);
            bool isContainer = IsContainer();
            bool isPublicInstance = Constants.IsPublicInstance;
            ServerSettingsSnapshot settings = settingsProvider.GetServerSettings();
            IPAddress? configuredProxy = settings.TrustedProxyIpAddress;
            string? trustedProxyIpAddress = configuredProxy is null
                ? null
                : TrustedProxyRequestExtensions.FormatConfiguredProxy(
                    configuredProxy,
                    settings.TrustedProxyPrefixLength);
            TempDirectoryProbeResult tempDirectory = tempDirectoryProbe.Probe();
            LinuxContainerSecuritySnapshot containerSecurity = LinuxContainerSecurity.Snapshot(isContainer);
            AdminTotpDiagnosticsDto adminTotp = await GetAdminTotpDiagnosticsAsync(cancellationToken);
            DatabaseIntegrityDiagnosticsDto databaseIntegrity = await databaseIntegrityDiagnostics
                .GetSnapshotAsync(cancellationToken);
            CpuFeatureDiagnosticsDto cpuFeatures = CpuFeatureDiagnostics.Snapshot();

            LinuxProcessSecurityDto linuxProcess = new()
            {
                HardeningRequested = hardeningStatus.Requested,
                HardeningApplied = hardeningStatus.Applied,
                HardeningError = hardeningStatus.Error,
                Dumpable = dumpable,
                EffectiveUserId = effectiveUserId,
                RunningAsRoot = effectiveUserId.HasValue ? effectiveUserId.Value == 0 : null,
                NoNewPrivileges = procStatus.NoNewPrivileges,
                SeccompMode = procStatus.SeccompMode,
                SeccompFilters = procStatus.SeccompFilters,
                EffectiveCapabilitiesHex = procStatus.EffectiveCapabilitiesHex,
                HasSysPtraceCapability = procStatus.HasSysPtraceCapability,
            };

            DotNetDiagnosticsDto dotnetDiagnostics = new()
            {
                Disabled = dotnetDiagnosticsDisabled,
                DotNetEnableDiagnostics = dotnetEnableDiagnostics,
                ComPlusEnableDiagnostics = comPlusEnableDiagnostics,
            };

            LinuxContainerSecurityDto linuxContainer = new()
            {
                RootFilesystemReadOnly = containerSecurity.RootFilesystemReadOnly,
                DockerSocketMounted = containerSecurity.DockerSocketMounted,
                HostPidNamespaceLikely = containerSecurity.HostPidNamespaceLikely,
                ProcOneCommandLine = containerSecurity.ProcOneCommandLine,
                CoreDumpSoftLimit = containerSecurity.CoreDumpSoftLimit,
                CoreDumpHardLimit = containerSecurity.CoreDumpHardLimit,
                CoreDumpSoftLimitDisabled = containerSecurity.CoreDumpSoftLimitDisabled,
                CorePattern = containerSecurity.CorePattern,
                AppArmorProfile = containerSecurity.AppArmorProfile,
                SelinuxContext = containerSecurity.SelinuxContext,
                SelinuxEnforcing = containerSecurity.SelinuxEnforcing,
            };

            IReadOnlyList<SecurityDiagnosticWarningDto> warnings = SecurityDiagnosticsWarningBuilder.Build(
                isContainer,
                isPublicInstance,
                masterKeyRuntimeState,
                dotnetDiagnostics,
                linuxProcess,
                linuxContainer,
                adminTotp,
                databaseIntegrity,
                tempDirectory,
                trustedProxyIpAddress);

            return new SecurityDiagnosticsDto
            {
                OperatingSystem = Environment.OSVersion.ToString(),
                IsLinux = OperatingSystem.IsLinux(),
                IsContainer = isContainer,
                MasterKeySource = masterKeyRuntimeState.Source,
                IsPublicInstance = isPublicInstance,
                TrustedProxyIpAddress = trustedProxyIpAddress,
                MasterKeyEnvironmentVariableWasConfigured = masterKeyRuntimeState.EnvironmentVariableWasConfigured,
                MasterKeyEnvironmentVariablePresentInProcess = masterKeyRuntimeState.EnvironmentVariablePresentAfterResolution,
                TempDirectoryPath = tempDirectory.TempPath,
                TempDirectoryWritable = tempDirectory.Writable,
                TempDirectoryError = tempDirectory.Error,
                DotNetDiagnostics = dotnetDiagnostics,
                LinuxProcess = linuxProcess,
                LinuxContainer = linuxContainer,
                AdminTotp = adminTotp,
                DatabaseIntegrity = databaseIntegrity,
                CpuFeatures = cpuFeatures,
                SecurityScore = SecurityDiagnosticsWarningBuilder.CalculateScore(warnings),
                Warnings = warnings,
            };
        }

        private async Task<AdminTotpDiagnosticsDto> GetAdminTotpDiagnosticsAsync(CancellationToken cancellationToken)
        {
            int adminCount = await dbContext.Users
                .CountAsync(user => user.Role == UserRole.Admin, cancellationToken);
            int adminsWithTotp = await dbContext.Users
                .CountAsync(user => user.Role == UserRole.Admin && user.IsTotpEnabled, cancellationToken);

            return new AdminTotpDiagnosticsDto
            {
                AdminCount = adminCount,
                AdminsWithTotp = adminsWithTotp,
                AdminsWithoutTotp = adminCount - adminsWithTotp,
            };
        }

        private static bool IsZero(string? value)
        {
            return string.Equals(value, "0", StringComparison.Ordinal);
        }

        private static bool IsContainer()
        {
            if (string.Equals(
                Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
                "true",
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (File.Exists("/.dockerenv"))
            {
                return true;
            }

            const string cgroupPath = "/proc/1/cgroup";
            if (!File.Exists(cgroupPath))
            {
                return false;
            }

            return File.ReadLines(cgroupPath).Any(line =>
                line.Contains("docker", StringComparison.OrdinalIgnoreCase)
                || line.Contains("kubepods", StringComparison.OrdinalIgnoreCase)
                || line.Contains("containerd", StringComparison.OrdinalIgnoreCase));
        }
    }
}
