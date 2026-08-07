// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text.Json.Serialization;

namespace Cotton.Server.Models.Enums
{
    /// <summary>
    /// Describes the generic authenticator category used for localized passkey fallback text.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<PasskeyAuthenticatorKind>))]
    public enum PasskeyAuthenticatorKind
    {
        /// <summary>
        /// The authenticator category could not be determined.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// An external or removable security key.
        /// </summary>
        SecurityKey = 1,

        /// <summary>
        /// A platform authenticator or synchronized device passkey.
        /// </summary>
        Device = 2
    }
}
