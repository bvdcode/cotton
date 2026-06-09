// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Startup
{
    internal class DesktopStartupOptions
    {
        private DesktopStartupOptions(
            Uri? serverUrl,
            string? username,
            string? dataDirectory,
            bool startMinimizedToTray,
            bool runSelfTest,
            bool exportDiagnostics,
            DesktopVisualSmokeScenario? visualSmokeScenario)
        {
            ServerUrl = serverUrl;
            Username = username;
            DataDirectory = dataDirectory;
            StartMinimizedToTray = startMinimizedToTray;
            RunSelfTest = runSelfTest;
            ExportDiagnostics = exportDiagnostics;
            VisualSmokeScenario = visualSmokeScenario;
        }

        public static DesktopStartupOptions Empty { get; } = new(null, null, null, false, false, false, null);

        public Uri? ServerUrl { get; }

        public string? Username { get; }

        public string? DataDirectory { get; }

        public bool StartMinimizedToTray { get; }

        public bool RunSelfTest { get; }

        public bool ExportDiagnostics { get; }

        public DesktopVisualSmokeScenario? VisualSmokeScenario { get; }

        public static DesktopStartupOptions Parse(IReadOnlyList<string> args)
        {
            ArgumentNullException.ThrowIfNull(args);
            string? serverUrl = ReadOption(args, "--server-url") ?? ReadOption(args, "--server");
            string? username = ReadOption(args, "--username") ?? ReadOption(args, "--user");
            string? dataDirectory = ReadOption(args, "--data-dir") ?? ReadOption(args, "--data-directory");
            string? visualSmokeScenario = ReadOption(args, "--visual-smoke") ?? ReadOption(args, "--screenshot-state");
            bool startMinimizedToTray = HasFlag(args, "--start-minimized")
                || HasFlag(args, "--minimized")
                || HasFlag(args, "--tray");
            bool runSelfTest = HasFlag(args, "--self-test")
                || HasFlag(args, "--smoke-test");
            bool exportDiagnostics = HasFlag(args, "--export-diagnostics")
                || HasFlag(args, "--diagnostics");
            return new DesktopStartupOptions(
                DesktopServerUrl.NormalizeOptional(serverUrl),
                NormalizeOptional(username),
                NormalizeOptional(dataDirectory),
                startMinimizedToTray,
                runSelfTest,
                exportDiagnostics,
                ParseVisualSmokeScenario(visualSmokeScenario));
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
                    return index + 1 < args.Count && !IsOptionName(args[index + 1]) ? args[index + 1] : null;
                }

                string prefix = name + "=";
                if (current.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return current[prefix.Length..];
                }
            }

            return null;
        }

        private static bool IsOptionName(string value)
        {
            return value.StartsWith("--", StringComparison.Ordinal);
        }

        private static string? NormalizeOptional(string? value)
        {
            string? normalized = value?.Trim();
            return string.IsNullOrEmpty(normalized) ? null : normalized;
        }

        private static DesktopVisualSmokeScenario? ParseVisualSmokeScenario(string? value)
        {
            string? normalized = NormalizeOptional(value);
            if (normalized is null)
            {
                return null;
            }

            string enumName = normalized
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace("_", string.Empty, StringComparison.Ordinal);
            return Enum.TryParse(enumName, ignoreCase: true, out DesktopVisualSmokeScenario scenario)
                ? scenario
                : null;
        }
    }
}
