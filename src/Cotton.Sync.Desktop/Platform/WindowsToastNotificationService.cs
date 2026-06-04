// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Cotton.Sync.Desktop.Platform;

internal sealed class WindowsToastNotificationService : IDesktopNotificationService
{
    private const string AppUserModelId = "Cotton.Sync.Desktop";

    private readonly string _powerShellPath;

    public WindowsToastNotificationService(string powerShellPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(powerShellPath);
        _powerShellPath = powerShellPath.Trim();
    }

    public bool IsSupported => true;

    public void Show(string title, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        try
        {
            Process? process = Process.Start(CreateStartInfo(_powerShellPath, title, message));
            process?.Dispose();
        }
        catch (Exception exception) when (IsExpectedNotificationFailure(exception))
        {
            Trace.TraceWarning("Failed to show Windows toast notification: {0}", exception);
        }
    }

    internal static ProcessStartInfo CreateStartInfo(string powerShellPath, string title, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(powerShellPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var startInfo = new ProcessStartInfo
        {
            FileName = powerShellPath,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(EncodePowerShellCommand(CreateToastCommand(title, message)));
        return startInfo;
    }

    internal static string DecodePowerShellCommand(string encodedCommand)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encodedCommand);
        return Encoding.Unicode.GetString(Convert.FromBase64String(encodedCommand));
    }

    private static string CreateToastCommand(string title, string message)
    {
        string titleLiteral = ToPowerShellSingleQuotedLiteral(title);
        string messageLiteral = ToPowerShellSingleQuotedLiteral(message);
        return string.Join(
            Environment.NewLine,
            "$ErrorActionPreference = 'SilentlyContinue'",
            "Add-Type -AssemblyName System.Runtime.WindowsRuntime",
            "[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null",
            "[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] > $null",
            "$template = [Windows.UI.Notifications.ToastTemplateType]::ToastText02",
            "$xml = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent($template)",
            "$textNodes = $xml.GetElementsByTagName('text')",
            $"$null = $textNodes.Item(0).AppendChild($xml.CreateTextNode({titleLiteral}))",
            $"$null = $textNodes.Item(1).AppendChild($xml.CreateTextNode({messageLiteral}))",
            "$toast = [Windows.UI.Notifications.ToastNotification]::new($xml)",
            $"[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('{AppUserModelId}').Show($toast)");
    }

    private static string EncodePowerShellCommand(string command)
    {
        return Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
    }

    private static string ToPowerShellSingleQuotedLiteral(string value)
    {
        return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    }

    private static bool IsExpectedNotificationFailure(Exception exception)
    {
        return exception is Win32Exception
            or FileNotFoundException
            or InvalidOperationException
            or ObjectDisposedException;
    }
}
