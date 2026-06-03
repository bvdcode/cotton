// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Text;

namespace Cotton.Sync.Desktop.Platform;

internal sealed class XdgAutostartService : IAutostartService
{
    private const string DesktopFileName = "cotton-sync.desktop";
    private const string ProductName = "Cotton Sync";

    private readonly string _autostartDirectory;
    private readonly AutostartLaunchCommand _launchCommand;
    private readonly string? _iconPath;

    public XdgAutostartService(
        string autostartDirectory,
        AutostartLaunchCommand launchCommand,
        string? iconPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(autostartDirectory);
        _autostartDirectory = autostartDirectory;
        _launchCommand = launchCommand ?? throw new ArgumentNullException(nameof(launchCommand));
        _iconPath = NormalizeOptional(iconPath);
    }

    public static XdgAutostartService CreateDefault(bool startMinimized)
    {
        string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(configHome))
        {
            configHome = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config");
        }

        return new XdgAutostartService(
            Path.Combine(configHome, "autostart"),
            AutostartLaunchCommand.CreateDefault(startMinimized),
            TryResolveIconPath());
    }

    public bool IsSupported => true;

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string desktopFilePath = DesktopFilePath;
        if (!File.Exists(desktopFilePath))
        {
            return false;
        }

        string content = await File.ReadAllTextAsync(desktopFilePath, cancellationToken).ConfigureAwait(false);
        return content.Contains("Type=Application", StringComparison.Ordinal)
            && content.Contains("Name=" + ProductName, StringComparison.Ordinal)
            && content.Contains("Exec=" + _launchCommand, StringComparison.Ordinal)
            && content.Contains("X-GNOME-Autostart-enabled=true", StringComparison.Ordinal);
    }

    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string desktopFilePath = DesktopFilePath;
        if (!enabled)
        {
            if (File.Exists(desktopFilePath))
            {
                File.Delete(desktopFilePath);
            }

            return;
        }

        Directory.CreateDirectory(_autostartDirectory);
        await File.WriteAllTextAsync(
            desktopFilePath,
            CreateDesktopFile(),
            Encoding.UTF8,
            cancellationToken).ConfigureAwait(false);
    }

    private string DesktopFilePath => Path.Combine(_autostartDirectory, DesktopFileName);

    private string CreateDesktopFile()
    {
        var builder = new StringBuilder();
        builder.AppendLine("[Desktop Entry]");
        builder.AppendLine("Type=Application");
        builder.AppendLine("Version=1.0");
        builder.AppendLine("Name=" + ProductName);
        builder.AppendLine("Comment=Synchronize Cotton Cloud folders");
        builder.AppendLine("Exec=" + _launchCommand);
        if (!string.IsNullOrWhiteSpace(_iconPath))
        {
            builder.AppendLine("Icon=" + EscapeDesktopValue(_iconPath));
        }

        builder.AppendLine("Terminal=false");
        builder.AppendLine("StartupNotify=false");
        builder.AppendLine("X-GNOME-Autostart-enabled=true");
        return builder.ToString();
    }

    private static string? TryResolveIconPath()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon-192.png");
        return File.Exists(iconPath) ? iconPath : null;
    }

    private static string EscapeDesktopValue(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal);
    }

    private static string? NormalizeOptional(string? value)
    {
        string? normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
