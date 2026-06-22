// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;

namespace Cotton.Models
{
    /// <summary>
    /// Describes a small end-to-end storage pipeline probe collected during opt-in telemetry.
    /// </summary>
    public class StoragePipelineProbeResult
    {
        /// <summary>
        /// Gets or sets when the probe finished.
        /// </summary>
        public DateTimeOffset CompletedAt { get; set; }

        /// <summary>
        /// Gets or sets the plaintext payload size used by each iteration.
        /// </summary>
        public int PayloadSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the coarse storage backend category measured by the probe.
        /// </summary>
        public string StorageBackend { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the warmup iteration result.
        /// </summary>
        public StoragePipelineProbeIteration Warmup { get; set; } = new StoragePipelineProbeIteration();

        /// <summary>
        /// Gets or sets the measured iteration result.
        /// </summary>
        public StoragePipelineProbeIteration Measured { get; set; } = new StoragePipelineProbeIteration();
    }
}
