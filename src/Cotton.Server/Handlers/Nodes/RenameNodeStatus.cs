// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Nodes
{
    public enum RenameNodeStatus
    {
        Renamed,

        InvalidName,

        NodeNotFound,

        NameConflict,
    }
}
