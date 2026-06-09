// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sdk;
using Cotton.Sdk.Auth;
using Cotton.Sync.App.Auth;
using Cotton.Sync.State;

namespace Cotton.Sync.Cli;

/// <summary>
/// Runs Cotton Sync CLI commands.
/// </summary>
public static class SyncCliCommandRunner
{
    private const string AuthBrowserCommand = "auth-browser";
    private const string StateSummaryCommand = "state-summary";
    private const string SyncOnceCommand = "sync-once";
    private const string SyncSoakCommand = "sync-soak";

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
        if (string.Equals(command, AuthBrowserCommand, StringComparison.OrdinalIgnoreCase))
        {
            return await RunAuthBrowserAsync(args.Skip(1).ToArray(), output, error, httpClient, cancellationToken)
                .ConfigureAwait(false);
        }

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

        if (string.Equals(command, SyncSoakCommand, StringComparison.OrdinalIgnoreCase))
        {
            return await SyncCliSoakCommandRunner
                .RunAsync(args.Skip(1).ToArray(), output, error, httpClient, cancellationToken)
                .ConfigureAwait(false);
        }

        await error.WriteLineAsync("Unknown command: " + command).ConfigureAwait(false);
        await WriteHelpAsync(error).ConfigureAwait(false);
        return 2;
    }

    private static async Task<int> RunAuthBrowserAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        HttpClient? injectedHttpClient,
        CancellationToken cancellationToken)
    {
        SyncCliBrowserAuthOptions? options = SyncCliOptionsReader.ReadBrowserAuthOptions(args, error);
        if (options is null)
        {
            return 2;
        }

        using HttpClient? ownedHttpClient = injectedHttpClient is null ? new HttpClient() : null;
        HttpClient httpClient = injectedHttpClient ?? ownedHttpClient!;
        await using var client = new CottonCloudClient(
            httpClient,
            new InMemoryCottonTokenStore(),
            new CottonSdkOptions
            {
                BaseAddress = options.ServerUri,
                RefreshOnUnauthorized = false,
                UserAgent = "CottonSyncCli",
                DeviceName = options.DeviceName,
            });
        var authFlow = new AppCodeBrowserAuthFlow(
            client.Auth,
            new SyncCliApprovalUrlWriter(output));

        await output.WriteLineAsync("Cotton Sync browser sign-in").ConfigureAwait(false);
        try
        {
            AuthSession session = await authFlow
                .SignInAsync(
                    new AppCodeBrowserSignInRequest
                    {
                        ApplicationName = options.ApplicationName,
                        ApplicationVersion = options.ApplicationVersion,
                        DeviceName = options.DeviceName,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            string account = string.IsNullOrWhiteSpace(session.Email) ? session.Username : session.Email!;
            await output.WriteLineAsync("Signed in: " + account).ConfigureAwait(false);
            await client.Auth.LogoutAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync("Signed out.").ConfigureAwait(false);
            return 0;
        }
        catch (AppCodeBrowserSignInException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(exception.Error))
            {
                await error.WriteLineAsync("Error: " + exception.Error).ConfigureAwait(false);
            }

            return 1;
        }
    }

    private static async Task<int> RunStateSummaryAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        string? databasePath = SyncCliOptionsReader.ReadOption(args, "--database");
        string? syncPairId = SyncCliOptionsReader.ReadOption(args, "--sync-pair");
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
        await output.WriteLineAsync("Cursor updated UTC: " + SyncCliFormat.FormatUtc(cursor.UpdatedAtUtc)).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> RunSyncOnceAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        HttpClient? injectedHttpClient,
        CancellationToken cancellationToken)
    {
        SyncCliConnectionOptions? options = SyncCliOptionsReader.ReadConnectionOptions(
            args,
            error,
            SyncOnceCommand,
            allowBrowserLogin: true);
        if (options is null)
        {
            return 2;
        }

        using HttpClient? ownedHttpClient = injectedHttpClient is null ? new HttpClient() : null;
        HttpClient httpClient = injectedHttpClient ?? ownedHttpClient!;
        SyncCliPassResult pass;
        try
        {
            await using SyncCliRuntime runtime = options.UseBrowserLogin
                ? await SyncCliRuntimeFactory
                    .CreateWithBrowserAuthAsync(
                        options,
                        httpClient,
                        new SyncCliApprovalUrlWriter(output),
                        cancellationToken)
                    .ConfigureAwait(false)
                : await SyncCliRuntimeFactory.CreateAsync(options, httpClient, cancellationToken)
                    .ConfigureAwait(false);
            pass = await SyncCliRuntimeFactory.RunSinglePassAsync(runtime, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AppCodeBrowserSignInException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(exception.Error))
            {
                await error.WriteLineAsync("Error: " + exception.Error).ConfigureAwait(false);
            }

            return 1;
        }

        await output.WriteLineAsync("Cotton Sync one-shot run").ConfigureAwait(false);
        await output.WriteLineAsync("Sync pair: " + options.SyncPairId).ConfigureAwait(false);
        await output.WriteLineAsync("Activities: " + pass.Result.Activities.Count.ToStringInvariant()).ConfigureAwait(false);
        foreach (SyncActivity activity in pass.Result.Activities)
        {
            await output
                .WriteLineAsync(activity.Kind + " " + activity.RelativePath + SyncCliFormat.FormatActivityDetails(activity.Details))
                .ConfigureAwait(false);
        }

        await output.WriteLineAsync("State entries: " + pass.StateEntries.Count.ToStringInvariant()).ConfigureAwait(false);
        return 0;
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
              auth-browser --server <url-or-host>
                  [--application-name <name>] [--application-version <version>]
                  [--device-name <name>]
                  Verifies app-code browser sign-in, then revokes the temporary session.

              state-summary --database <path> --sync-pair <id>
                  Initializes and summarizes a sync-state SQLite database for one sync pair.
              sync-once --server <url-or-host> --username <name>
                  (--password <password> | --password-env <name>) --local-root <path>
                  --remote-root <node-id> --sync-pair <id> --database <path>
                  [--two-factor-code <code>]
              sync-once --server <url-or-host> --browser-login --local-root <path>
                  --remote-root <node-id> --sync-pair <id> --database <path>
                  Signs in and runs one full-mirror sync pass for one pair.
              sync-soak --server <url-or-host> --username <name>
                  (--password <password> | --password-env <name>) --local-root <path>
                  --remote-root <node-id> --sync-pair <id> --database <path>
                  (--iterations <count> | --duration-seconds <seconds>)
                  [--interval-seconds <seconds>] [--probe-file <relative-path>]
                  [--second-local-root <path> --second-sync-pair <id>
                   --second-database <path>]
                  [--two-factor-code <code>]
                  Repeats full-mirror sync passes for one-client or two-client
                  release soak validation.
            """);
    }
}
