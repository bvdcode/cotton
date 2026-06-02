// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Cotton.Contracts.Auth;
using Cotton.Contracts.Nodes;
using Cotton.Sdk;
using Cotton.Sync.ClientState;
using Cotton.Sync.Desktop.ViewModels;
using Cotton.Sync.Local;
using Cotton.Sync.Remote;
using Cotton.Sync.State;

namespace Cotton.Sync.Desktop;

/// <summary>
/// Main desktop synchronization window.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int DefaultIntervalSeconds = 30;
    private CancellationTokenSource? _loopCancellation;
    private Task? _loopTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow" /> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        LoadDefaults();
    }

    /// <summary>
    /// Gets activity rows shown in the synchronization log.
    /// </summary>
    public ObservableCollection<ActivityRow> Activities { get; } = [];

    private async void LoginButton_Click(object? sender, RoutedEventArgs args)
    {
        await RunUiActionAsync("Logging in", LoginAsync).ConfigureAwait(true);
    }

    private async void SyncOnceButton_Click(object? sender, RoutedEventArgs args)
    {
        await RunUiActionAsync("Syncing", async token =>
        {
            SyncRunResult result = await RunSyncPassAsync(token).ConfigureAwait(true);
            AddResultActivities(result);
        }).ConfigureAwait(true);
    }

    private void StartLoopButton_Click(object? sender, RoutedEventArgs args)
    {
        if (_loopCancellation is not null)
        {
            return;
        }

        _loopCancellation = new CancellationTokenSource();
        StartLoopButton.IsEnabled = false;
        StopLoopButton.IsEnabled = true;
        SetStatus("Sync loop running");
        _loopTask = RunLoopAsync(_loopCancellation.Token);
    }

    private async void StopLoopButton_Click(object? sender, RoutedEventArgs args)
    {
        CancellationTokenSource? cancellation = _loopCancellation;
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        try
        {
            if (_loopTask is not null)
            {
                await _loopTask.ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            AddActivity("Info", string.Empty, "Sync loop stopped.");
        }
        finally
        {
            cancellation.Dispose();
            _loopCancellation = null;
            _loopTask = null;
            StartLoopButton.IsEnabled = true;
            StopLoopButton.IsEnabled = false;
            SetStatus("Ready");
        }
    }

    private async void BrowseButton_Click(object? sender, RoutedEventArgs args)
    {
        TopLevel? topLevel = GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select local sync folder",
            AllowMultiple = false,
        }).ConfigureAwait(true);
        IStorageFolder? folder = folders.FirstOrDefault();
        if (folder?.Path.LocalPath is { Length: > 0 } localPath)
        {
            LocalPathBox.Text = localPath;
        }
    }

    private void ClearButton_Click(object? sender, RoutedEventArgs args)
    {
        Activities.Clear();
    }

    private async Task LoginAsync(CancellationToken cancellationToken)
    {
        Uri server = ReadServer();
        string username = ReadRequired(UsernameBox.Text, "Username");
        string password = ReadRequired(PasswordInput.Text, "Password");
        SqliteClientStateStore store = CreateClientStateStore();
        using HttpClient httpClient = CreateHttpClient(server);
        var client = new CottonCloudClient(httpClient, store, new CottonSdkOptions { BaseAddress = server });
        await client.Auth.LoginAsync(new LoginRequestDto
        {
            Username = username,
            Password = password,
            TwoFactorCode = NormalizeOptional(TwoFactorBox.Text),
            TrustDevice = TrustDeviceBox.IsChecked == true,
        }, cancellationToken).ConfigureAwait(true);
        await store.SaveServerBaseAddressAsync(server, cancellationToken).ConfigureAwait(true);
        UserDto user = await client.Auth.MeAsync(cancellationToken).ConfigureAwait(true);
        AddActivity("Login", user.Username, "Authenticated on " + server.ToString().TrimEnd('/'));
        SetStatus("Logged in as " + user.Username);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        int intervalSeconds = ReadIntervalSeconds();
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                SyncRunResult result = await RunSyncPassAsync(cancellationToken).ConfigureAwait(true);
                AddResultActivities(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or Cotton.Sdk.CottonApiException or HttpRequestException or IOException)
            {
                AddActivity("Error", string.Empty, exception.Message);
                SetStatus("Sync error");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken).ConfigureAwait(true);
        }
    }

    private async Task<SyncRunResult> RunSyncPassAsync(CancellationToken cancellationToken)
    {
        Uri server = ReadServer();
        string localRoot = Path.GetFullPath(ReadRequired(LocalPathBox.Text, "Local folder"));
        string? remotePath = NormalizeOptional(RemotePathBox.Text);
        Directory.CreateDirectory(localRoot);
        SqliteClientStateStore clientStore = CreateClientStateStore();
        using HttpClient httpClient = CreateHttpClient(server);
        var client = new CottonCloudClient(httpClient, clientStore, new CottonSdkOptions { BaseAddress = server });
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync(remotePath, cancellationToken).ConfigureAwait(true);
        string syncPairId = BuildSyncPairId(server, localRoot, remoteRoot.Id);
        var engine = new SyncEngine(
            new LocalFileScanner(),
            new RemoteTreeCrawler(client.Nodes),
            new SdkRemoteFileSynchronizer(client),
            new SqliteSyncStateStore(GetSyncStateDatabasePath()));
        SetStatus("Syncing " + localRoot);
        return await engine.RunOnceAsync(new SyncPair
        {
            SyncPairId = syncPairId,
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        }, new SyncRunOptions
        {
            ActivityProgress = new Progress<SyncActivity>(activity => AddActivity(activity)),
        }, cancellationToken).ConfigureAwait(true);
    }

    private async Task RunUiActionAsync(string status, Func<CancellationToken, Task> action)
    {
        SetBusy(true);
        SetStatus(status);
        using var cancellation = new CancellationTokenSource();
        try
        {
            await action(cancellation.Token).ConfigureAwait(true);
            if (_loopCancellation is null)
            {
                SetStatus("Ready");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or Cotton.Sdk.CottonApiException or HttpRequestException or IOException)
        {
            AddActivity("Error", string.Empty, exception.Message);
            SetStatus("Error");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void AddResultActivities(SyncRunResult result)
    {
        if (result.Activities.Count == 0)
        {
            AddActivity("Info", string.Empty, "Already in sync.");
        }
    }

    private void AddActivity(SyncActivity activity)
    {
        string kind = activity.Kind switch
        {
            SyncActivityKind.Uploaded => "Uploaded",
            SyncActivityKind.Downloaded => "Downloaded",
            SyncActivityKind.DeletedLocal => "Deleted local",
            SyncActivityKind.DeletedRemote => "Deleted remote",
            SyncActivityKind.Conflict => "Conflict",
            _ => activity.Kind.ToString(),
        };
        AddActivity(kind, activity.RelativePath, activity.Details ?? string.Empty);
    }

    private void AddActivity(string kind, string path, string details)
    {
        Activities.Insert(0, new ActivityRow
        {
            Time = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            Kind = kind,
            Path = path,
            Details = details,
        });
    }

    private void SetBusy(bool isBusy)
    {
        LoginButton.IsEnabled = !isBusy;
        SyncOnceButton.IsEnabled = !isBusy;
        BrowseButton.IsEnabled = !isBusy;
        StartLoopButton.IsEnabled = !isBusy && _loopCancellation is null;
        StopLoopButton.IsEnabled = _loopCancellation is not null;
    }

    private void SetStatus(string status)
    {
        StatusText.Text = status;
    }

    private void LoadDefaults()
    {
        ServerBox.Text = "http://localhost:5182";
        LocalPathBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Cotton");
        RemotePathBox.Text = "DesktopSync";
        IntervalBox.Text = DefaultIntervalSeconds.ToString(CultureInfo.InvariantCulture);
    }

    private Uri ReadServer()
    {
        string server = ReadRequired(ServerBox.Text, "Server URL").TrimEnd('/');
        return new Uri(server, UriKind.Absolute);
    }

    private int ReadIntervalSeconds()
    {
        string value = NormalizeOptional(IntervalBox.Text) ?? DefaultIntervalSeconds.ToString(CultureInfo.InvariantCulture);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
            ? parsed
            : DefaultIntervalSeconds;
    }

    private SqliteClientStateStore CreateClientStateStore()
    {
        return new SqliteClientStateStore(Path.Combine(GetConfigDirectory(), "client-state.sqlite"));
    }

    private string GetSyncStateDatabasePath()
    {
        return Path.Combine(GetConfigDirectory(), "sync-state.sqlite");
    }

    private static string GetConfigDirectory()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        return Path.Combine(appData, "Cotton", "SyncDesktop");
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

    private static string ReadRequired(string? value, string label)
    {
        string? normalized = NormalizeOptional(value);
        return normalized ?? throw new InvalidOperationException(label + " is required.");
    }

    private static string? NormalizeOptional(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
