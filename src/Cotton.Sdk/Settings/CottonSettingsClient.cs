// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton;
using Cotton.Settings;
using Cotton.Sdk.Internal;

namespace Cotton.Sdk.Settings;

/// <summary>
/// Provides client-visible Cotton server settings.
/// </summary>
public sealed class CottonSettingsClient : ICottonSettingsClient
{
    private readonly CottonHttpTransport _transport;

    internal CottonSettingsClient(CottonHttpTransport transport)
    {
        _transport = transport;
    }

    /// <summary>
    /// Gets settings required by SDK file transfer operations.
    /// </summary>
    public Task<ClientSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        return _transport.SendJsonAsync<ClientSettingsDto>(
            HttpMethod.Get,
            Routes.V1.Settings,
            cancellationToken: cancellationToken);
    }
}
