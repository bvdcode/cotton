// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class CpuFeatureDiagnosticsDto
    {
        public string Architecture { get; init; } = string.Empty;

        public string OsArchitecture { get; init; } = string.Empty;

        public int LogicalProcessorCount { get; init; }

        public string? VendorId { get; init; }

        public string? ModelName { get; init; }

        public bool AesGcmHardwareAccelerationLikely { get; init; }

        public CpuFeatureAvailabilityDto AesNi { get; init; } = new();

        public CpuFeatureAvailabilityDto Pclmulqdq { get; init; } = new();

        public CpuFeatureAvailabilityDto Vaes { get; init; } = new();

        public CpuFeatureAvailabilityDto Vpclmulqdq { get; init; } = new();

        public CpuFeatureAvailabilityDto Avx2 { get; init; } = new();

        public CpuFeatureAvailabilityDto Tme { get; init; } = new();

        public CpuFeatureAvailabilityDto TmeMk { get; init; } = new();

        public CpuFeatureAvailabilityDto Pconfig { get; init; } = new();

        public IReadOnlyList<string> LinuxCpuFlags { get; init; } = [];
    }
}
