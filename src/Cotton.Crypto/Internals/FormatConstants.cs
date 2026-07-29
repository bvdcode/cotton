// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;
namespace Cotton.Crypto.Internals
{
    internal static class FormatConstants
    {
        public const int CurrentVersion = 2;

        public static ReadOnlySpan<byte> MagicBytes => "CTN2"u8;
    }
}
