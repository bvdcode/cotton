// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Auth
{
    /// <summary>
    /// Names the configured auth rate limit policies.
    /// </summary>
    public static class AuthRateLimitPolicies
    {
        /// <summary>
        /// Defines the interactive.
        /// </summary>
        public const string Interactive = "auth.interactive";
        /// <summary>
        /// Defines the refresh.
        /// </summary>
        public const string Refresh = "auth.refresh";
        /// <summary>
        /// Defines the anonymous public share archive policy.
        /// </summary>
        public const string PublicShareArchive = "public-share.archive";
    }
}
