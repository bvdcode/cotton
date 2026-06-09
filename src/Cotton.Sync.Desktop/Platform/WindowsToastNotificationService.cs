// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Cotton.Sync.Desktop.Platform
{
    internal class WindowsToastNotificationService : IDesktopNotificationService
    {
        private readonly string _powerShellPath;
        private readonly string? _iconPath;

        public WindowsToastNotificationService(string powerShellPath, string? iconPath = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(powerShellPath);
            _powerShellPath = powerShellPath.Trim();
            _iconPath = string.IsNullOrWhiteSpace(iconPath) ? null : iconPath.Trim();
        }

        public bool IsSupported => true;

        public void Show(string title, string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            try
            {
                Process? process = Process.Start(CreateStartInfo(_powerShellPath, title, message, _iconPath));
                process?.Dispose();
            }
            catch (Exception exception) when (IsExpectedNotificationFailure(exception))
            {
                Trace.TraceWarning("Failed to show Windows toast notification: {0}", exception);
            }
        }

        internal static ProcessStartInfo CreateStartInfo(string powerShellPath, string title, string message)
        {
            return CreateStartInfo(powerShellPath, title, message, iconPath: null);
        }

        internal static ProcessStartInfo CreateStartInfo(
            string powerShellPath,
            string title,
            string message,
            string? iconPath)
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
            startInfo.ArgumentList.Add(EncodePowerShellCommand(CreateToastCommand(title, message, ToFileUri(iconPath))));
            return startInfo;
        }

        internal static string DecodePowerShellCommand(string encodedCommand)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(encodedCommand);
            return Encoding.Unicode.GetString(Convert.FromBase64String(encodedCommand));
        }

        private static string CreateToastCommand(string title, string message, string? iconUri)
        {
            string titleLiteral = ToPowerShellSingleQuotedLiteral(title);
            string messageLiteral = ToPowerShellSingleQuotedLiteral(message);
            List<string> lines =
            [
                "$ErrorActionPreference = 'SilentlyContinue'",
                "Add-Type -AssemblyName System.Runtime.WindowsRuntime",
                "[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null",
                "[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] > $null",
                "$xml = [Windows.Data.Xml.Dom.XmlDocument]::new()",
                "$toastNode = $xml.CreateElement('toast')",
                "$visualNode = $xml.CreateElement('visual')",
                "$bindingNode = $xml.CreateElement('binding')",
                "$bindingNode.SetAttribute('template', 'ToastGeneric')"
            ];
            if (!string.IsNullOrWhiteSpace(iconUri))
            {
                string iconUriLiteral = ToPowerShellSingleQuotedLiteral(iconUri);
                lines.AddRange(
                [
                    "$bindingNode = $xml.GetElementsByTagName('binding').Item(0)",
                    "$imageNode = $xml.CreateElement('image')",
                    "$imageNode.SetAttribute('placement', 'appLogoOverride')",
                    $"$imageNode.SetAttribute('src', {iconUriLiteral})",
                    "$null = $bindingNode.AppendChild($imageNode)"
                ]);
            }

            lines.AddRange(
            [
                "$titleNode = $xml.CreateElement('text')",
                $"$null = $titleNode.AppendChild($xml.CreateTextNode({titleLiteral}))",
                "$messageNode = $xml.CreateElement('text')",
                $"$null = $messageNode.AppendChild($xml.CreateTextNode({messageLiteral}))",
                "$null = $bindingNode.AppendChild($titleNode)",
                "$null = $bindingNode.AppendChild($messageNode)",
                "$null = $visualNode.AppendChild($bindingNode)",
                "$null = $toastNode.AppendChild($visualNode)",
                "$null = $xml.AppendChild($toastNode)",
                "$toast = [Windows.UI.Notifications.ToastNotification]::new($xml)",
                $"[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('{DesktopAppIdentity.AppUserModelId}').Show($toast)"
            ]);
            return string.Join(Environment.NewLine, lines);
        }

        private static string EncodePowerShellCommand(string command)
        {
            return Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
        }

        private static string ToPowerShellSingleQuotedLiteral(string value)
        {
            return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
        }

        private static string? ToFileUri(string? iconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPath))
            {
                return null;
            }

            return new Uri(Path.GetFullPath(iconPath.Trim())).AbsoluteUri;
        }

        private static bool IsExpectedNotificationFailure(Exception exception)
        {
            return exception is Win32Exception
                or FileNotFoundException
                or InvalidOperationException
                or ObjectDisposedException;
        }
    }
}
