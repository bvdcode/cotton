// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace Cotton.Server.Models.Requests
{
    /// <summary>
    /// Represents a push device token registration request payload.
    /// </summary>
    public class PushDeviceTokenRegistrationRequestDto
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
        /// Gets or sets the provider registration token.
        /// </summary>
        [Required]
        [MaxLength(PushDeviceToken.TokenMaxLength)]
        public string Token { get; set; } = null!;
        /// <summary>
        /// Gets or sets the client-reported device name.
        /// </summary>
        [MaxLength(PushDeviceToken.DeviceNameMaxLength)]
        public string? DeviceName { get; set; }
        /// <summary>
        /// Gets or sets the client-reported application version.
        /// </summary>
        [MaxLength(PushDeviceToken.AppVersionMaxLength)]
        public string? AppVersion { get; set; }
    }
}
