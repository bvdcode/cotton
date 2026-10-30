// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Nodes;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Nodes
{
    public record UpdateNodeMetadataRequest(
        Guid UserId,
        Guid NodeId,
        Dictionary<string, string?>? Patch) : IRequest<UpdateNodeMetadataResult>;

    public class UpdateNodeMetadataRequestHandler(
        CottonDbContext _dbContext,
        IEventNotificationService _notifications,
        ILogger<UpdateNodeMetadataRequestHandler> _logger)
        : IRequestHandler<UpdateNodeMetadataRequest, UpdateNodeMetadataResult>
    {
        public async Task<UpdateNodeMetadataResult> Handle(
            UpdateNodeMetadataRequest request,
            CancellationToken ct)
        {
            string? validationError = ValidatePatch(request.Patch);
            if (validationError is not null)
            {
                return Failure(UpdateNodeMetadataStatus.InvalidPatch, validationError);
            }

            Node? node = await _dbContext.Nodes
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

            Dictionary<string, string> metadata = node.Metadata is null
                ? []
                : new Dictionary<string, string>(node.Metadata);
            foreach ((string key, string? value) in request.Patch!)
            {
                metadata[key] = value!;
            }

            node.Metadata = metadata;
            await _dbContext.SaveChangesAsync(ct);
            NodeDto nodeDto = node.Adapt<NodeDto>();
            try
            {
                await _notifications.NotifyNodeMetadataUpdatedAsync(
                    request.UserId,
                    nodeDto,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to send node metadata update notification for node {NodeId}",
                    request.NodeId);
            }

            return new UpdateNodeMetadataResult(
                UpdateNodeMetadataStatus.Updated,
                nodeDto);
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
