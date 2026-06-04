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
            return await RunSyncSoakAsync(args.Skip(1).ToArray(), output, error, httpClient, cancellationToken)
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
        SyncCliConnectionOptions? options = ReadConnectionOptions(args, error, SyncOnceCommand);
        if (options is null)
        {
            return 2;
        }

        using HttpClient? ownedHttpClient = injectedHttpClient is null ? new HttpClient() : null;
        HttpClient httpClient = injectedHttpClient ?? ownedHttpClient!;
        SyncCliRuntime runtime = await CreateRuntimeAsync(options, httpClient, cancellationToken).ConfigureAwait(false);
        SyncCliPassResult pass = await RunSingleSyncPassAsync(runtime, cancellationToken).ConfigureAwait(false);

        await output.WriteLineAsync("Cotton Sync one-shot run").ConfigureAwait(false);
        await output.WriteLineAsync("Sync pair: " + options.SyncPairId).ConfigureAwait(false);
        await output.WriteLineAsync("Activities: " + pass.Result.Activities.Count.ToStringInvariant()).ConfigureAwait(false);
        foreach (SyncActivity activity in pass.Result.Activities)
        {
            await output
                .WriteLineAsync(activity.Kind + " " + activity.RelativePath + FormatActivityDetails(activity.Details))
                .ConfigureAwait(false);
        }

        await output.WriteLineAsync("State entries: " + pass.StateEntries.Count.ToStringInvariant()).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> RunSyncSoakAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        HttpClient? injectedHttpClient,
        CancellationToken cancellationToken)
    {
        SyncCliConnectionOptions? options = ReadConnectionOptions(args, error, SyncSoakCommand);
        if (options is null)
        {
            return 2;
        }

        if (!TryReadOptionalPositiveInt(args, "--iterations", error, out int? iterations))
        {
            return 2;
        }

        if (!TryReadOptionalPositiveInt(args, "--duration-seconds", error, out int? durationSeconds))
        {
            return 2;
        }

        if (!TryReadOptionalPositiveInt(args, "--interval-seconds", error, out int? intervalSeconds))
        {
            return 2;
        }

        if (!iterations.HasValue && !durationSeconds.HasValue)
        {
            await error.WriteLineAsync("sync-soak requires --iterations or --duration-seconds.").ConfigureAwait(false);
            return 2;
        }

        string? probeFile = ReadOption(args, "--probe-file");
        string? normalizedProbeFile = null;
        if (!string.IsNullOrWhiteSpace(probeFile)
            && !TryNormalizeProbeFile(options.LocalRoot, probeFile, out normalizedProbeFile, out string probeError))
        {
            await error.WriteLineAsync(probeError).ConfigureAwait(false);
            return 2;
        }

        using HttpClient? ownedHttpClient = injectedHttpClient is null ? new HttpClient() : null;
        HttpClient httpClient = injectedHttpClient ?? ownedHttpClient!;
        SyncCliRuntime runtime = await CreateRuntimeAsync(options, httpClient, cancellationToken).ConfigureAwait(false);
        DateTime startedAtUtc = DateTime.UtcNow;
        DateTime? stopAtUtc = durationSeconds.HasValue
            ? startedAtUtc.AddSeconds(durationSeconds.Value)
            : null;
        int completedIterations = 0;
        int totalActivities = 0;
        await output.WriteLineAsync("Cotton Sync soak run").ConfigureAwait(false);
        await output.WriteLineAsync("Sync pair: " + options.SyncPairId).ConfigureAwait(false);
        await output.WriteLineAsync("Started UTC: " + FormatUtc(startedAtUtc)).ConfigureAwait(false);

        while (ShouldRunNextSoakIteration(completedIterations, iterations, stopAtUtc))
        {
            cancellationToken.ThrowIfCancellationRequested();
            int iteration = completedIterations + 1;
            if (normalizedProbeFile is not null)
            {
                await WriteProbeFileAsync(options.LocalRoot, normalizedProbeFile, iteration, cancellationToken).ConfigureAwait(false);
            }

            SyncCliPassResult pass = await RunSingleSyncPassAsync(runtime, cancellationToken).ConfigureAwait(false);
            completedIterations++;
            totalActivities += pass.Result.Activities.Count;
            await output
                .WriteLineAsync(
                    "Iteration " + iteration.ToStringInvariant()
                    + ": activities=" + pass.Result.Activities.Count.ToStringInvariant()
                    + ", stateEntries=" + pass.StateEntries.Count.ToStringInvariant())
                .ConfigureAwait(false);

            if (!ShouldRunNextSoakIteration(completedIterations, iterations, stopAtUtc))
            {
                break;
            }

            await Task.Delay(
                GetNextSoakDelay(intervalSeconds ?? 30, stopAtUtc),
                cancellationToken).ConfigureAwait(false);
        }

        DateTime completedAtUtc = DateTime.UtcNow;
        await output.WriteLineAsync("Completed UTC: " + FormatUtc(completedAtUtc)).ConfigureAwait(false);
        await output.WriteLineAsync("Iterations completed: " + completedIterations.ToStringInvariant()).ConfigureAwait(false);
        await output.WriteLineAsync("Total activities: " + totalActivities.ToStringInvariant()).ConfigureAwait(false);
        await output.WriteLineAsync("Failures: 0").ConfigureAwait(false);
        return 0;
    }

    private static SyncCliConnectionOptions? ReadConnectionOptions(
        IReadOnlyList<string> args,
        TextWriter error,
        string command)
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
            error.WriteLine(
                command + " requires --server, --username, --password or --password-env, "
                + "--local-root, --remote-root, --sync-pair, and --database.");
            return null;
        }

        Uri? serverUri = CottonServerUrl.NormalizeOptional(server);
        if (serverUri is null)
        {
            error.WriteLine("--server must be an HTTP or HTTPS URL.");
            return null;
        }

        if (!Guid.TryParse(remoteRoot, out Guid remoteRootNodeId))
        {
            error.WriteLine("--remote-root must be a node id GUID.");
            return null;
        }

        return new SyncCliConnectionOptions(
            serverUri,
            username.Trim(),
            password,
            localRoot,
            remoteRootNodeId,
            syncPairId.Trim(),
            databasePath,
            string.IsNullOrWhiteSpace(twoFactorCode) ? null : twoFactorCode.Trim());
    }

    private static async Task<SyncCliRuntime> CreateRuntimeAsync(
        SyncCliConnectionOptions options,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        var client = new CottonCloudClient(
            httpClient,
            new InMemoryCottonTokenStore(),
            new CottonSdkOptions
            {
                BaseAddress = options.ServerUri,
                RefreshOnUnauthorized = false,
                UserAgent = "CottonSyncCli",
                DeviceName = "Cotton Sync CLI",
            });
        await client.Auth.LoginAsync(
            new LoginRequestDto
            {
                Username = options.Username,
                Password = options.Password,
                TwoFactorCode = options.TwoFactorCode,
                TrustDevice = true,
            },
            cancellationToken).ConfigureAwait(false);

        var stateStore = new SqliteSyncStateStore(options.DatabasePath);
        var engine = new SyncEngine(
            new LocalFileScanner(),
            new RemoteTreeCrawler(client.Nodes),
            new SdkRemoteFileSynchronizer(client),
            stateStore,
            remoteDirectories: new SdkRemoteDirectorySynchronizer(client.Nodes));
        var syncPair = new SyncPair
        {
            SyncPairId = options.SyncPairId,
            LocalRootPath = options.LocalRoot,
            RemoteRootNodeId = options.RemoteRootNodeId,
        };
        return new SyncCliRuntime(syncPair, stateStore, engine);
    }

    private static async Task<SyncCliPassResult> RunSingleSyncPassAsync(
        SyncCliRuntime runtime,
        CancellationToken cancellationToken)
    {
        SyncRunResult result = await runtime.Engine
            .RunOnceAsync(runtime.SyncPair, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<SyncStateEntry> entries = await runtime.StateStore
            .LoadPairAsync(runtime.SyncPair.SyncPairId, cancellationToken)
            .ConfigureAwait(false);
        return new SyncCliPassResult(result, entries);
    }

    private static bool ShouldRunNextSoakIteration(
        int completedIterations,
        int? maxIterations,
        DateTime? stopAtUtc)
    {
        if (maxIterations.HasValue && completedIterations >= maxIterations.Value)
        {
            return false;
        }

        return !stopAtUtc.HasValue || DateTime.UtcNow < stopAtUtc.Value || completedIterations == 0;
    }

    private static TimeSpan GetNextSoakDelay(int intervalSeconds, DateTime? stopAtUtc)
    {
        TimeSpan interval = TimeSpan.FromSeconds(intervalSeconds);
        if (!stopAtUtc.HasValue)
        {
            return interval;
        }

        TimeSpan remaining = stopAtUtc.Value - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return remaining >= interval ? interval : remaining;
    }

    private static async Task WriteProbeFileAsync(
        string localRoot,
        string relativePath,
        int iteration,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.Combine(localRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string content = "Cotton Sync soak probe" + Environment.NewLine
            + "Iteration: " + iteration.ToStringInvariant() + Environment.NewLine
            + "UTC: " + FormatUtc(DateTime.UtcNow) + Environment.NewLine;
        await File.WriteAllTextAsync(fullPath, content, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryReadOptionalPositiveInt(
        IReadOnlyList<string> args,
        string name,
        TextWriter error,
        out int? value)
    {
        value = null;
        string? rawValue = ReadOption(args, name);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return true;
        }

        if (!int.TryParse(
                rawValue.Trim(),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out int parsedValue)
            || parsedValue <= 0)
        {
            error.WriteLine(name + " must be a positive integer.");
            return false;
        }

        value = parsedValue;
        return true;
    }

    private static bool TryNormalizeProbeFile(
        string localRoot,
        string probeFile,
        out string normalizedProbeFile,
        out string error)
    {
        normalizedProbeFile = string.Empty;
        error = string.Empty;
        if (Path.IsPathRooted(probeFile))
        {
            error = "--probe-file must be a relative path inside --local-root.";
            return false;
        }

        try
        {
            normalizedProbeFile = SyncPath.Normalize(probeFile);
        }
        catch (ArgumentException exception)
        {
            error = "--probe-file is invalid: " + exception.Message;
            return false;
        }

        string root = Path.GetFullPath(localRoot);
        string fullPath = Path.GetFullPath(Path.Combine(root, normalizedProbeFile.Replace('/', Path.DirectorySeparatorChar)));
        string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal)
            && !string.Equals(fullPath, root, StringComparison.Ordinal))
        {
            error = "--probe-file must stay inside --local-root.";
            return false;
        }

        return true;
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
              sync-once --server <url-or-host> --username <name>
                  (--password <password> | --password-env <name>) --local-root <path>
                  --remote-root <node-id> --sync-pair <id> --database <path>
                  [--two-factor-code <code>]
                  Signs in and runs one full-mirror sync pass for one pair.
              sync-soak --server <url-or-host> --username <name>
                  (--password <password> | --password-env <name>) --local-root <path>
                  --remote-root <node-id> --sync-pair <id> --database <path>
                  (--iterations <count> | --duration-seconds <seconds>)
                  [--interval-seconds <seconds>] [--probe-file <relative-path>]
                  [--two-factor-code <code>]
                  Repeats full-mirror sync passes for release soak validation.
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

    private sealed record SyncCliConnectionOptions(
        Uri ServerUri,
        string Username,
        string Password,
        string LocalRoot,
        Guid RemoteRootNodeId,
        string SyncPairId,
        string DatabasePath,
        string? TwoFactorCode);

    private sealed record SyncCliRuntime(
        SyncPair SyncPair,
        SqliteSyncStateStore StateStore,
        SyncEngine Engine);

    private sealed record SyncCliPassResult(
        SyncRunResult Result,
        IReadOnlyList<SyncStateEntry> StateEntries);
}
