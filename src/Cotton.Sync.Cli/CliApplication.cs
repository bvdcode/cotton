// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;
using System.Text;
using Cotton.Contracts.Auth;
using Cotton.Contracts.Nodes;
using Cotton.Sdk;
using Cotton.Sync.ClientState;
using Cotton.Sync.Local;
using Cotton.Sync.Remote;
using Cotton.Sync.State;

namespace Cotton.Sync.Cli;

internal static class CliApplication
{
    private const int DefaultLoopIntervalSeconds = 30;
    private const int RemoteDirectoryPageSize = 100;

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken)
    {
        try
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                WriteHelp(stdout);
                return 0;
            }

            string command = args[0].Trim().ToLowerInvariant();
            return command switch
            {
                "login" => await LoginAsync(OptionReader.Parse(args.Skip(1)), stdout, cancellationToken).ConfigureAwait(false),
                "logout" => await LogoutAsync(OptionReader.Parse(args.Skip(1)), stdout, cancellationToken).ConfigureAwait(false),
                "sync" => await SyncAsync(args.Skip(1).ToArray(), stdout, cancellationToken).ConfigureAwait(false),
                _ => WriteUnknown(command, stderr),
            };
        }
        catch (OperationCanceledException)
        {
            stderr.WriteLine("Canceled.");
            return 130;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or Cotton.Sdk.CottonApiException or HttpRequestException)
        {
            stderr.WriteLine("Error: " + exception.Message);
            return 1;
        }
    }

    private static async Task<int> LoginAsync(OptionReader options, TextWriter stdout, CancellationToken cancellationToken)
    {
        Uri server = ResolveServerOption(options) ?? new Uri("http://localhost:5182");
        string username = options.GetRequired("username");
        string password = options.GetOptional("password") ?? Environment.GetEnvironmentVariable("COTTON_PASSWORD") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Missing required option --password or COTTON_PASSWORD environment variable.");
        }

        SqliteClientStateStore store = CreateCliStore(options);
        using HttpClient httpClient = CreateHttpClient(server);
        var client = new CottonCloudClient(httpClient, store, new CottonSdkOptions { BaseAddress = server });
        await client.Auth.LoginAsync(new LoginRequestDto
        {
            Username = username.Trim(),
            Password = password,
            TwoFactorCode = options.GetOptional("two-factor")?.Trim(),
            TrustDevice = options.HasFlag("trust-device"),
        }, cancellationToken).ConfigureAwait(false);
        await store.SaveServerBaseAddressAsync(server, cancellationToken).ConfigureAwait(false);
        UserDto user = await client.Auth.MeAsync(cancellationToken).ConfigureAwait(false);
        stdout.WriteLine("Logged in as " + user.Username + " on " + server.ToString().TrimEnd('/') + ".");
        return 0;
    }

    private static async Task<int> LogoutAsync(OptionReader options, TextWriter stdout, CancellationToken cancellationToken)
    {
        SqliteClientStateStore store = CreateCliStore(options);
        Uri server = await ResolveServerAsync(options, store, cancellationToken).ConfigureAwait(false);
        using HttpClient httpClient = CreateHttpClient(server);
        var client = new CottonCloudClient(httpClient, store, new CottonSdkOptions { BaseAddress = server });
        await client.Auth.LogoutAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        stdout.WriteLine("Logged out.");
        return 0;
    }

    private static async Task<int> SyncAsync(string[] args, TextWriter stdout, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            WriteSyncHelp(stdout);
            return 0;
        }

        string mode = args[0].Trim().ToLowerInvariant();
        OptionReader options = OptionReader.Parse(args.Skip(1));
        return mode switch
        {
            "once" => await SyncOnceAsync(options, stdout, cancellationToken).ConfigureAwait(false),
            "loop" => await SyncLoopAsync(options, stdout, cancellationToken).ConfigureAwait(false),
            _ => WriteUnknown("sync " + mode, stdout),
        };
    }

    private static async Task<int> SyncOnceAsync(OptionReader options, TextWriter stdout, CancellationToken cancellationToken)
    {
        SyncRunResult result = await RunSyncPassAsync(options, stdout, cancellationToken).ConfigureAwait(false);
        WriteActivities(stdout, result.Activities);
        return 0;
    }

    private static async Task<int> SyncLoopAsync(OptionReader options, TextWriter stdout, CancellationToken cancellationToken)
    {
        int intervalSeconds = options.GetInt32("interval", DefaultLoopIntervalSeconds);
        while (!cancellationToken.IsCancellationRequested)
        {
            SyncRunResult result = await RunSyncPassAsync(options, stdout, cancellationToken).ConfigureAwait(false);
            WriteActivities(stdout, result.Activities);
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken).ConfigureAwait(false);
        }

        return 0;
    }

    private static async Task<SyncRunResult> RunSyncPassAsync(
        OptionReader options,
        TextWriter stdout,
        CancellationToken cancellationToken)
    {
        string localRoot = Path.GetFullPath(options.GetRequired("local"));
        Directory.CreateDirectory(localRoot);
        SqliteClientStateStore cliStore = CreateCliStore(options);
        Uri server = await ResolveServerAsync(options, cliStore, cancellationToken).ConfigureAwait(false);
        using HttpClient httpClient = CreateHttpClient(server);
        var client = new CottonCloudClient(httpClient, cliStore, new CottonSdkOptions { BaseAddress = server });
        string? remotePath = options.GetOptional("remote")?.Trim();
        NodeDto remoteRoot = await EnsureRemoteRootAsync(client.Nodes, remotePath, cancellationToken).ConfigureAwait(false);
        string syncPairId = options.GetOptional("pair")?.Trim() ?? BuildSyncPairId(server, localRoot, remoteRoot.Id);
        string statePath = options.GetOptional("state") ?? Path.Combine(GetConfigDirectory(options), "sync-state.sqlite");
        var engine = new SyncEngine(
            new LocalFileScanner(),
            new RemoteTreeCrawler(client.Nodes),
            new SdkRemoteFileSynchronizer(client),
            new SqliteSyncStateStore(statePath));
        stdout.WriteLine("Syncing " + localRoot + " <-> " + (string.IsNullOrWhiteSpace(remotePath) ? "/" : remotePath) + ".");
        return await engine.RunOnceAsync(new SyncPair
        {
            SyncPairId = syncPairId,
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        }, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static async Task<NodeDto> EnsureRemoteRootAsync(
        Cotton.Sdk.Nodes.ICottonNodeClient nodes,
        string? remotePath,
        CancellationToken cancellationToken)
    {
        NodeDto current = await nodes.ResolveAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(remotePath))
        {
            return current;
        }

        string[] segments = remotePath.Replace('\\', '/').Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (string segment in segments)
        {
            NodeDto? existing = await FindChildDirectoryAsync(nodes, current.Id, segment, cancellationToken).ConfigureAwait(false);
            current = existing ?? await nodes.CreateAsync(current.Id, segment, cancellationToken).ConfigureAwait(false);
        }

        return current;
    }

    private static async Task<NodeDto?> FindChildDirectoryAsync(
        Cotton.Sdk.Nodes.ICottonNodeClient nodes,
        Guid parentNodeId,
        string name,
        CancellationToken cancellationToken)
    {
        int page = 1;
        int loaded = 0;
        while (true)
        {
            Cotton.Contracts.Nodes.NodeContentDto content = await nodes.GetChildrenAsync(
                parentNodeId,
                page,
                RemoteDirectoryPageSize,
                depth: 0,
                cancellationToken).ConfigureAwait(false);
            NodeDto? match = content.Nodes.FirstOrDefault(node => string.Equals(node.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }

            int count = content.Nodes.Count + content.Files.Count;
            loaded += count;
            if (count == 0 || loaded >= content.TotalCount)
            {
                return null;
            }

            page++;
        }
    }

    private static async Task<Uri> ResolveServerAsync(
        OptionReader options,
        SqliteClientStateStore store,
        CancellationToken cancellationToken)
    {
        Uri? server = ResolveServerOption(options) ?? await store.GetServerBaseAddressAsync(cancellationToken).ConfigureAwait(false);
        return server ?? throw new InvalidOperationException("Server URL is not configured. Run login with --server or pass --server to this command.");
    }

    private static Uri? ResolveServerOption(OptionReader options)
    {
        string? value = options.GetOptional("server")?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : new Uri(value, UriKind.Absolute);
    }

    private static SqliteClientStateStore CreateCliStore(OptionReader options)
    {
        return new SqliteClientStateStore(Path.Combine(GetConfigDirectory(options), "client-state.sqlite"));
    }

    private static string GetConfigDirectory(OptionReader options)
    {
        string? configured = options.GetOptional("config");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        return Path.Combine(appData, "Cotton", "SyncClient");
    }

    private static HttpClient CreateHttpClient(Uri server)
    {
        return new HttpClient { BaseAddress = server };
    }

    private static string BuildSyncPairId(Uri server, string localRoot, Guid remoteRootNodeId)
    {
        string identity = server.ToString().TrimEnd('/') + "|" + Path.GetFullPath(localRoot) + "|" + remoteRootNodeId.ToString("D");
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexStringLower(hash);
    }

    private static void WriteActivities(TextWriter stdout, IReadOnlyList<SyncActivity> activities)
    {
        if (activities.Count == 0)
        {
            stdout.WriteLine("Already in sync.");
            return;
        }

        foreach (SyncActivity activity in activities)
        {
            string verb = activity.Kind switch
            {
                SyncActivityKind.Uploaded => "Uploaded",
                SyncActivityKind.Downloaded => "Downloaded",
                SyncActivityKind.DeletedLocal => "Deleted local",
                SyncActivityKind.DeletedRemote => "Deleted remote",
                SyncActivityKind.Conflict => "Conflict",
                _ => activity.Kind.ToString(),
            };
            stdout.WriteLine(string.IsNullOrWhiteSpace(activity.Details)
                ? verb + ": " + activity.RelativePath
                : verb + ": " + activity.RelativePath + " (" + activity.Details + ")");
        }
    }

    private static bool IsHelp(string value)
    {
        return string.Equals(value, "help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase);
    }

    private static int WriteUnknown(string command, TextWriter output)
    {
        output.WriteLine("Unknown command: " + command);
        output.WriteLine("Run `cotton-sync help`.");
        return 1;
    }

    private static void WriteHelp(TextWriter output)
    {
        output.WriteLine("Cotton Sync CLI");
        output.WriteLine();
        output.WriteLine("Commands:");
        output.WriteLine("  login --server URL --username USER --password PASS [--two-factor CODE] [--trust-device]");
        output.WriteLine("  logout [--server URL]");
        output.WriteLine("  sync once --local PATH [--remote PATH] [--server URL] [--pair ID] [--state PATH]");
        output.WriteLine("  sync loop --local PATH [--remote PATH] [--interval SECONDS]");
        output.WriteLine();
        output.WriteLine("Global options:");
        output.WriteLine("  --config PATH    Directory for CLI token/profile SQLite and default sync state SQLite.");
    }

    private static void WriteSyncHelp(TextWriter output)
    {
        output.WriteLine("Cotton Sync CLI sync commands");
        output.WriteLine("  sync once --local PATH [--remote PATH]");
        output.WriteLine("  sync loop --local PATH [--remote PATH] [--interval SECONDS]");
    }
}
