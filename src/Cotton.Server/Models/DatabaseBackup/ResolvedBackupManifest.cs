// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.DatabaseBackup
{
    /// <summary>
    /// Describes a resolved backup manifest.
    /// </summary>
    public record ResolvedBackupManifest(
        string ManifestStorageKey,
        BackupManifestPointer Pointer,
        BackupManifest Manifest);
}
