// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Cli
{
    internal sealed class SyncCliRunProgressWriter : IProgress<SyncRunProgress>
    {
        private static readonly TimeSpan DefaultMinimumFirstReportElapsed = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan DefaultMinimumReportInterval = TimeSpan.FromSeconds(5);
        private readonly object _gate = new();
        private readonly TextWriter _output;
        private readonly TimeSpan _minimumFirstReportElapsed;
        private readonly TimeSpan _minimumReportInterval;
        private DateTime? _lastReportedAtUtc;
        private SyncRunProgressStage? _lastReportedStage;
        private bool _hasReported;

        public SyncCliRunProgressWriter(
            TextWriter output,
            TimeSpan? minimumFirstReportElapsed = null,
            TimeSpan? minimumReportInterval = null)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _minimumFirstReportElapsed = minimumFirstReportElapsed ?? DefaultMinimumFirstReportElapsed;
            _minimumReportInterval = minimumReportInterval ?? DefaultMinimumReportInterval;
        }

        public void Report(SyncRunProgress value)
        {
            ArgumentNullException.ThrowIfNull(value);
            lock (_gate)
            {
                if (!ShouldReport(value))
                {
                    return;
                }

                _output.WriteLine(Format(value));
                _lastReportedAtUtc = value.OccurredAtUtc;
                _lastReportedStage = value.Stage;
                _hasReported = true;
            }
        }

        private bool ShouldReport(SyncRunProgress value)
        {
            if (value.IsCompleted)
            {
                return _hasReported;
            }

            TimeSpan elapsed = value.OccurredAtUtc - value.StartedAtUtc;
            if (elapsed < _minimumFirstReportElapsed)
            {
                return false;
            }

            if (!_hasReported || _lastReportedStage != value.Stage)
            {
                return true;
            }

            return !_lastReportedAtUtc.HasValue
                || value.OccurredAtUtc - _lastReportedAtUtc.Value >= _minimumReportInterval;
        }

        private static string Format(SyncRunProgress value)
        {
            string line = "Progress: "
                + FormatStage(value.Stage)
                + " "
                + value.FilesCompleted.ToStringInvariant()
                + "/"
                + (value.FilesTotal?.ToStringInvariant() ?? "?")
                + " files";
            if (value.BytesTotal.HasValue && value.BytesTotal.Value > 0)
            {
                line += ", "
                    + FormatBytes(value.BytesCompleted)
                    + "/"
                    + FormatBytes(value.BytesTotal.Value);
            }

            if (!string.IsNullOrWhiteSpace(value.CurrentPath))
            {
                line += ", current: " + value.CurrentPath;
            }

            line += ", elapsed: " + FormatElapsed(value.OccurredAtUtc - value.StartedAtUtc);
            if (value.IsCompleted)
            {
                line += ", completed";
            }

            return line;
        }

        private static string FormatStage(SyncRunProgressStage stage)
        {
            return stage switch
            {
                SyncRunProgressStage.ScanningLocal => "scanning local",
                SyncRunProgressStage.ScanningRemote => "scanning remote",
                SyncRunProgressStage.ReconcilingDirectories => "reconciling folders",
                SyncRunProgressStage.ReconcilingFiles => "reconciling files",
                SyncRunProgressStage.Completed => "completed",
                _ => "syncing",
            };
        }

        private static string FormatBytes(long bytes)
        {
            const double KiB = 1024;
            const double MiB = KiB * 1024;
            const double GiB = MiB * 1024;
            if (bytes >= GiB)
            {
                return (bytes / GiB).ToStringInvariant() + " GiB";
            }

            if (bytes >= MiB)
            {
                return (bytes / MiB).ToStringInvariant() + " MiB";
            }

            if (bytes >= KiB)
            {
                return (bytes / KiB).ToStringInvariant() + " KiB";
            }

            return bytes.ToStringInvariant() + " B";
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            return elapsed < TimeSpan.Zero
                ? "00:00:00"
                : elapsed.ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
