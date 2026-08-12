// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Passkeys
{
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
