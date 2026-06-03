// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.State;

namespace Cotton.Sync.Cli;

/// <summary>
/// Runs Cotton Sync CLI commands.
/// </summary>
public static class SyncCliCommandRunner
{
    private const string StateSummaryCommand = "state-summary";

    /// <summary>
    /// Runs a CLI command and returns the process exit code.
    /// </summary>
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Count == 0 || IsHelp(args[0]))
        {
            await WriteHelpAsync(output).ConfigureAwait(false);
            return 0;
        }

        if (!string.Equals(args[0], StateSummaryCommand, StringComparison.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync("Unknown command: " + args[0]).ConfigureAwait(false);
            await WriteHelpAsync(error).ConfigureAwait(false);
            return 2;
        }

        return await RunStateSummaryAsync(args.Skip(1).ToArray(), output, error, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<int> RunStateSummaryAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        string? databasePath = ReadOption(args, "--database");
        string? syncPairId = ReadOption(args, "--sync-pair");
        if (string.IsNullOrWhiteSpace(databasePath) || string.IsNullOrWhiteSpace(syncPairId))
        {
            await error.WriteLineAsync("state-summary requires --database and --sync-pair.").ConfigureAwait(false);
            return 2;
        }

        var store = new SqliteSyncStateStore(databasePath);
        await store.InitializeAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SyncStateEntry> entries = await store
            .LoadPairAsync(syncPairId, cancellationToken)
            .ConfigureAwait(false);
        SyncChangeCursor cursor = await store
            .GetChangeCursorAsync(syncPairId, cancellationToken)
            .ConfigureAwait(false);

        await output.WriteLineAsync("Cotton Sync state summary").ConfigureAwait(false);
        await output.WriteLineAsync("Database: " + databasePath).ConfigureAwait(false);
        await output.WriteLineAsync("Sync pair: " + syncPairId).ConfigureAwait(false);
        await output.WriteLineAsync("Entries: " + entries.Count.ToStringInvariant()).ConfigureAwait(false);
        await output.WriteLineAsync("Remote cursor: " + cursor.LastCursor.ToStringInvariant()).ConfigureAwait(false);
        await output.WriteLineAsync("Cursor updated UTC: " + FormatUtc(cursor.UpdatedAtUtc)).ConfigureAwait(false);
        return 0;
    }

    private static string? ReadOption(IReadOnlyList<string> args, string name)
    {
        for (int index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static bool IsHelp(string value)
    {
        return string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "help", StringComparison.OrdinalIgnoreCase);
    }

    private static Task WriteHelpAsync(TextWriter writer)
    {
        return writer.WriteLineAsync(
            """
            Cotton Sync CLI

            Commands:
              state-summary --database <path> --sync-pair <id>
                  Initializes and summarizes a sync-state SQLite database for one sync pair.
            """);
    }

    private static string FormatUtc(DateTime value)
    {
        return DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ToStringInvariant(this int value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ToStringInvariant(this long value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
