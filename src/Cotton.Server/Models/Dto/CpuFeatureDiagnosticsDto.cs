// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents CPU feature diagnostics.
    /// </summary>
    public class CpuFeatureDiagnosticsDto
    {
        /// <summary>
        /// Gets process architecture.
        /// </summary>
        public string Architecture { get; init; } = string.Empty;

        /// <summary>
        /// Gets OS architecture.
        /// </summary>
        public string OsArchitecture { get; init; } = string.Empty;

        /// <summary>
        /// Gets logical processor count visible to the process.
        /// </summary>
        public int LogicalProcessorCount { get; init; }

        /// <summary>
        /// Gets CPU vendor ID reported by Linux procfs.
        /// </summary>
        public string? VendorId { get; init; }

        /// <summary>
        /// Gets CPU model name reported by Linux procfs.
        /// </summary>
        public string? ModelName { get; init; }

        /// <summary>
        /// Indicates whether AES-GCM hardware acceleration is likely available to the runtime.
        /// </summary>
        public bool AesGcmHardwareAccelerationLikely { get; init; }

        /// <summary>
        /// Gets AES-NI feature availability.
        /// </summary>
        public CpuFeatureAvailabilityDto AesNi { get; init; } = new();

        /// <summary>
        /// Gets PCLMULQDQ feature availability.
        /// </summary>
        public CpuFeatureAvailabilityDto Pclmulqdq { get; init; } = new();

        /// <summary>
        /// Gets VAES feature availability.
        /// </summary>
        public CpuFeatureAvailabilityDto Vaes { get; init; } = new();

        /// <summary>
        /// Gets VPCLMULQDQ feature availability.
        /// </summary>
        public CpuFeatureAvailabilityDto Vpclmulqdq { get; init; } = new();

        /// <summary>
        /// Gets AVX2 feature availability.
        /// </summary>
        public CpuFeatureAvailabilityDto Avx2 { get; init; } = new();

        /// <summary>
        /// Gets total memory encryption feature availability.
        /// </summary>
        public CpuFeatureAvailabilityDto Tme { get; init; } = new();

        /// <summary>
        /// Gets multi-key total memory encryption feature availability.
        /// </summary>
        public CpuFeatureAvailabilityDto TmeMk { get; init; } = new();

        /// <summary>
        /// Gets PCONFIG feature availability used by some TME-MK platforms.
        /// </summary>
        public CpuFeatureAvailabilityDto Pconfig { get; init; } = new();

        /// <summary>
        /// Gets raw Linux CPU flags visible through procfs.
        /// </summary>
        public IReadOnlyList<string> LinuxCpuFlags { get; init; } = [];
    }
}
