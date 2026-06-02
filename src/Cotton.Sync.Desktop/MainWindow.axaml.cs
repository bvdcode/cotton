// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Collections.ObjectModel;
using System.Data.Common;
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
    private const string LocalPathProfileKey = "desktop.local_path";
    private const string RemotePathProfileKey = "desktop.remote_path";
    private const string IntervalProfileKey = "desktop.interval_seconds";
    private const string UsernameProfileKey = "desktop.username";
    private const string TrustDeviceProfileKey = "desktop.trust_device";
    private CancellationTokenSource? _actionCancellation;
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
        _ = LoadSavedProfileAsync();
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

        var cancellation = new CancellationTokenSource();
        _loopCancellation = cancellation;
        SetBusy(true);
        SetStatus("Sync loop running");
        _loopTask = RunLoopSupervisedAsync(cancellation);
    }

    private async void StopLoopButton_Click(object? sender, RoutedEventArgs args)
    {
        CancellationTokenSource? actionCancellation = _actionCancellation;
        CancellationTokenSource? cancellation = _loopCancellation;
        Task? loopTask = _loopTask;
        if (actionCancellation is null && cancellation is null)
        {
            return;
        }

        actionCancellation?.Cancel();
        cancellation?.Cancel();
        StopLoopButton.IsEnabled = false;
        SetStatus("Canceling...");
        if (loopTask is not null)
        {
            await loopTask.ConfigureAwait(true);
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
        await store.SaveProfileValueAsync(UsernameProfileKey, username, cancellationToken).ConfigureAwait(true);
        await store.SaveProfileValueAsync(TrustDeviceProfileKey, (TrustDeviceBox.IsChecked == true).ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(true);
        UserDto user = await client.Auth.MeAsync(cancellationToken).ConfigureAwait(true);
        PasswordInput.Text = string.Empty;
        AddActivity("Login", user.Username, "Authenticated on " + server.ToString().TrimEnd('/'));
        SetStatus("Logged in as " + user.Username);
    }

    private async Task RunLoopSupervisedAsync(CancellationTokenSource cancellation)
    {
        string finalStatus = "Ready";
        try
        {
            await RunLoopAsync(cancellation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            AddActivity("Info", string.Empty, "Sync loop stopped.");
        }
        catch (Exception exception)
        {
            AddActivity("Error", string.Empty, exception.Message);
            finalStatus = "Error";
        }
        finally
        {
            if (ReferenceEquals(_loopCancellation, cancellation))
            {
                _loopCancellation = null;
                _loopTask = null;
                SetBusy(false);
                SetStatus(finalStatus);
            }

            cancellation.Dispose();
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        int intervalSeconds = ReadIntervalSeconds();
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                SetStatus("Starting sync pass...");
                SyncRunResult result = await RunSyncPassAsync(cancellationToken).ConfigureAwait(true);
                AddResultActivities(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or Cotton.Sdk.CottonApiException or HttpRequestException or IOException or UnauthorizedAccessException or DbException)
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
        if (!Directory.Exists(localRoot))
        {
            throw new InvalidOperationException("Local sync folder does not exist: " + localRoot + ". Create it explicitly before syncing.");
        }

        SqliteClientStateStore clientStore = CreateClientStateStore();
        await SaveSyncProfileAsync(clientStore, server, localRoot, remotePath, cancellationToken).ConfigureAwait(true);
        using HttpClient httpClient = CreateHttpClient(server);
        var client = new CottonCloudClient(httpClient, clientStore, new CottonSdkOptions { BaseAddress = server });
        IProgress<SyncProgress> progress = new Progress<SyncProgress>(UpdateProgress);
        SetStatus("Resolving remote folder...");
        NodeDto remoteRoot = await new RemoteRootResolver(client.Nodes).EnsureAsync(remotePath, cancellationToken).ConfigureAwait(true);
        string syncPairId = BuildSyncPairId(server, localRoot, remoteRoot.Id);
        var engine = new SyncEngine(
            new LocalFileScanner(progress),
            new RemoteTreeCrawler(client.Nodes, progress: progress),
            new SdkRemoteFileSynchronizer(client, progress: progress),
            new SqliteSyncStateStore(GetSyncStateDatabasePath()));
        SetStatus("Scanning and syncing " + localRoot);
        return await engine.RunOnceAsync(new SyncPair
        {
            SyncPairId = syncPairId,
            LocalRootPath = localRoot,
            RemoteRootNodeId = remoteRoot.Id,
        }, new SyncRunOptions
        {
            ActivityProgress = new Progress<SyncActivity>(activity => AddActivity(activity)),
            Progress = progress,
        }, cancellationToken).ConfigureAwait(true);
    }

    private async Task RunUiActionAsync(string status, Func<CancellationToken, Task> action)
    {
        if (_actionCancellation is not null || _loopCancellation is not null)
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _actionCancellation = cancellation;
        SetBusy(true);
        SetStatus(status);
        try
        {
            await action(cancellation.Token).ConfigureAwait(true);
            if (_loopCancellation is null)
            {
                SetStatus("Ready");
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            AddActivity("Info", string.Empty, "Operation canceled.");
            SetStatus("Canceled");
        }
        catch (OperationCanceledException exception)
        {
            AddActivity("Error", string.Empty, exception.Message);
            SetStatus("Error");
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or Cotton.Sdk.CottonApiException or HttpRequestException or IOException or UnauthorizedAccessException or DbException)
        {
            AddActivity("Error", string.Empty, exception.Message);
            SetStatus("Error");
        }
        finally
        {
            if (ReferenceEquals(_actionCancellation, cancellation))
            {
                _actionCancellation = null;
            }

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
        StopLoopButton.IsEnabled = _actionCancellation is not null || _loopCancellation is not null;
        ServerBox.IsEnabled = !isBusy;
        UsernameBox.IsEnabled = !isBusy;
        PasswordInput.IsEnabled = !isBusy;
        TwoFactorBox.IsEnabled = !isBusy;
        TrustDeviceBox.IsEnabled = !isBusy;
        LocalPathBox.IsEnabled = !isBusy;
        RemotePathBox.IsEnabled = !isBusy;
        IntervalBox.IsEnabled = !isBusy;
        BusyProgress.IsVisible = isBusy;
        BusyProgress.IsIndeterminate = isBusy;
        BusyProgress.Value = 0;
    }

    private void SetStatus(string status)
    {
        StatusText.Text = status;
    }

    private void UpdateProgress(SyncProgress progress)
    {
        if (!string.IsNullOrWhiteSpace(progress.Message))
        {
            SetStatus(progress.Message);
        }

        if (progress.Current.HasValue && progress.Total.HasValue && progress.Total.Value > 0)
        {
            BusyProgress.IsIndeterminate = false;
            BusyProgress.Maximum = progress.Total.Value;
            BusyProgress.Value = Math.Min(progress.Current.Value, progress.Total.Value);
            return;
        }

        BusyProgress.IsIndeterminate = BusyProgress.IsVisible;
    }

    private void LoadDefaults()
    {
        ServerBox.Text = "http://localhost:5182";
        LocalPathBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Cotton");
        RemotePathBox.Text = "DesktopSync";
        IntervalBox.Text = DefaultIntervalSeconds.ToString(CultureInfo.InvariantCulture);
    }

    private async Task LoadSavedProfileAsync()
    {
        try
        {
            SqliteClientStateStore store = CreateClientStateStore();
            Uri? server = await store.GetServerBaseAddressAsync().ConfigureAwait(true);
            if (server is not null)
            {
                ServerBox.Text = server.ToString().TrimEnd('/');
            }

            string? username = await store.GetProfileValueAsync(UsernameProfileKey).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(username))
            {
                UsernameBox.Text = username;
            }

            string? trustDevice = await store.GetProfileValueAsync(TrustDeviceProfileKey).ConfigureAwait(true);
            if (bool.TryParse(trustDevice, out bool trust))
            {
                TrustDeviceBox.IsChecked = trust;
            }

            string? localPath = await store.GetProfileValueAsync(LocalPathProfileKey).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(localPath))
            {
                LocalPathBox.Text = localPath;
            }

            string? remotePath = await store.GetProfileValueAsync(RemotePathProfileKey).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(remotePath))
            {
                RemotePathBox.Text = remotePath;
            }

            string? interval = await store.GetProfileValueAsync(IntervalProfileKey).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(interval))
            {
                IntervalBox.Text = interval;
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException or DbException)
        {
            AddActivity("Error", string.Empty, "Could not load saved settings: " + exception.Message);
        }
    }

    private async Task SaveSyncProfileAsync(
        SqliteClientStateStore store,
        Uri server,
        string localRoot,
        string? remotePath,
        CancellationToken cancellationToken)
    {
        await store.SaveServerBaseAddressAsync(server, cancellationToken).ConfigureAwait(true);
        await store.SaveProfileValueAsync(LocalPathProfileKey, localRoot, cancellationToken).ConfigureAwait(true);
        await store.SaveProfileValueAsync(RemotePathProfileKey, remotePath ?? string.Empty, cancellationToken).ConfigureAwait(true);
        await store.SaveProfileValueAsync(IntervalProfileKey, ReadIntervalSeconds().ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(true);
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
