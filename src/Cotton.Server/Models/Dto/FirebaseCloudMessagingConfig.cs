// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents Firebase Cloud Messaging configuration for server-side Android push delivery.
    /// </summary>
    public class FirebaseCloudMessagingConfig
    {
        /// <summary>
        /// Gets or sets the Firebase project identifier.
        /// </summary>
        public string ProjectId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the service account JSON used by server-side FCM delivery.
        /// </summary>
        public string ServiceAccountJson { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether a service account JSON value is already configured.
        /// </summary>
        public bool HasServiceAccountJson { get; set; }
    }
}
