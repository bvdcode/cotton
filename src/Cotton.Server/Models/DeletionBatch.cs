// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Models
{
    public record DeletionBatch(
        IReadOnlyList<Node> Folders,
        IReadOnlyList<NodeFile> Files,
        IReadOnlyList<Node> Subtree,
        IReadOnlySet<Guid> SelectedFolderIds,
        IReadOnlySet<Guid> SelectedFileIds);
}
