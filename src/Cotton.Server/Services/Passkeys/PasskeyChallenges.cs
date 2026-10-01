// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.WebUtilities;

namespace Cotton.Server.Services.Passkeys
{
    internal static class PasskeyChallenges
    {
        public static readonly TimeSpan OptionsLifetime = TimeSpan.FromMinutes(5);

        public static string CreateRequestId() => WebEncoders.Base64UrlEncode(Guid.NewGuid().ToByteArray());
        public static string RegistrationCacheKey(string requestId) => $"passkey:registration:{requestId}";
        public static string AssertionCacheKey(string requestId) => $"passkey:assertion:{requestId}";
    }
}
