// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Providers;
using Cotton.Storage.Processors;

namespace Cotton.Server.Services;

/// <summary>
/// Reads the runtime Zstandard compression level from server settings.
/// </summary>
public sealed class SettingsCompressionLevelProvider(SettingsProvider settings) : ICompressionLevelProvider
{
    /// <inheritdoc />
    public int Level => settings.GetServerSettings().CompressionLevel;
}
