// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Models.Enums
{
    /// <summary>
    /// Defines the available email template types.
    /// </summary>
    public enum EmailTemplate
    {
        /// <summary>
        /// Email confirmation/verification template.
        /// </summary>
        EmailConfirmation = 1,

        /// <summary>
        /// Password reset template.
        /// </summary>
        PasswordReset = 2,
    }
}
