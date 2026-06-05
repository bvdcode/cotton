// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Shared.Contracts.Auth;
using Cotton.Sdk.Auth;
using Microsoft.AspNetCore.SignalR.Client;

namespace Cotton.Sdk.Realtime;

/// <summary>
/// Connects to the Cotton SignalR event hub.
/// </summary>
public sealed class CottonRealtimeClient : ICottonRealtimeClient
{
    private readonly HubConnection _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="CottonRealtimeClient" /> class.
    /// </summary>
    public CottonRealtimeClient(ICottonTokenStore tokenStore, CottonSdkOptions options)
    {
        ArgumentNullException.ThrowIfNull(tokenStore);
        ArgumentNullException.ThrowIfNull(options);

        _connection = new HubConnectionBuilder()
            .WithUrl(
                CottonRealtimeHubEndpoint.CreateUri(options.BaseAddress),
                hubOptions =>
                {
                    hubOptions.AccessTokenProvider = async () =>
                    {
                        TokenPairDto? tokens = await tokenStore.GetAsync().ConfigureAwait(false);
                        return string.IsNullOrWhiteSpace(tokens?.AccessToken) ? null : tokens.AccessToken;
                    };
                })
            .WithAutomaticReconnect()
            .Build();

        RegisterHandlers();
    }

    /// <inheritdoc />
    public event EventHandler<CottonRealtimeEvent>? RemoteFileTreeChanged;

    /// <inheritdoc />
    public event EventHandler<CottonRealtimeEvent>? SessionRevoked;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return _connection.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return _connection.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return _connection.DisposeAsync();
    }

    private void RegisterHandlers()
    {
        foreach (string methodName in CottonRealtimeHubMethods.RemoteFileTreeChanged)
        {
            _connection.On<object?>(methodName, _ => PublishRemoteFileTreeChanged(methodName));
        }

        _connection.On(
            CottonRealtimeHubMethods.SessionRevoked,
            () => PublishSessionRevoked(CottonRealtimeHubMethods.SessionRevoked));
    }

    private void PublishRemoteFileTreeChanged(string methodName)
    {
        RemoteFileTreeChanged?.Invoke(
            this,
            new CottonRealtimeEvent(
                CottonRealtimeEventKind.RemoteFileTreeChanged,
                methodName,
                DateTime.UtcNow));
    }

    private void PublishSessionRevoked(string methodName)
    {
        SessionRevoked?.Invoke(
            this,
            new CottonRealtimeEvent(
                CottonRealtimeEventKind.SessionRevoked,
                methodName,
                DateTime.UtcNow));
    }
}
