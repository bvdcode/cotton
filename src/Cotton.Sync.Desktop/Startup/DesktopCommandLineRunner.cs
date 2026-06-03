// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Composition;
using Cotton.Sync.Desktop.Diagnostics;
using Cotton.Sync.Desktop.Shell;

namespace Cotton.Sync.Desktop.Startup;

internal static class DesktopCommandLineRunner
{
    public static async Task<int> RunSelfTestAsync(
        DesktopStartupOptions startupOptions,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        return await RunSelfTestAsync(
            DesktopStartupPathResolver.Resolve(startupOptions),
            startupOptions,
            output,
            cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> RunSelfTestAsync(
        DesktopAppPaths paths,
        DesktopStartupOptions startupOptions,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(startupOptions);
        ArgumentNullException.ThrowIfNull(output);

        DesktopTraceLogging.Install(paths);
        using DesktopShellController controller = DesktopShellController.CreateDefault(paths, startupOptions);
        DesktopSelfTestSnapshot result = await controller.RunSelfTestAsync(cancellationToken).ConfigureAwait(false);
        await output.WriteLineAsync("Cotton Sync Desktop self-test").ConfigureAwait(false);
        foreach (DesktopSelfTestItemSnapshot item in result.Items)
        {
            await output.WriteLineAsync(FormatSelfTestItem(item)).ConfigureAwait(false);
        }

        await output.WriteLineAsync(result.Passed ? "Result: passed" : "Result: failed").ConfigureAwait(false);
        return result.Passed ? 0 : 1;
    }

    private static string FormatSelfTestItem(DesktopSelfTestItemSnapshot item)
    {
        string status = item.Passed ? "OK" : "FAIL";
        return "[" + status + "] " + item.Name + " - " + item.Details;
    }

}
