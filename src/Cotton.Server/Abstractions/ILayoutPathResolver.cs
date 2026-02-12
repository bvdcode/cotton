// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;

namespace Cotton.Server.Abstractions;

public interface ILayoutPathResolver
{
    Task<(Layout Layout, Node Root)> GetLayoutAndRootAsync(Guid userId, NodeType nodeType, CancellationToken ct = default);
    Task<Node?> ResolveNodeByPathAsync(Guid userId, string? path, NodeType nodeType, CancellationToken ct = default);
}
