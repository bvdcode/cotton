// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Contracts.Auth;
using Cotton.Sdk;
using Cotton.Sdk.Auth;
using Cotton.Sync.Local;
using Cotton.Sync.Remote;
using Cotton.Sync.State;

namespace Cotton.Sync.Cli;

/// <summary>
/// Runs Cotton Sync CLI commands.
/// </summary>
public static class SyncCliCommandRunner
{
    private const string StateSummaryCommand = "state-summary";
    private const string SyncOnceCommand = "sync-once";

    /// <summary>
    /// Runs a CLI command and returns the process exit code.
    /// </summary>
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        return await RunAsync(args, output, error, null, cancellationToken)
            .ConfigureAwait(false);
    }

    internal static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        HttpClient? httpClient,
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

        string command = args[0];
        if (string.Equals(command, StateSummaryCommand, StringComparison.OrdinalIgnoreCase))
        {
            return await RunStateSummaryAsync(args.Skip(1).ToArray(), output, error, cancellationToken)
                .ConfigureAwait(false);
        }

        if (string.Equals(command, SyncOnceCommand, StringComparison.OrdinalIgnoreCase))
        {
            return await RunSyncOnceAsync(args.Skip(1).ToArray(), output, error, httpClient, cancellationToken)
                .ConfigureAwait(false);
        }

        await error.WriteLineAsync("Unknown command: " + command).ConfigureAwait(false);
        await WriteHelpAsync(error).ConfigureAwait(false);
        return 2;
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

    private static async Task<int> RunSyncOnceAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        HttpClient? injectedHttpClient,
        CancellationToken cancellationToken)
    {
        string? server = ReadOption(args, "--server");
        string? username = ReadOption(args, "--username");
        string? password = ReadPassword(args);
        string? localRoot = ReadOption(args, "--local-root");
        string? remoteRoot = ReadOption(args, "--remote-root");
        string? syncPairId = ReadOption(args, "--sync-pair");
        string? databasePath = ReadOption(args, "--database");
        string? twoFactorCode = ReadOption(args, "--two-factor-code");
        if (string.IsNullOrWhiteSpace(server)
            || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(localRoot)
            || string.IsNullOrWhiteSpace(remoteRoot)
            || string.IsNullOrWhiteSpace(syncPairId)
            || string.IsNullOrWhiteSpace(databasePath))
        {
            await error
                .WriteLineAsync(
                    "sync-once requires --server, --username, --password or --password-env, "
                    + "--local-root, --remote-root, --sync-pair, and --database.")
                .ConfigureAwait(false);
            return 2;
        }

        if (!Uri.TryCreate(server, UriKind.Absolute, out Uri? serverUri))
        {
            await error.WriteLineAsync("--server must be an absolute URI.").ConfigureAwait(false);
            return 2;
        }

        if (!Guid.TryParse(remoteRoot, out Guid remoteRootNodeId))
        {
            await error.WriteLineAsync("--remote-root must be a node id GUID.").ConfigureAwait(false);
            return 2;
        }

        using HttpClient? ownedHttpClient = injectedHttpClient is null ? new HttpClient() : null;
        HttpClient httpClient = injectedHttpClient ?? ownedHttpClient!;
        var client = new CottonCloudClient(
            httpClient,
            new InMemoryCottonTokenStore(),
            new CottonSdkOptions
            {
                BaseAddress = serverUri,
                RefreshOnUnauthorized = false,
                UserAgent = "CottonSyncCli/SyncOnce",
                DeviceName = "Cotton Sync CLI",
            });
        await client.Auth.LoginAsync(
            new LoginRequestDto
            {
                Username = username.Trim(),
                Password = password,
                TwoFactorCode = string.IsNullOrWhiteSpace(twoFactorCode) ? null : twoFactorCode.Trim(),
                TrustDevice = true,
            },
            cancellationToken).ConfigureAwait(false);

        var stateStore = new SqliteSyncStateStore(databasePath);
        var engine = new SyncEngine(
            new LocalFileScanner(),
            new RemoteTreeCrawler(client.Nodes),
            new SdkRemoteFileSynchronizer(client),
            stateStore);
        SyncRunResult result = await engine.RunOnceAsync(
            new SyncPair
            {
                SyncPairId = syncPairId.Trim(),
                LocalRootPath = localRoot,
                RemoteRootNodeId = remoteRootNodeId,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SyncStateEntry> entries = await stateStore
            .LoadPairAsync(syncPairId.Trim(), cancellationToken)
            .ConfigureAwait(false);

        await output.WriteLineAsync("Cotton Sync one-shot run").ConfigureAwait(false);
        await output.WriteLineAsync("Sync pair: " + syncPairId.Trim()).ConfigureAwait(false);
        await output.WriteLineAsync("Activities: " + result.Activities.Count.ToStringInvariant()).ConfigureAwait(false);
        foreach (SyncActivity activity in result.Activities)
        {
            await output
                .WriteLineAsync(activity.Kind + " " + activity.RelativePath + FormatActivityDetails(activity.Details))
                .ConfigureAwait(false);
        }

        await output.WriteLineAsync("State entries: " + entries.Count.ToStringInvariant()).ConfigureAwait(false);
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

    private static string? ReadPassword(IReadOnlyList<string> args)
    {
        string? password = ReadOption(args, "--password");
        if (!string.IsNullOrWhiteSpace(password))
        {
            return password;
        }

        string? passwordEnvironmentVariable = ReadOption(args, "--password-env");
        if (string.IsNullOrWhiteSpace(passwordEnvironmentVariable))
        {
            return null;
        }

        return Environment.GetEnvironmentVariable(passwordEnvironmentVariable.Trim());
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
              sync-once --server <url> --username <name>
                  (--password <password> | --password-env <name>) --local-root <path>
                  --remote-root <node-id> --sync-pair <id> --database <path>
                  [--two-factor-code <code>]
                  Signs in and runs one full-mirror sync pass for one pair.
            """);
    }

    private static string FormatActivityDetails(string? details)
    {
        return string.IsNullOrWhiteSpace(details) ? string.Empty : " - " + details;
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
