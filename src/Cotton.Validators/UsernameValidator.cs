// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text.RegularExpressions;

namespace Cotton.Validators
{
    /// <summary>
    /// Normalizes and validates account usernames.
    /// </summary>
    public static partial class UsernameValidator
    {
        /// <summary>
        /// Minimum username length.
        /// </summary>
        public const int MinLength = 2;

        /// <summary>
        /// Maximum username length.
        /// </summary>
        public const int MaxLength = 32;

        /// <summary>
        /// Normalizes (trim + lower) and validates username.
        /// Policy:
        /// - only lowercase latin letters, digits, underscores, dots and dashes
        /// - length: 2..32
        /// - must start with a letter
        /// - can use underscores, dots and dashes as separators, but not consecutively or at the start/end
        /// </summary>
        public static bool TryNormalizeAndValidate(
            string input,
            out string normalized,
            out string errorMessage)
        {
            normalized = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                errorMessage = "Username is required.";
                return false;
            }

            string username = input.Trim().ToLowerInvariant();

            if (username.Length < MinLength || username.Length > MaxLength)
            {
                errorMessage = $"Username must be between {MinLength} and {MaxLength} characters.";
                return false;
            }

            if (!UsernameRegex().IsMatch(username))
            {
                errorMessage = "Username must start with a letter and may contain lowercase latin letters and digits, using '_', '.' or '-' only as non-consecutive separators between characters.";
                return false;
            }

            normalized = username;
            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// Validates a username without returning the normalized value.
        /// </summary>
        public static bool IsValid(string username, out string errorMessage)
            => TryNormalizeAndValidate(username, out _, out errorMessage);

        [GeneratedRegex("^[a-z](?:[a-z0-9]|[._-](?=[a-z0-9])){1,31}$", RegexOptions.CultureInvariant)]
        private static partial Regex UsernameRegex();
    }
}
