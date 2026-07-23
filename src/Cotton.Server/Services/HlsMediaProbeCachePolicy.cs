// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Previews;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Defines cache lifetimes for HLS media-probe results.
    /// </summary>
    internal static class HlsMediaProbeCachePolicy
    {
        private static readonly TimeSpan SuccessfulProbeLifetime = TimeSpan.FromHours(1);
        private static readonly TimeSpan UnavailableProbeLifetime = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Returns the cache lifetime for a media-probe result.
        /// </summary>
        public static TimeSpan GetLifetime(MediaProbeInfo? probe)
        {
            return probe is null
                ? UnavailableProbeLifetime
                : SuccessfulProbeLifetime;
        }
    }
}
