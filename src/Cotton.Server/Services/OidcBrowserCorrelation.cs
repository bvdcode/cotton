// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    internal static class OidcBrowserCorrelation
    {
        private const string CookiePrefix = "__Host-Cotton.Oidc.";
        private const string CookieMarker = "1";

        public static void Create(HttpResponse response, string state, DateTime expiresAt)
        {
            CookieOptions options = CreateCookieOptions();
            options.Expires = expiresAt;
            response.Cookies.Append(GetCookieName(state), CookieMarker, options);
        }

        public static bool ValidateAndClear(HttpContext context, string state)
        {
            string cookieName = GetCookieName(state);
            if (!context.Request.Cookies.TryGetValue(cookieName, out string? value))
            {
                return false;
            }

            context.Response.Cookies.Delete(cookieName, CreateCookieOptions());
            return string.Equals(value, CookieMarker, StringComparison.Ordinal);
        }

        private static string GetCookieName(string state) => CookiePrefix + OidcProtocol.HashOpaqueValue(state);

        private static CookieOptions CreateCookieOptions() => new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            IsEssential = true,
        };
    }
}
