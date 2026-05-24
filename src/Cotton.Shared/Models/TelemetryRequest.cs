// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;

namespace Cotton.Models
{
    /// <summary>
    /// Represents a telemetry request containing the instance identifier, server URL, and associated JSON content.
    /// </summary>
    public class TelemetryRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier for this instance.
        /// </summary>
        public Guid InstanceId { get; set; }

        /// <summary>
        /// Gets or sets the URL of the server to which the application connects.
        /// </summary>
        public string ServerUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of users associated with the current instance.
        /// </summary>
        public int Users { get; set; }

        /// <summary>
        /// Gets or sets the version identifier for the current instance.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of nodes.
        /// </summary>
        public int Nodes { get; set; }

        /// <summary>
        /// Gets or sets the number of files associated with the current instance.
        /// </summary>
        public int Files { get; set; }

        /// <summary>
        /// Gets or sets an optional synthetic measurement of the active storage pipeline.
        /// </summary>
        public StoragePipelineProbeResult? StoragePipelineProbe { get; set; }
    }

    /// <summary>
    /// Describes a small end-to-end storage pipeline probe collected during opt-in telemetry.
    /// </summary>
    public sealed class StoragePipelineProbeResult
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

    /// <summary>
    /// Describes one storage pipeline probe iteration.
    /// </summary>
    public sealed class StoragePipelineProbeIteration
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
