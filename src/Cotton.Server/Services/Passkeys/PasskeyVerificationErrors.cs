// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Formats.Asn1;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text.Json;
using EasyExtensions.Passkeys;

namespace Cotton.Server.Services.Passkeys
{
    internal static class PasskeyVerificationErrors
    {
        public static bool IsInvalidResponse(Exception error)
        {
            return error is PasskeyVerificationException or FormatException or JsonException
                or CborContentException or AsnContentException or CryptographicException
                or ArgumentException or InvalidOperationException or OverflowException;
        }
    }
}
