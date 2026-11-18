// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Email
{
    /// <summary>
    /// Describes the representation of email template parameters during transport.
    /// </summary>
    public enum EmailParameterFormat
    {
        /// <summary>
        /// Security message fields contain HTML-encoded text and line breaks.
        /// </summary>
        HtmlEncoded,

        /// <summary>
        /// Template variables contain plain text, encoded only when rendered.
        /// </summary>
        PlainText,
    }
}
