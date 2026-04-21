// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Text.RegularExpressions;

namespace Cotton.Validators
{
    public static partial class UsernameValidator
    {
        public const int MinLength = 2;
        public const int MaxLength = 32;

        /// <summary>
        /// Normalizes (trim + lower) and validates username.
        /// Policy:
        /// - only lowercase latin letters, digits, underscores, dots and dashes
        /// - length: 2..32
        /// - must start with a letter
        /// - can use underscores and dots as separators, but not consecutively or at the start/end
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

        public static bool IsValid(string username, out string errorMessage)
            => TryNormalizeAndValidate(username, out _, out errorMessage);

        [GeneratedRegex("^[a-z](?:[a-z0-9]|[._-](?=[a-z0-9])){1,31}$", RegexOptions.CultureInvariant)]
        private static partial Regex UsernameRegex();
    }
}
