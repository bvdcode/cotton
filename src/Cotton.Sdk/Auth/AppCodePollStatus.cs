// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk.Auth
{
    /// <summary>
    /// Represents an app-code authorization polling outcome.
    /// </summary>
    public enum AppCodePollStatus
    {
        /// <summary>
        /// The polling status is not known.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The browser approval is still pending.
        /// </summary>
        Pending = 1,

        /// <summary>
        /// The browser approval succeeded and returned tokens.
        /// </summary>
        Approved = 2,

        /// <summary>
        /// The browser approval was denied.
        /// </summary>
        Denied = 3,

        /// <summary>
        /// The app-code session expired.
        /// </summary>
        Expired = 4,

        /// <summary>
        /// The app-code session was not found.
        /// </summary>
        NotFound = 5,

        /// <summary>
        /// The server asked the client to slow down polling.
        /// </summary>
        TooManyRequests = 6,
    }
}
