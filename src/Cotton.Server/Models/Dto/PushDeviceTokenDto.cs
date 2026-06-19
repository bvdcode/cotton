// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents a registered push device token without exposing the token value.
    /// </summary>
    public class PushDeviceTokenDto : BaseDto<Guid>
    {
        /// <summary>
        /// Gets or sets the push provider.
        /// </summary>
        public PushDeviceTokenProvider Provider { get; set; }
        /// <summary>
        /// Gets or sets the mobile platform.
        /// </summary>
        public PushDeviceTokenPlatform Platform { get; set; }
        /// <summary>
        /// Gets or sets the auth session identifier.
        /// </summary>
        public string SessionId { get; set; } = null!;
        /// <summary>
        /// Gets or sets the client-reported device name.
        /// </summary>
        public string? DeviceName { get; set; }
        /// <summary>
        /// Gets or sets the client-reported application version.
        /// </summary>
        public string? AppVersion { get; set; }
        /// <summary>
        /// Gets or sets the latest registration timestamp.
        /// </summary>
        public DateTime LastRegisteredAt { get; set; }
        /// <summary>
        /// Gets or sets the revocation timestamp.
        /// </summary>
        public DateTime? RevokedAt { get; set; }
    }
}
