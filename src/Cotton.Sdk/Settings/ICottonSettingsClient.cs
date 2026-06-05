// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Shared.Contracts.Settings;

namespace Cotton.Sdk.Settings;

/// <summary>
/// Provides client-visible Cotton server settings.
/// </summary>
public interface ICottonSettingsClient
{
    /// <summary>
    /// Gets settings required by SDK file transfer operations.
    /// </summary>
    Task<ClientSettingsDto> GetAsync(CancellationToken cancellationToken = default);
}
