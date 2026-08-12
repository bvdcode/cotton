// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class CpuFeatureAvailabilityDto
    {
        public bool? RuntimeSupported { get; init; }

        public bool? LinuxFlagPresent { get; init; }
    }
}
