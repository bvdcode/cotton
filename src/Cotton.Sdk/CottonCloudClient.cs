// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Sdk.Auth;
using Cotton.Sdk.Chunks;
using Cotton.Sdk.Files;
using Cotton.Sdk.Internal;
using Cotton.Sdk.Nodes;
using Cotton.Sdk.Notifications;
using Cotton.Sdk.Realtime;
using Cotton.Sdk.Settings;
using Cotton.Sdk.Sync;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Sdk;

/// <summary>
/// Provides typed access to Cotton Cloud APIs.
/// </summary>
public class CottonCloudClient : ICottonCloudClient
{
    private readonly HttpClient? _ownedHttpClient;
    private int _disposeState;

    /// <summary>
    /// Initializes a new instance of the <see cref="CottonCloudClient" /> class.
    /// </summary>
    /// <remarks>
    /// The caller retains ownership of <paramref name="httpClient" />. Use the constructor without an
    /// <see cref="HttpClient" /> when the SDK client should manage its own HTTP client.
    /// </remarks>
    public CottonCloudClient(
        HttpClient httpClient,
        ICottonTokenStore tokenStore,
        CottonSdkOptions? options = null,
        ILoggerFactory? loggerFactory = null)
        : this(httpClient, tokenStore, options, loggerFactory, ownsHttpClient: false)
    {
    }

    /// <summary>
    /// Initializes a client that creates, owns, and disposes its own <see cref="HttpClient" />.
    /// </summary>
    public CottonCloudClient(
        ICottonTokenStore tokenStore,
        CottonSdkOptions? options = null,
        ILoggerFactory? loggerFactory = null)
        : this(CreateOwnedHttpClient(tokenStore), tokenStore, options, loggerFactory, ownsHttpClient: true)
    {
    }

    private CottonCloudClient(
        HttpClient httpClient,
        ICottonTokenStore tokenStore,
        CottonSdkOptions? options,
        ILoggerFactory? loggerFactory,
        bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(tokenStore);
        _ownedHttpClient = ownsHttpClient ? httpClient : null;
        CottonSdkOptions resolvedOptions = options ?? new CottonSdkOptions();
        ILoggerFactory resolvedLoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        var transport = new CottonHttpTransport(
            httpClient,
            tokenStore,
            resolvedOptions,
            resolvedLoggerFactory.CreateLogger<CottonHttpTransport>());
        Auth = new CottonAuthClient(transport, tokenStore);
        Settings = new CottonSettingsClient(transport);
        Chunks = new CottonChunkClient(transport);
        Files = new CottonFileClient(transport);
        Nodes = new CottonNodeClient(transport);
        Notifications = new CottonNotificationClient(transport);
        Sync = new CottonSyncClient(transport);
        Realtime = new CottonRealtimeClient(tokenStore, resolvedOptions);
    }

    private static HttpClient CreateOwnedHttpClient(ICottonTokenStore tokenStore)
    {
        ArgumentNullException.ThrowIfNull(tokenStore);
        return new HttpClient();
    }

    /// <summary>
    /// Gets authentication operations.
    /// </summary>
    public ICottonAuthClient Auth { get; }

    /// <summary>
    /// Gets client settings operations.
    /// </summary>
    public ICottonSettingsClient Settings { get; }

    /// <summary>
    /// Gets chunk operations.
    /// </summary>
    public ICottonChunkClient Chunks { get; }

    /// <summary>
    /// Gets file operations.
    /// </summary>
    public ICottonFileClient Files { get; }

    /// <summary>
    /// Gets node operations.
    /// </summary>
    public ICottonNodeClient Nodes { get; }

    /// <summary>
    /// Gets notification operations.
    /// </summary>
    public ICottonNotificationClient Notifications { get; }

    /// <summary>
    /// Gets synchronization feed operations.
    /// </summary>
    public ICottonSyncClient Sync { get; }

    /// <summary>
    /// Gets realtime event hub operations.
    /// </summary>
    public ICottonRealtimeClient Realtime { get; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        try
        {
            await Realtime.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _ownedHttpClient?.Dispose();
        }
    }
}
