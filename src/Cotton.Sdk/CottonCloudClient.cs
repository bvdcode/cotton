// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sdk.Auth;
using Cotton.Sdk.Chunks;
using Cotton.Sdk.Files;
using Cotton.Sdk.Internal;
using Cotton.Sdk.Nodes;
using Cotton.Sdk.Settings;

namespace Cotton.Sdk;

/// <summary>
/// Provides typed access to Cotton Cloud APIs.
/// </summary>
public sealed class CottonCloudClient : ICottonCloudClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CottonCloudClient" /> class.
    /// </summary>
    public CottonCloudClient(HttpClient httpClient, ICottonTokenStore tokenStore, CottonSdkOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(tokenStore);
        CottonSdkOptions resolvedOptions = options ?? new CottonSdkOptions();
        var transport = new CottonHttpTransport(httpClient, tokenStore, resolvedOptions);
        Auth = new CottonAuthClient(transport, tokenStore);
        Settings = new CottonSettingsClient(transport);
        Chunks = new CottonChunkClient(transport);
        Files = new CottonFileClient(transport);
        Nodes = new CottonNodeClient(transport);
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
}
