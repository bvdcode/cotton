// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Handlers.Nodes;
using Cotton.Server.Services;
using Cotton.Server.Services.WebDav;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.WebDav;

/// <summary>
/// Command for WebDAV DELETE operation
/// </summary>
public record WebDavDeleteRequest(
    Guid UserId,
    string Path,
    bool SkipTrash = false) : IRequest<WebDavDeleteResult>;

/// <summary>
/// Result of WebDAV DELETE operation
/// </summary>
public record WebDavDeleteResult(
    bool Success,
    bool NotFound = false,
    Guid? DeletedNodeId = null,
    Guid? DeletedNodeFileId = null);

/// <summary>
/// Handler for WebDAV DELETE operation
/// </summary>
public class WebDavDeleteRequestHandler(
    CottonDbContext _dbContext,
    IMediator _mediator,
    IWebDavPathResolver _pathResolver,
    IEventNotificationService _eventNotification,
    ILogger<WebDavDeleteRequestHandler> _logger)
    : IRequestHandler<WebDavDeleteRequest, WebDavDeleteResult>
{
    public async Task<WebDavDeleteResult> Handle(WebDavDeleteRequest request, CancellationToken ct)
    {
        var resolveResult = await _pathResolver.ResolveMetadataAsync(request.UserId, request.Path, ct);

        if (!resolveResult.Found)
        {
            _logger.LogDebug("WebDAV DELETE: Path not found: {Path}", request.Path);
            return new WebDavDeleteResult(false, NotFound: true);
        }

        if (resolveResult.IsCollection && resolveResult.Node is not null)
        {
            // Check if it's the root node - don't allow deletion
            if (resolveResult.Node.ParentId is null)
            {
                _logger.LogWarning("WebDAV DELETE: Attempted to delete root node for user {UserId}", request.UserId);
                return new WebDavDeleteResult(false);
            }

            // Get the tracked entity for deletion
            var node = await _dbContext.Nodes
                .FirstOrDefaultAsync(n => n.Id == resolveResult.Node.Id, ct);

            if (node is null)
            {
                return new WebDavDeleteResult(false, NotFound: true);
            }

            // Use existing delete handler
            var deleteQuery = new DeleteNodeQuery(request.UserId, node.Id, request.SkipTrash);
            await _mediator.Send(deleteQuery, ct);

            _logger.LogInformation("WebDAV DELETE: Deleted directory {Path} for user {UserId}", request.Path, request.UserId);

            await _eventNotification.NotifyNodeDeletedAsync(request.UserId, node.Id, ct);

            return new WebDavDeleteResult(true, false, node.Id, null);
        }
        else if (resolveResult.NodeFile is not null)
        {
            // Get the tracked entity for deletion
            var nodeFile = await _dbContext.NodeFiles
                .FirstOrDefaultAsync(f => f.Id == resolveResult.NodeFile.Id, ct);

            if (nodeFile is null)
            {
                return new WebDavDeleteResult(false, NotFound: true);
            }

            // Use existing delete handler
            var deleteQuery = new DeleteFileQuery(request.UserId, nodeFile.Id, request.SkipTrash);
            await _mediator.Send(deleteQuery, ct);

            _logger.LogInformation("WebDAV DELETE: Deleted file {Path} for user {UserId}", request.Path, request.UserId);

            await _eventNotification.NotifyFileDeletedAsync(request.UserId, nodeFile.Id, ct);

            return new WebDavDeleteResult(true, false, null, nodeFile.Id);
        }

        return new WebDavDeleteResult(true);
    }
}
