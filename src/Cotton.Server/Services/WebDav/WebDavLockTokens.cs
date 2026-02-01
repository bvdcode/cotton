// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.WebDav;

public static class WebDavLockTokens
{
    public static string? NormalizeToken(string? tokenHeader)
    {
        if (string.IsNullOrWhiteSpace(tokenHeader))
        {
            return null;
        }

        var t = tokenHeader.Trim();
        if (t.StartsWith('<') && t.EndsWith('>') && t.Length > 2)
        {
            t = t[1..^1];
        }

        return string.IsNullOrWhiteSpace(t) ? null : t;
    }

    public static string? TryExtractToken(string? ifHeader)
    {
        if (string.IsNullOrWhiteSpace(ifHeader))
        {
            return null;
        }

        // Minimal parser: look for first opaquelocktoken:... inside <...>
        var idx = ifHeader.IndexOf("opaquelocktoken:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        var start = ifHeader.LastIndexOf('<', idx);
        if (start < 0)
        {
            start = idx;
        }
        else
        {
            start++;
        }

        var end = ifHeader.IndexOf('>', idx);
        if (end < 0)
        {
            end = ifHeader.Length;
        }

        var token = ifHeader[start..end].Trim();
        return NormalizeToken(token);
    }
}
