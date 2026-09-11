// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.WebDav
{
    public static class WebDavRequestHeaders
    {
        public const string WebDavRoute = "/api/v1/webdav/";
        private static readonly string WebDavPrefix = WebDavRoute.TrimEnd(WebDavPathResolver.PathSeparator);

        public static string? GetLockToken(IHeaderDictionary headers)
        {
            string lockTokenHeader = headers["Lock-Token"].ToString();
            if (!string.IsNullOrWhiteSpace(lockTokenHeader))
            {
                return lockTokenHeader.Trim().Trim('<', '>');
            }

            string ifHeader = headers["If"].ToString();
            int start = ifHeader.IndexOf("<opaquelocktoken:", StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return null;
            }

            int end = ifHeader.IndexOf('>', start);
            return end < 0 ? null : ifHeader[(start + 1)..end];
        }

        public static TimeSpan GetLockTimeout(IHeaderDictionary headers)
        {
            string value = headers["Timeout"].ToString();
            return !string.IsNullOrWhiteSpace(value)
                && value.StartsWith("Second-", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(value["Second-".Length..], out int seconds)
                && seconds > 0
                    ? TimeSpan.FromSeconds(seconds)
                    : TimeSpan.FromHours(1);
        }

        public static int GetDepth(IHeaderDictionary headers)
        {
            string? depth = headers["Depth"].FirstOrDefault()?.Split(',')[0].Trim();
            return depth?.ToLowerInvariant() switch
            {
                "0" => 0,
                "infinity" => 25,
                _ => 1,
            };
        }

        public static string? GetDestinationPath(IHeaderDictionary headers)
        {
            string? destination = headers["Destination"].FirstOrDefault();
            if (string.IsNullOrEmpty(destination))
            {
                return null;
            }

            if (Uri.TryCreate(destination, UriKind.RelativeOrAbsolute, out Uri? uri))
            {
                destination = uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString;
            }

            destination = Uri.UnescapeDataString(destination);
            int prefixIndex = destination.IndexOf(WebDavPrefix, StringComparison.OrdinalIgnoreCase);
            if (prefixIndex >= 0)
            {
                destination = destination[(prefixIndex + WebDavPrefix.Length)..]
                    .TrimStart(WebDavPathResolver.PathSeparator);
            }

            return destination.Trim(WebDavPathResolver.PathSeparator);
        }

        public static bool GetOverwrite(IHeaderDictionary headers)
        {
            string? overwrite = headers["Overwrite"].FirstOrDefault();
            return !string.Equals(overwrite, "F", StringComparison.OrdinalIgnoreCase);
        }
    }
}
