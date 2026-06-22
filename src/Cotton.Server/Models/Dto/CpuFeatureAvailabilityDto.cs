// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents availability of a single CPU feature.
    /// </summary>
    public class CpuFeatureAvailabilityDto
    {
        /// <summary>
        /// Gets the runtime intrinsic support status, when applicable.
        /// </summary>
        public bool? RuntimeSupported { get; init; }
        /// <summary>
        /// Gets Linux procfs flag presence, when available.
        /// </summary>
        public bool? LinuxFlagPresent { get; init; }
    }
}
