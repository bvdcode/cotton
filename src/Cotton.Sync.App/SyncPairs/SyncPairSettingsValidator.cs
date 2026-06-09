// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Text.RegularExpressions;

namespace Cotton.Sync.App.SyncPairs
{

    /// <summary>
    /// Validates sync-pair settings before they are persisted or used by the sync supervisor.
    /// </summary>
    public sealed partial class SyncPairSettingsValidator
    {
        private static readonly SyncPairMode[] SupportedModes = [SyncPairMode.FullMirror];

        /// <summary>
        /// Validates a set of sync-pair settings.
        /// </summary>
        public SyncPairValidationResult Validate(IReadOnlyCollection<SyncPairSettings> syncPairs)
        {
            ArgumentNullException.ThrowIfNull(syncPairs);
            var errors = new List<SyncPairValidationError>();
            foreach (SyncPairSettings syncPair in syncPairs)
            {
                ValidateSingle(syncPair, errors);
            }

            ValidateLocalRootOverlaps(syncPairs, errors);
            return new SyncPairValidationResult(errors);
        }

        private static void ValidateSingle(SyncPairSettings syncPair, ICollection<SyncPairValidationError> errors)
        {
            ArgumentNullException.ThrowIfNull(syncPair);
            if (syncPair.Id == Guid.Empty)
            {
                Add(errors, SyncPairValidationIssue.EmptyId, syncPair.Id, null, "Sync pair id is required.");
            }

            if (string.IsNullOrWhiteSpace(syncPair.DisplayName))
            {
                Add(errors, SyncPairValidationIssue.EmptyDisplayName, syncPair.Id, null, "Sync pair display name is required.");
            }

            if (string.IsNullOrWhiteSpace(syncPair.LocalRootPath))
            {
                Add(errors, SyncPairValidationIssue.EmptyLocalRootPath, syncPair.Id, null, "Local root path is required.");
            }

            if (syncPair.RemoteRootNodeId == Guid.Empty)
            {
                Add(errors, SyncPairValidationIssue.EmptyRemoteRootNodeId, syncPair.Id, null, "Remote root node id is required.");
            }

            if (string.IsNullOrWhiteSpace(syncPair.RemoteDisplayPath))
            {
                Add(errors, SyncPairValidationIssue.EmptyRemoteDisplayPath, syncPair.Id, null, "Remote display path is required.");
            }

            if (!SupportedModes.Contains(syncPair.Mode))
            {
                Add(errors, SyncPairValidationIssue.UnsupportedMode, syncPair.Id, null, "The selected sync mode is not implemented yet.");
            }
        }

        private static void ValidateLocalRootOverlaps(
            IReadOnlyCollection<SyncPairSettings> syncPairs,
            ICollection<SyncPairValidationError> errors)
        {
            List<NormalizedLocalRoot> roots = syncPairs
                .Where(static syncPair => !string.IsNullOrWhiteSpace(syncPair.LocalRootPath))
                .Select(static syncPair => new NormalizedLocalRoot(syncPair.Id, NormalizeLocalRoot(syncPair.LocalRootPath)))
                .ToList();
            for (int leftIndex = 0; leftIndex < roots.Count; leftIndex++)
            {
                for (int rightIndex = leftIndex + 1; rightIndex < roots.Count; rightIndex++)
                {
                    NormalizedLocalRoot left = roots[leftIndex];
                    NormalizedLocalRoot right = roots[rightIndex];
                    if (!left.Path.IsSameStyle(right.Path) || !left.Path.Overlaps(right.Path))
                    {
                        continue;
                    }

                    Add(
                        errors,
                        SyncPairValidationIssue.OverlappingLocalRoots,
                        left.SyncPairId,
                        right.SyncPairId,
                        left.Path.IsSamePath(right.Path)
                            ? "This folder is already syncing."
                            : "Sync folders cannot be inside each other.");
                }
            }
        }

        private static NormalizedPath NormalizeLocalRoot(string localRootPath)
        {
            string trimmed = localRootPath.Trim();
            bool windowsStyle = DriveRootRegex().IsMatch(trimmed) || trimmed.Contains('\\', StringComparison.Ordinal);
            char separator = windowsStyle ? '\\' : '/';
            string normalized = windowsStyle
                ? trimmed.Replace('/', '\\')
                : trimmed.Replace('\\', '/');
            normalized = CollapseSeparators(normalized, separator, windowsStyle);
            normalized = TrimTrailingSeparators(normalized, separator, windowsStyle);
            if (windowsStyle && normalized.Length >= 2 && normalized[1] == ':')
            {
                normalized = char.ToUpperInvariant(normalized[0]) + normalized[1..];
            }

            return new NormalizedPath(normalized, windowsStyle);
        }

        private static string CollapseSeparators(string path, char separator, bool windowsStyle)
        {
            if (windowsStyle && path.StartsWith(@"\\", StringComparison.Ordinal))
            {
                string collapsedUncTail = SeparatorRunRegex(separator).Replace(path[2..], separator.ToString());
                return @"\\" + collapsedUncTail;
            }

            return SeparatorRunRegex(separator).Replace(path, separator.ToString());
        }

        private static string TrimTrailingSeparators(string path, char separator, bool windowsStyle)
        {
            int minimumLength = 1;
            if (windowsStyle && DriveRootRegex().IsMatch(path))
            {
                minimumLength = 3;
            }

            while (path.Length > minimumLength && path[^1] == separator)
            {
                path = path[..^1];
            }

            return path;
        }

        private static void Add(
            ICollection<SyncPairValidationError> errors,
            SyncPairValidationIssue issue,
            Guid? syncPairId,
            Guid? otherSyncPairId,
            string message)
        {
            errors.Add(new SyncPairValidationError(issue, syncPairId, otherSyncPairId, message));
        }

        [GeneratedRegex(@"^[a-zA-Z]:([\\/]|$)", RegexOptions.CultureInvariant)]
        private static partial Regex DriveRootRegex();

        private static Regex SeparatorRunRegex(char separator)
        {
            return separator == '\\' ? BackslashRunRegex() : SlashRunRegex();
        }

        [GeneratedRegex(@"\\+", RegexOptions.CultureInvariant)]
        private static partial Regex BackslashRunRegex();

        [GeneratedRegex(@"/+", RegexOptions.CultureInvariant)]
        private static partial Regex SlashRunRegex();

        private sealed record NormalizedLocalRoot(Guid SyncPairId, NormalizedPath Path);

        private sealed record NormalizedPath(string Value, bool WindowsStyle)
        {
            public bool IsSameStyle(NormalizedPath other)
            {
                return WindowsStyle == other.WindowsStyle;
            }

            public bool Overlaps(NormalizedPath other)
            {
                StringComparison comparison = WindowsStyle ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                return IsSamePath(other, comparison)
                    || IsParentOf(other, comparison)
                    || other.IsParentOf(this, comparison);
            }

            public bool IsSamePath(NormalizedPath other)
            {
                StringComparison comparison = WindowsStyle ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                return IsSamePath(other, comparison);
            }

            private bool IsSamePath(NormalizedPath other, StringComparison comparison)
            {
                return string.Equals(Value, other.Value, comparison);
            }

            private bool IsParentOf(NormalizedPath child, StringComparison comparison)
            {
                char separator = WindowsStyle ? '\\' : '/';
                string prefix = Value.EndsWith(separator)
                    ? Value
                    : Value + separator;
                return child.Value.StartsWith(prefix, comparison);
            }
        }
    }
}
