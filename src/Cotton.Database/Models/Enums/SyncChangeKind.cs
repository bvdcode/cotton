// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Database.Models.Enums
{
    /// <summary>Describes durable file-tree mutations visible to synchronization clients.</summary>
    public enum SyncChangeKind
    {
        /// <summary>A file entry was created.</summary>
        FileCreated = 0,
        /// <summary>A file entry content or metadata payload was updated.</summary>
        FileContentUpdated = 1,
        /// <summary>A file entry was renamed.</summary>
        FileRenamed = 2,
        /// <summary>A file entry was moved to a different parent folder.</summary>
        FileMoved = 3,
        /// <summary>A file entry was deleted or moved to trash.</summary>
        FileDeleted = 4,
        /// <summary>A file entry was restored from trash.</summary>
        FileRestored = 5,
        /// <summary>A folder was created.</summary>
        FolderCreated = 6,
        /// <summary>A folder was renamed.</summary>
        FolderRenamed = 7,
        /// <summary>A folder was moved to a different parent folder.</summary>
        FolderMoved = 8,
        /// <summary>A folder was deleted or moved to trash.</summary>
        FolderDeleted = 9,
        /// <summary>A folder was restored from trash.</summary>
        FolderRestored = 10,
    }
}
