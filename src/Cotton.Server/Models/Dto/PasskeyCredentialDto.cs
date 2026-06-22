// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the passkey credential API payload.
    /// </summary>
    public class PasskeyCredentialDto
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        public Guid Id { get; set; }
        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string Name { get; set; } = null!;
        /// <summary>
        /// Gets or sets the WebAuthn credential identifier encoded for transport.
        /// </summary>
        public string CredentialId { get; set; } = null!;
        /// <summary>
        /// Gets or sets the authenticator transports reported by the browser.
        /// </summary>
        public string[] Transports { get; set; } = [];
        /// <summary>
        /// Indicates whether the passkey can be backed up by the authenticator provider.
        /// </summary>
        public bool IsBackupEligible { get; set; }
        /// <summary>
        /// Indicates whether the passkey is currently backed up by the authenticator provider.
        /// </summary>
        public bool IsBackedUp { get; set; }
        /// <summary>
        /// Gets or sets the timestamp when the resource was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
        /// <summary>
        /// Gets or sets the timestamp when the credential was last used.
        /// </summary>
        public DateTime? LastUsedAt { get; set; }
    }
}
