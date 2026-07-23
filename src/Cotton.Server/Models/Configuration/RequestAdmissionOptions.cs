// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Configuration
{
    /// <summary>
    /// Configures process-wide and per-client HTTP request admission.
    /// </summary>
    public class RequestAdmissionOptions
    {
        /// <summary>
        /// Configuration section name.
        /// </summary>
        public const string SectionName = "RequestAdmission";

        /// <summary>
        /// Gets or sets the maximum number of concurrent admitted HTTP requests.
        /// </summary>
        public int GlobalConcurrentRequestLimit { get; set; } = 256;

        /// <summary>
        /// Gets or sets the maximum number of concurrent requests per identified client.
        /// </summary>
        public int ClientConcurrentRequestLimit { get; set; } = 32;

        /// <summary>
        /// Gets or sets the maximum number of requests waiting for process-wide capacity.
        /// </summary>
        public int GlobalQueueLimit { get; set; } = 256;

        /// <summary>
        /// Gets or sets the maximum number of requests waiting for capacity per identified client.
        /// </summary>
        public int ClientQueueLimit { get; set; } = 32;

        /// <summary>
        /// Validates configured request limits.
        /// </summary>
        public void Validate()
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(GlobalConcurrentRequestLimit);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ClientConcurrentRequestLimit);
            ArgumentOutOfRangeException.ThrowIfNegative(GlobalQueueLimit);
            ArgumentOutOfRangeException.ThrowIfNegative(ClientQueueLimit);
            if (ClientQueueLimit > int.MaxValue - ClientConcurrentRequestLimit)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ClientQueueLimit),
                    "The per-client active and queued request limits are too large.");
            }
            if (ClientConcurrentRequestLimit > GlobalConcurrentRequestLimit)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ClientConcurrentRequestLimit),
                    "The per-client request limit cannot exceed the global request limit.");
            }
        }
    }
}
