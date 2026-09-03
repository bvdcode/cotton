// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Dto;

namespace Cotton.Server.Services
{
    internal static class SecurityDiagnosticsWarningBuilder
    {
        public static IReadOnlyList<SecurityDiagnosticWarningDto> Build(
            bool isContainer,
            bool isPublicInstance,
            MasterKeyRuntimeState masterKey,
            DotNetDiagnosticsDto dotnetDiagnostics,
            LinuxProcessSecurityDto linuxProcess,
            LinuxContainerSecurityDto linuxContainer,
            AdminTotpDiagnosticsDto adminTotp,
            DatabaseIntegrityDiagnosticsDto databaseIntegrity,
            TempDirectoryProbeResult tempDirectory,
            string? trustedProxyIpAddress)
        {
            List<SecurityDiagnosticWarningDto> warnings = [];
            AddPublicInstanceWarning(warnings, isPublicInstance);
            AddTrustedProxyWarning(warnings, trustedProxyIpAddress);
            AddMasterKeyWarning(warnings, masterKey);
            AddAdminTotpWarning(warnings, adminTotp);
            AddDotNetDiagnosticsWarning(warnings, dotnetDiagnostics);
            AddTempDirectoryWarning(warnings, tempDirectory);
            LinuxSecurityWarningBuilder.AddWarnings(warnings, isContainer, linuxProcess, linuxContainer);
            AddHardeningWarning(warnings, linuxProcess);
            AddDatabaseIntegrityWarnings(warnings, databaseIntegrity);
            return warnings;
        }

        public static int CalculateScore(IReadOnlyList<SecurityDiagnosticWarningDto> warnings)
        {
            int penalty = warnings.Sum(warning => warning.Severity switch
            {
                "critical" => 3,
                "warning" => 2,
                "info" => 1,
                _ => 0,
            });

            return Math.Max(0, 10 - penalty);
        }

        private static void AddTrustedProxyWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            string? trustedProxyIpAddress)
        {
            if (!string.IsNullOrWhiteSpace(trustedProxyIpAddress))
            {
                return;
            }

            warnings.Add(Create(
                "trusted-proxy-not-configured",
                "warning",
                "No trusted reverse-proxy IP address is configured. Client-address headers are accepted from every connection for backward compatibility."));
        }

        private static void AddTempDirectoryWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            TempDirectoryProbeResult tempDirectory)
        {
            if (tempDirectory.Writable)
            {
                return;
            }

            string tempPath = string.IsNullOrWhiteSpace(tempDirectory.TempPath)
                ? "unknown path"
                : tempDirectory.TempPath;
            string error = string.IsNullOrWhiteSpace(tempDirectory.Error)
                ? string.Empty
                : $" Error: {tempDirectory.Error}";
            warnings.Add(Create(
                "temp-directory-not-writable",
                "critical",
                $"Cotton cannot write to the OS temp directory ({tempPath}). Database dumps/restores, S3 upload spooling, and preview tooling require writable scratch space. Mount a writable /tmp when using read_only: true, or bind-mount a fast writable disk at /tmp.{error}"));
        }

        private static void AddDatabaseIntegrityWarnings(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            DatabaseIntegrityDiagnosticsDto databaseIntegrity)
        {
            if (databaseIntegrity.UnsignedProtectedRows <= 0)
            {
                return;
            }

            warnings.Add(Create(
                "db-integrity-unsigned-rows",
                "critical",
                $"{databaseIntegrity.UnsignedProtectedRows} protected database rows are missing valid integrity signatures. Restore the affected rows from a trusted backup or run the required transition version before upgrading."));
        }

        private static void AddPublicInstanceWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            bool isPublicInstance)
        {
            if (!isPublicInstance)
            {
                return;
            }

            warnings.Add(Create(
                "public-instance",
                "warning",
                "This instance allows public/demo account creation. Keep quotas, default content, and abuse monitoring configured before exposing it on the internet."));
        }

        private static void AddMasterKeyWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            MasterKeyRuntimeState masterKey)
        {
            if (!masterKey.EnvironmentVariableWasConfigured)
            {
                return;
            }

            warnings.Add(Create(
                "master-key-from-environment",
                "warning",
                "This process was unlocked from COTTON_MASTER_KEY. Cotton clears its own process environment after reading it, but container runtimes may still expose configured environment variables through deployment metadata or docker exec environments."));
        }

        private static void AddAdminTotpWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            AdminTotpDiagnosticsDto adminTotp)
        {
            if (adminTotp.AdminsWithoutTotp <= 0)
            {
                return;
            }

            warnings.Add(Create(
                "admins-without-2fa",
                "warning",
                $"{adminTotp.AdminsWithoutTotp} of {adminTotp.AdminCount} admin accounts do not have 2FA enabled."));
        }

        private static void AddDotNetDiagnosticsWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            DotNetDiagnosticsDto dotnetDiagnostics)
        {
            if (dotnetDiagnostics.Disabled)
            {
                return;
            }

            warnings.Add(Create(
                "dotnet-diagnostics-enabled",
                "warning",
                "DOTNET diagnostics appear enabled. Production containers should set DOTNET_EnableDiagnostics=0 to disable debugger, profiler, EventPipe, and dump collection endpoints."));
        }

        private static void AddHardeningWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            LinuxProcessSecurityDto linuxProcess)
        {
            if (!linuxProcess.HardeningRequested || linuxProcess.HardeningApplied)
            {
                return;
            }

            warnings.Add(Create(
                "process-hardening-failed",
                "warning",
                linuxProcess.HardeningError ?? "Process hardening was requested but did not apply."));
        }

        private static SecurityDiagnosticWarningDto Create(string code, string severity, string message)
        {
            return new SecurityDiagnosticWarningDto
            {
                Code = code,
                Severity = severity,
                Message = message,
            };
        }
    }
}
