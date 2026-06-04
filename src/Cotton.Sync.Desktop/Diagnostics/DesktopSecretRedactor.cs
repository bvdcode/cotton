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

        string redacted = AuthorizationHeaderRegex().Replace(value, "$1$2 [redacted]");
        redacted = BearerTokenRegex().Replace(redacted, "Bearer [redacted]");
        redacted = JsonSecretRegex().Replace(redacted, RedactedValue);
        redacted = QuerySecretRegex().Replace(redacted, RedactedValue);
        return redacted;
    }

    [GeneratedRegex(
        @"(Authorization\s*:\s*)(Bearer|Basic)\s+[^\r\n\s]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationHeaderRegex();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(
        """("(?:(?:access|refresh|id)[_-]?token|client[_-]?secret|password|two[_-]?factor[_-]?code|totp[_-]?code|api[_-]?key)"\s*:\s*")([^"]*)(")""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JsonSecretRegex();

    [GeneratedRegex(
        @"((?:(?:access|refresh|id)[_-]?token|client[_-]?secret|password|two[_-]?factor[_-]?code|totp[_-]?code|api[_-]?key)=)([^&\s]+)(&?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QuerySecretRegex();
}
