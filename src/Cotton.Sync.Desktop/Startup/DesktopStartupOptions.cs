// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Startup;

internal sealed class DesktopStartupOptions
{
    private DesktopStartupOptions(Uri? serverUrl, string? username, bool startMinimizedToTray)
    {
        ServerUrl = serverUrl;
        Username = username;
        StartMinimizedToTray = startMinimizedToTray;
    }

    public static DesktopStartupOptions Empty { get; } = new(null, null, false);

    public Uri? ServerUrl { get; }

    public string? Username { get; }

    public bool StartMinimizedToTray { get; }

    public static DesktopStartupOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? serverUrl = ReadOption(args, "--server-url") ?? ReadOption(args, "--server");
        string? username = ReadOption(args, "--username") ?? ReadOption(args, "--user");
        bool startMinimizedToTray = HasFlag(args, "--start-minimized")
            || HasFlag(args, "--minimized")
            || HasFlag(args, "--tray");
        return new DesktopStartupOptions(
            ParseServerUrl(serverUrl),
            NormalizeOptional(username),
            startMinimizedToTray);
    }

    private static Uri? ParseServerUrl(string? value)
    {
        string? normalized = NormalizeOptional(value);
        if (normalized is null
            || !Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri)
            || !IsHttpScheme(uri))
        {
            return null;
        }

        return uri;
    }

    private static bool HasFlag(IReadOnlyList<string> args, string name)
    {
        return args.Any(argument => string.Equals(argument, name, StringComparison.Ordinal));
    }

    private static string? ReadOption(IReadOnlyList<string> args, string name)
    {
        for (int index = 0; index < args.Count; index++)
        {
            string current = args[index];
            if (string.Equals(current, name, StringComparison.Ordinal))
            {
                return index + 1 < args.Count ? args[index + 1] : null;
            }

            string prefix = name + "=";
            if (current.StartsWith(prefix, StringComparison.Ordinal))
            {
                return current[prefix.Length..];
            }
        }

        return null;
    }

    private static bool IsHttpScheme(Uri uri)
    {
        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeOptional(string? value)
    {
        string? normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
