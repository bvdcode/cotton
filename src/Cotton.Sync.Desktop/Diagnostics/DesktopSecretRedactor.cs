// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Text.RegularExpressions;

namespace Cotton.Sync.Desktop.Diagnostics;

internal static partial class DesktopSecretRedactor
{
    private const string RedactedValue = "$1[redacted]$3";

    public static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        string redacted = BearerTokenRegex().Replace(value, "Bearer [redacted]");
        redacted = JsonSecretRegex().Replace(redacted, RedactedValue);
        redacted = QuerySecretRegex().Replace(redacted, RedactedValue);
        return redacted;
    }

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(
        """("(?:accessToken|refreshToken|password|twoFactorCode|totpCode)"\s*:\s*")([^"]*)(")""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JsonSecretRegex();

    [GeneratedRegex(
        @"((?:access_token|refresh_token|password|two_factor_code|totp_code)=)([^&\s]+)(&?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QuerySecretRegex();
}
