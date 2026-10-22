// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Nodes;
using Cotton.Server.Abstractions;
using Cotton.Server.Services;
using Cotton.Topology;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.Nodes
{
    public record UpdateNodeMetadataRequest(
        Guid UserId,
        Guid NodeId,
        Dictionary<string, string?>? Patch,
        bool IncludeDescendants = false) : IRequest<UpdateNodeMetadataResult>;

    public class UpdateNodeMetadataRequestHandler(
        CottonDbContext _dbContext,
        ILayoutMutationGate _layoutGate,
        IEventNotificationService _notifications,
        ILogger<UpdateNodeMetadataRequestHandler> _logger)
        : IRequestHandler<UpdateNodeMetadataRequest, UpdateNodeMetadataResult>
    {
        private const int BatchSize = 256;

        public async Task<UpdateNodeMetadataResult> Handle(
            UpdateNodeMetadataRequest request,
            CancellationToken ct)
        {
            string? validationError = ValidatePatch(request.Patch);
            if (validationError is not null)
            {
                return Failure(UpdateNodeMetadataStatus.InvalidPatch, validationError);
            }

            Node? node = await _dbContext.Nodes.AsNoTracking()
                .Where(x => x.Id == request.NodeId
                    && x.OwnerId == request.UserId
                    && x.Type == NodeType.Default)
                .SingleOrDefaultAsync(ct);
            if (node is null)
            {
                return Failure(
                    UpdateNodeMetadataStatus.NodeNotFound,
                    "Node not found.");
            }

            List<NodeDto> updatedNodes;
            await using (IAsyncDisposable gate = await _layoutGate.EnterAsync(node.LayoutId, ct))
            await using (IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(ct))
            {
                updatedNodes = await ApplyPatchAsync(node, request, ct);
                if (updatedNodes.Count == 0)
                {
                    return Failure(UpdateNodeMetadataStatus.NodeNotFound, "Node not found.");
                }
                await transaction.CommitAsync(ct);
            }

            foreach (NodeDto updatedNode in updatedNodes)
            {
                try
                {
                    await _notifications.NotifyNodeMetadataUpdatedAsync(request.UserId, updatedNode, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to send node metadata update notification for node {NodeId}", updatedNode.Id);
                }
            }

            return new UpdateNodeMetadataResult(
                UpdateNodeMetadataStatus.Updated,
                updatedNodes);
        }

        private async Task<List<NodeDto>> ApplyPatchAsync(
            Node root, UpdateNodeMetadataRequest request, CancellationToken ct)
        {
            NodeDirectory directory = new(_dbContext, root);
            IQueryable<Node> scope = _dbContext.Nodes.Where(node => node.OwnerId == request.UserId
                && node.LayoutId == root.LayoutId && node.Type == NodeType.Default);
            Queue<Guid> pending = new([root.Id]);
            HashSet<Guid> visited = [root.Id];
            List<NodeDto> updatedNodes = [];
            while (pending.Count > 0)
            {
                List<Guid> batch = [];
                while (pending.Count > 0 && batch.Count < BatchSize)
                {
                    batch.Add(pending.Dequeue());
                }
                List<Node> nodes = await scope.Where(node => batch.Contains(node.Id)).ToListAsync(ct);
                foreach (Node node in nodes)
                {
                    node.Metadata = NodeMetadataPatch.Apply(node.Metadata, request.Patch);
                }
                await _dbContext.SaveChangesAsync(ct);
                updatedNodes.AddRange(nodes.Select(node => node.Adapt<NodeDto>()));
                if (request.IncludeDescendants && nodes.Count > 0)
                {
                    Guid[] parentIds = [.. nodes.Select(node => node.Id)];
                    List<Guid> children = await directory.GetNodes(parentIds).AsNoTracking()
                        .Select(node => node.Id).ToListAsync(ct);
                    foreach (Guid childId in children)
                    {
                        if (visited.Add(childId))
                        {
                            pending.Enqueue(childId);
                        }
                    }
                }
                _dbContext.ChangeTracker.Clear();
            }
            return updatedNodes;
        }

        private static string? ValidatePatch(
            IReadOnlyDictionary<string, string?>? patch)
        {
            if (patch is null)
            {
                return "Metadata patch is required.";
            }

            if (patch.Keys.Any(string.IsNullOrWhiteSpace))
            {
                return "Metadata keys must be non-empty strings.";
            }

            return patch.Values.Any(value => value is null)
                ? "Metadata values must be strings."
                : null;
        }

        private static UpdateNodeMetadataResult Failure(
            UpdateNodeMetadataStatus status,
            string error) => new(status, Error: error);
    }
}
