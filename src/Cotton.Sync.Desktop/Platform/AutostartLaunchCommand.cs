// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Text;

namespace Cotton.Sync.Desktop.Platform
{

    internal sealed class AutostartLaunchCommand
    {
        private const string StartMinimizedArgument = "--start-minimized";
        private const string PublishDirectorySegment = "/publish/";

        public AutostartLaunchCommand(string executablePath, IReadOnlyList<string> arguments)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
            ExecutablePath = executablePath.Trim();
            Arguments = arguments
                .Select(static argument => argument.Trim())
                .Where(static argument => argument.Length > 0)
                .ToArray();
        }

        public string ExecutablePath { get; }

        public IReadOnlyList<string> Arguments { get; }

        public static AutostartLaunchCommand CreateDefault(bool startMinimized)
        {
            string[] commandLineArguments = Environment.GetCommandLineArgs();
            string? processPath = Environment.ProcessPath;
            string[] startupArguments = startMinimized ? [StartMinimizedArgument] : [];
            if (IsDotnetHost(processPath) && IsManagedAssembly(commandLineArguments.FirstOrDefault()))
            {
                return new AutostartLaunchCommand(
                    processPath!,
                    [commandLineArguments[0], .. startupArguments]);
            }

            string executablePath = processPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? commandLineArguments.FirstOrDefault()
                ?? AppContext.BaseDirectory;
            return new AutostartLaunchCommand(executablePath, startupArguments);
        }

        public static AutostartLaunchCommand? TryCreateDefault(bool startMinimized)
        {
            string[] commandLineArguments = Environment.GetCommandLineArgs();
            string? processPath = Environment.ProcessPath;
            return TryCreate(processPath, commandLineArguments, AppContext.BaseDirectory, startMinimized);
        }

        internal static AutostartLaunchCommand? TryCreate(
            string? processPath,
            IReadOnlyList<string> commandLineArguments,
            string baseDirectory,
            bool startMinimized)
        {
            ArgumentNullException.ThrowIfNull(commandLineArguments);
            ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
            string[] startupArguments = startMinimized ? [StartMinimizedArgument] : [];
            if (IsDotnetHost(processPath))
            {
                return null;
            }

            string? executablePath = NormalizeExecutablePath(processPath)
                ?? NormalizeExecutablePath(Process.GetCurrentProcess().MainModule?.FileName)
                ?? NormalizeExecutablePath(commandLineArguments.FirstOrDefault());
            if (executablePath is null || IsDevelopmentBuildOutput(executablePath, baseDirectory))
            {
                return null;
            }

            return new AutostartLaunchCommand(executablePath, startupArguments);
        }

        public override string ToString()
        {
            return string.Join(
                " ",
                new[] { QuoteDesktopEntryArgument(ExecutablePath) }.Concat(Arguments.Select(QuoteDesktopEntryArgument)));
        }

        public string ToWindowsRunCommandLine()
        {
            return string.Join(
                " ",
                new[] { QuoteWindowsCommandLineArgument(ExecutablePath) }.Concat(Arguments.Select(QuoteWindowsCommandLineArgument)));
        }

        private static bool IsDotnetHost(string? processPath)
        {
            return !string.IsNullOrWhiteSpace(processPath)
                && string.Equals(GetPortableFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsManagedAssembly(string? commandLinePath)
        {
            return !string.IsNullOrWhiteSpace(commandLinePath)
                && string.Equals(Path.GetExtension(commandLinePath), ".dll", StringComparison.OrdinalIgnoreCase);
        }

        private static string? NormalizeExecutablePath(string? executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return null;
            }

            string normalized = executablePath.Trim();
            if (string.Equals(Path.GetExtension(normalized), ".dll", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return File.Exists(normalized) || Path.HasExtension(normalized) ? normalized : null;
        }

        private static bool IsDevelopmentBuildOutput(string executablePath, string baseDirectory)
        {
            string normalizedExecutableDirectory = NormalizePathForSegmentChecks(
                GetPortableDirectoryName(executablePath) ?? baseDirectory);
            return IsBuildOutputDirectory(normalizedExecutableDirectory)
                && !normalizedExecutableDirectory.Contains(PublishDirectorySegment, StringComparison.Ordinal);
        }

        private static bool IsBuildOutputDirectory(string path)
        {
            return path.Contains("/bin/debug/", StringComparison.Ordinal)
                || path.Contains("/bin/release/", StringComparison.Ordinal);
        }

        private static string NormalizePathForSegmentChecks(string path)
        {
            return "/" + path.Trim()
                .Replace('\\', '/')
                .Trim('/')
                .ToLowerInvariant()
                + "/";
        }

        private static string GetPortableFileNameWithoutExtension(string path)
        {
            string fileName = GetPortableFileName(path);
            string extension = Path.GetExtension(fileName);
            return extension.Length == 0 ? fileName : fileName[..^extension.Length];
        }

        private static string GetPortableFileName(string path)
        {
            string normalized = path.Replace('\\', '/');
            int separatorIndex = normalized.LastIndexOf('/');
            return separatorIndex < 0 ? normalized : normalized[(separatorIndex + 1)..];
        }

        private static string? GetPortableDirectoryName(string path)
        {
            string normalized = path.Replace('\\', '/');
            int separatorIndex = normalized.LastIndexOf('/');
            return separatorIndex < 0 ? null : normalized[..separatorIndex];
        }

        private static string QuoteDesktopEntryArgument(string value)
        {
            string escaped = value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("$", "\\$", StringComparison.Ordinal)
                .Replace("`", "\\`", StringComparison.Ordinal);
            return escaped.Any(static character => char.IsWhiteSpace(character) || character is '"' or '\\' or '$' or '`')
                ? "\"" + escaped + "\""
                : escaped;
        }

        private static string QuoteWindowsCommandLineArgument(string value)
        {
            if (!value.Any(static character => char.IsWhiteSpace(character) || character is '"'))
            {
                return value;
            }

            var builder = new StringBuilder();
            builder.Append('"');
            var backslashes = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (character == '"')
                {
                    builder.Append('\\', backslashes * 2 + 1);
                    builder.Append('"');
                    backslashes = 0;
                    continue;
                }

                builder.Append('\\', backslashes);
                backslashes = 0;
                builder.Append(character);
            }

            builder.Append('\\', backslashes * 2);
            builder.Append('"');
            return builder.ToString();
        }
    }
}
