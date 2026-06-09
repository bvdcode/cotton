// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.SyncPairs
{
    internal record NormalizedPath(string Value, bool WindowsStyle)
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
