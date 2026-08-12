// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Nodes;
using Cotton.Topology.Abstractions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;

namespace Cotton.Server.Handlers.Layouts
{
    public record ResolveLayoutPathQuery(
        Guid UserId,
        string? Path,
        NodeType NodeType) : IRequest<NodeDto?>;

    public class ResolveLayoutPathQueryHandler(ILayoutNavigator _navigator)
        : IRequestHandler<ResolveLayoutPathQuery, NodeDto?>
    {
        public async Task<NodeDto?> Handle(
            ResolveLayoutPathQuery request,
            CancellationToken ct)
        {
            Node? node = await _navigator.ResolveNodeByPathAsync(
                request.UserId,
                request.Path,
                request.NodeType,
                ct);
            return node?.Adapt<NodeDto>();
        }
    }
}
