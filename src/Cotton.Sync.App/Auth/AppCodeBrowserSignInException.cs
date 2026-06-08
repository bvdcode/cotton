// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sdk.Auth;

namespace Cotton.Sync.App.Auth
{
    /// <summary>
    /// Represents a terminal browser app-code sign-in failure.
    /// </summary>
    public class AppCodeBrowserSignInException : InvalidOperationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AppCodeBrowserSignInException" /> class.
        /// </summary>
        public AppCodeBrowserSignInException(AppCodePollStatus status, string message, string? error)
            : base(message)
        {
            Status = status;
            Error = error;
        }

        /// <summary>
        /// Gets the terminal polling status.
        /// </summary>
        public AppCodePollStatus Status { get; }

        /// <summary>
        /// Gets the server error code when one was returned.
        /// </summary>
        public string? Error { get; }
    }
}
