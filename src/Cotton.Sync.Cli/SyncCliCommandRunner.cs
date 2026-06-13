// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sdk;
using Cotton.Sdk.Auth;
using Cotton.Sync.App.Auth;
using Cotton.Sync.State;
using System.Net;

namespace Cotton.Sync.Cli
{
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
            if (IsVersion(command))
            {
                await output.WriteLineAsync(SyncCliAppVersion.Current).ConfigureAwait(false);
                return 0;
            }

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
            using CancellationTokenSource? timeoutCancellation = options.TimeoutSeconds.HasValue
                ? new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds.Value))
                : null;
            using CancellationTokenSource? linkedCancellation = timeoutCancellation is null
                ? null
                : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token);
            CancellationToken signInCancellation = linkedCancellation?.Token ?? cancellationToken;
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
                        signInCancellation)
                    .ConfigureAwait(false);
                string account = string.IsNullOrWhiteSpace(session.Email) ? session.Username : session.Email!;
                await output.WriteLineAsync("Signed in: " + account).ConfigureAwait(false);
                await client.Auth.LogoutAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                await output.WriteLineAsync("Signed out.").ConfigureAwait(false);
                return 0;
            }
            catch (OperationCanceledException) when (timeoutCancellation?.IsCancellationRequested == true
                && !cancellationToken.IsCancellationRequested)
            {
                await error.WriteLineAsync("Browser sign-in timed out before approval completed.").ConfigureAwait(false);
                return 1;
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

            IReadOnlyList<SyncStateEntry> entries;
            SyncChangeCursor cursor;
            try
            {
                var store = new SqliteSyncStateStore(databasePath);
                await store.InitializeAsync(cancellationToken).ConfigureAwait(false);
                entries = await store
                    .LoadPairAsync(syncPairId, cancellationToken)
                    .ConfigureAwait(false);
                cursor = await store
                    .GetChangeCursorAsync(syncPairId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (IsStateDatabaseReadException(exception))
            {
                await error
                    .WriteLineAsync(
                        "state-summary could not read the sync-state database. The file may be corrupt or not a Cotton Sync state database: "
                        + exception.Message)
                    .ConfigureAwait(false);
                return 2;
            }

            await output.WriteLineAsync("Cotton Sync state summary").ConfigureAwait(false);
            await output.WriteLineAsync("Database: " + databasePath).ConfigureAwait(false);
            await output.WriteLineAsync("Sync pair: " + syncPairId).ConfigureAwait(false);
            await output.WriteLineAsync("Entries: " + entries.Count.ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("Remote cursor: " + cursor.LastCursor.ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("Cursor updated UTC: " + SyncCliFormat.FormatUtc(cursor.UpdatedAtUtc)).ConfigureAwait(false);
            return 0;
        }

        private static bool IsStateDatabaseReadException(Exception exception)
        {
            if (exception is IOException or UnauthorizedAccessException)
            {
                return true;
            }

            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                string? typeName = current.GetType().FullName;
                if (string.Equals(typeName, "Microsoft.Data.Sqlite.SqliteException", StringComparison.Ordinal)
                    || string.Equals(typeName, "Microsoft.EntityFrameworkCore.DbUpdateException", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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
                if (options.UseBrowserLogin)
                {
                    await output.WriteLineAsync("Browser approval completed. Starting sync...").ConfigureAwait(false);
                }

                pass = await SyncCliRuntimeFactory
                    .RunSinglePassAsync(
                        runtime,
                        new SyncRunOptions { RunProgress = new SyncCliRunProgressWriter(output) },
                        cancellationToken)
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
            catch (Exception exception) when (IsSupportableSyncOnceException(exception))
            {
                await WriteSyncOnceFailureAsync(error, options, exception).ConfigureAwait(false);
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

        private static bool IsSupportableSyncOnceException(Exception exception)
        {
            return exception is CottonApiException
                or HttpRequestException
                or IOException
                or TimeoutException
                or TaskCanceledException
                or UnauthorizedAccessException
                or SyncPathValidationException;
        }

        private static async Task WriteSyncOnceFailureAsync(
            TextWriter error,
            SyncCliConnectionOptions options,
            Exception exception)
        {
            await error.WriteLineAsync("sync-once failed.").ConfigureAwait(false);
            await error.WriteLineAsync("Server: " + options.ServerUri).ConfigureAwait(false);
            await error.WriteLineAsync("Local root: " + options.LocalRoot).ConfigureAwait(false);
            await error.WriteLineAsync("Remote root: " + options.RemoteRootNodeId).ConfigureAwait(false);
            await error.WriteLineAsync("Sync pair: " + options.SyncPairId).ConfigureAwait(false);
            await error.WriteLineAsync("Database: " + options.DatabasePath).ConfigureAwait(false);
            await error.WriteLineAsync("Error: " + FormatSyncOnceFailure(exception)).ConfigureAwait(false);
        }

        private static string FormatSyncOnceFailure(Exception exception)
        {
            if (exception is CottonApiException apiException)
            {
                HttpStatusCode apiStatusCode = apiException.StatusCode.GetValueOrDefault();
                return "Cotton API returned "
                    + ((int)apiStatusCode).ToStringInvariant()
                    + " "
                    + apiStatusCode
                    + ". "
                    + CleanSingleLine(apiException.Message);
            }

            if (exception is HttpRequestException httpException && httpException.StatusCode is HttpStatusCode statusCode)
            {
                return "HTTP request failed with "
                    + ((int)statusCode).ToStringInvariant()
                    + " "
                    + statusCode
                    + ". "
                    + CleanSingleLine(httpException.Message);
            }

            return CleanSingleLine(exception.Message);
        }

        private static string CleanSingleLine(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "Operation could not be completed.";
            }

            return message
                .Replace(Environment.NewLine, " ", StringComparison.Ordinal)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
        }

        private static bool IsHelp(string value)
        {
            return string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "help", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsVersion(string value)
        {
            return string.Equals(value, "--version", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "-v", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "version", StringComparison.OrdinalIgnoreCase);
        }

        private static Task WriteHelpAsync(TextWriter writer)
        {
            return writer.WriteLineAsync(
                """
                Cotton Sync CLI

                Commands:
                  auth-browser --server <url-or-host>
                      [--application-name <name>] [--application-version <version>]
                      [--device-name <name>] [--timeout-seconds <seconds>]
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
                  sync-soak --server <url-or-host> --browser-login --local-root <path>
                      --remote-root <node-id> --sync-pair <id> --database <path>
                      (--iterations <count> | --duration-seconds <seconds>)
                      [--interval-seconds <seconds>] [--probe-file <relative-path>]
                      [--second-local-root <path> --second-sync-pair <id>
                       --second-database <path>]
                      Repeats full-mirror sync passes for one-client or two-client
                      release soak validation.
                """);
        }
    }
}
