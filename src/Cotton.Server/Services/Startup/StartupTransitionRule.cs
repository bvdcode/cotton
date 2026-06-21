// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Helpers;

namespace Cotton.Server.Services.Startup
{
    internal class StartupTransitionRule(
        string _targetMinimumVersion,
        string _requiredMinimumVersion,
        string _requiredMaximumExclusiveVersion,
        TimeSpan _requiredRunDuration,
        string _title,
        string _message)
    {
        public string RequiredVersionRange => $">= {_requiredMinimumVersion}, < {_requiredMaximumExclusiveVersion}";

        public bool AppliesTo(string currentVersion)
        {
            return SemanticVersionComparer.IsGreaterThanOrEqual(currentVersion, _targetMinimumVersion);
        }

        public bool IsSatisfiedBy(string recordedVersion, DateTime recordedAt, DateTime utcNow)
        {
            return SemanticVersionComparer.IsGreaterThanOrEqual(recordedVersion, _requiredMinimumVersion)
                && SemanticVersionComparer.IsLessThan(recordedVersion, _requiredMaximumExclusiveVersion)
                && recordedAt <= utcNow.Subtract(_requiredRunDuration);
        }

        public StartupBlocker CreateBlocker(
            string currentVersion,
            string? lastRecordedVersion)
        {
            return new StartupBlocker
            {
                Kind = "version-transition-required",
                Title = _title,
                Message = _message,
                CurrentVersion = currentVersion,
                RequiredVersion = _requiredMinimumVersion,
                RequiredVersionRange = RequiredVersionRange,
                LastRecordedVersion = lastRecordedVersion,
            };
        }
    }
}
