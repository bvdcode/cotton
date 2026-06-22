// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Startup
{
    internal static class StartupTransitionRules
    {
        private static readonly TimeSpan RequiredTransitionRunDuration = TimeSpan.FromHours(24);

        public static IReadOnlyCollection<StartupTransitionRule> All { get; } =
        [
            new StartupTransitionRule(
                "0.5.0",
                "0.4.33",
                "0.5.0",
                RequiredTransitionRunDuration,
                "Cotton must run the 0.4.33 transition before starting this version.",
                "Start Cotton 0.4.33 first and keep it running for at least 24 hours. After that, stop it and start this version again.")
        ];
    }
}
