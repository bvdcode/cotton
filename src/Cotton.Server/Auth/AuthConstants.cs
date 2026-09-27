// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Auth
{
    public static class AuthConstants
    {
        public const int WebDavTokenLength = 32;
        public const int RefreshTokenLength = 32;
        public const string AccessTokenCookie = "access_token";
        public const string RefreshTokenCookie = "refresh_token";
    }
}
