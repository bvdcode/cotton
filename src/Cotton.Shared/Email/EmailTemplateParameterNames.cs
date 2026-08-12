// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Email
{
    /// <summary>
    /// Defines parameter names used by Cotton email templates.
    /// </summary>
    public static class EmailTemplateParameterNames
    {
        /// <summary>
        /// Recipient display name.
        /// </summary>
        public const string RecipientName = "recipient_name";

        /// <summary>
        /// Recipient email address.
        /// </summary>
        public const string RecipientEmail = "recipient_email";

        /// <summary>
        /// Email confirmation or password reset token.
        /// </summary>
        public const string Token = "token";

        /// <summary>
        /// Email confirmation URL.
        /// </summary>
        public const string ConfirmationUrl = "confirmation_url";

        /// <summary>
        /// Password reset URL.
        /// </summary>
        public const string ResetUrl = "reset_url";

        /// <summary>
        /// Security alert title.
        /// </summary>
        public const string SecurityTitle = "security_title";

        /// <summary>
        /// Security alert content.
        /// </summary>
        public const string SecurityContent = "security_content";

        /// <summary>
        /// UTC timestamp when the security event occurred.
        /// </summary>
        public const string OccurredAt = "occurred_at";

        /// <summary>
        /// Verified Cotton server URL.
        /// </summary>
        public const string ServerUrl = "server_url";

        /// <summary>
        /// Current year used in email footers.
        /// </summary>
        public const string Year = "year";
    }
}
