// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sdk.Auth;
using Cotton.Sdk.Chunks;
using Cotton.Sdk.Files;
using Cotton.Sdk.Nodes;
using Cotton.Sdk.Settings;
using Cotton.Sdk.Sync;

namespace Cotton.Sdk;

/// <summary>
/// Provides typed access to Cotton Cloud API groups.
/// </summary>
public interface ICottonCloudClient
{
    /// <summary>
    /// Gets authentication operations.
    /// </summary>
    ICottonAuthClient Auth { get; }

    /// <summary>
    /// Gets client settings operations.
    /// </summary>
    ICottonSettingsClient Settings { get; }

    /// <summary>
    /// Gets chunk operations.
    /// </summary>
    ICottonChunkClient Chunks { get; }

    /// <summary>
    /// Gets file operations.
    /// </summary>
    ICottonFileClient Files { get; }

    /// <summary>
    /// Gets node operations.
    /// </summary>
    ICottonNodeClient Nodes { get; }

    /// <summary>
    /// Gets synchronization feed operations.
    /// </summary>
    ICottonSyncClient Sync { get; }
}
