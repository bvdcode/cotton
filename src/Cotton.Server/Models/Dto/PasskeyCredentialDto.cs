// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;
using Cotton.Server.Models.Enums;

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
        /// Gets or sets the optional user-authored label.
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// Gets or sets the WebAuthn credential identifier encoded for transport.
        /// </summary>
        public string CredentialId { get; set; } = null!;

        /// <summary>
        /// Gets or sets the authenticator transports reported by the browser.
        /// </summary>
        public string[] Transports { get; set; } = [];

        /// <summary>
        /// Gets or sets the authenticator attestation GUID.
        /// </summary>
        public Guid AaGuid { get; set; }

        /// <summary>
        /// Gets or sets the detected authenticator or passkey provider name.
        /// </summary>
        public string? AuthenticatorName { get; set; }

        /// <summary>
        /// Gets or sets the generic authenticator category used for localized fallback text.
        /// </summary>
        public PasskeyAuthenticatorKind AuthenticatorKind { get; set; }

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
