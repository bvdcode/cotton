// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Shared.Models.Enums
{
    /// <summary>Describes durable file-tree mutations visible to synchronization clients.</summary>
    public enum SyncChangeKind
    {
        /// <summary>Unknown or uninitialized mutation kind.</summary>
        Unknown = 0,
        /// <summary>A file entry was created.</summary>
        FileCreated = 1,
        /// <summary>A file entry content or metadata payload was updated.</summary>
        FileContentUpdated = 2,
        /// <summary>A file entry was renamed.</summary>
        FileRenamed = 3,
        /// <summary>A file entry was moved to a different parent folder.</summary>
        FileMoved = 4,
        /// <summary>A file entry was deleted or moved to trash.</summary>
        FileDeleted = 5,
        /// <summary>A file entry was restored from trash.</summary>
        FileRestored = 6,
        /// <summary>A folder was created.</summary>
        FolderCreated = 7,
        /// <summary>A folder was renamed.</summary>
        FolderRenamed = 8,
        /// <summary>A folder was moved to a different parent folder.</summary>
        FolderMoved = 9,
        /// <summary>A folder was deleted or moved to trash.</summary>
        FolderDeleted = 10,
        /// <summary>A folder was restored from trash.</summary>
        FolderRestored = 11,
    }
}
