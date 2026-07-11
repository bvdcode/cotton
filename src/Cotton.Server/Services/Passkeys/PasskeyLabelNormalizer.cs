// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Passkeys
{
    /// <summary>
    /// Normalizes optional user-authored passkey labels consistently across registration and rename flows.
    /// </summary>
    internal static class PasskeyLabelNormalizer
    {
        internal const int MaximumLength = 120;

        internal static string? Normalize(string? label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return null;
            }

            string trimmed = label.Trim();
            return trimmed.Length <= MaximumLength
                ? trimmed
                : trimmed[..MaximumLength];
        }
    }
}
