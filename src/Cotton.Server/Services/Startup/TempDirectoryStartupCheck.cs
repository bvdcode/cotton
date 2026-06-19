// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;

namespace Cotton.Server.Services.Startup
{
    internal sealed class TempDirectoryStartupCheck(
        TempDirectoryProbe _probe,
        ILogger<TempDirectoryStartupCheck> _logger) : IStartupCheck
    {
        public const string BlockerKind = "temp-directory-not-writable";

        public Task<StartupBlocker?> ValidateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TempDirectoryProbeResult result = _probe.Probe();
            if (result.Writable)
            {
                return Task.FromResult<StartupBlocker?>(null);
            }

            _logger.LogCritical(
                "Cotton startup is blocked because the OS temp directory {TempDirectory} is not writable: {Error}",
                string.IsNullOrWhiteSpace(result.TempPath) ? "<empty>" : result.TempPath,
                result.Error ?? "<none>");

            return Task.FromResult<StartupBlocker?>(new StartupBlocker
            {
                Kind = BlockerKind,
                Title = "Cotton cannot write to the OS temp directory.",
                Message = $"Cotton uses the OS temp directory ({FormatTempPath(result.TempPath)}) for database backups/restores, S3 upload spooling, and preview tooling. Make it writable and restart. In Docker Compose with read_only: true, mount writable scratch storage at /tmp, for example tmpfs: [\"/tmp\"], or bind-mount a fast writable disk at /tmp. {FormatError(result.Error)}".Trim(),
            });
        }

        private static string FormatTempPath(string tempPath)
        {
            return string.IsNullOrWhiteSpace(tempPath) ? "unknown path" : tempPath;
        }

        private static string FormatError(string? error)
        {
            return string.IsNullOrWhiteSpace(error) ? string.Empty : $"Error: {error}";
        }
    }
}
