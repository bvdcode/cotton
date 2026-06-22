// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;

namespace Cotton.Models
{
    /// <summary>
    /// Describes one storage pipeline probe iteration.
    /// </summary>
    public class StoragePipelineProbeIteration
    {
        /// <summary>
        /// Gets or sets whether this iteration was used only to warm the pipeline.
        /// </summary>
        public bool IsWarmup { get; set; }

        /// <summary>
        /// Gets or sets how long the write leg took.
        /// </summary>
        public double WriteMilliseconds { get; set; }

        /// <summary>
        /// Gets or sets how long the read leg took.
        /// </summary>
        public double ReadMilliseconds { get; set; }

        /// <summary>
        /// Gets or sets how long the full write-read-delete operation took.
        /// </summary>
        public double RoundtripMilliseconds { get; set; }

        /// <summary>
        /// Gets or sets write throughput in MiB/s.
        /// </summary>
        public double WriteMebibytesPerSecond { get; set; }

        /// <summary>
        /// Gets or sets read throughput in MiB/s.
        /// </summary>
        public double ReadMebibytesPerSecond { get; set; }

        /// <summary>
        /// Gets or sets the stored backend size after processors have transformed the payload.
        /// </summary>
        public long StoredSizeBytes { get; set; }
    }
}
