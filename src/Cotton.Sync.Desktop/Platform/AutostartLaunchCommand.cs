// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;

namespace Cotton.Sync.Desktop.Platform;

internal sealed class AutostartLaunchCommand
{
    private const string StartMinimizedArgument = "--start-minimized";

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

    public override string ToString()
    {
        return string.Join(
            " ",
            new[] { Quote(ExecutablePath) }.Concat(Arguments.Select(Quote)));
    }

    private static bool IsDotnetHost(string? processPath)
    {
        return !string.IsNullOrWhiteSpace(processPath)
            && string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManagedAssembly(string? commandLinePath)
    {
        return !string.IsNullOrWhiteSpace(commandLinePath)
            && string.Equals(Path.GetExtension(commandLinePath), ".dll", StringComparison.OrdinalIgnoreCase);
    }

    private static string Quote(string value)
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
}
