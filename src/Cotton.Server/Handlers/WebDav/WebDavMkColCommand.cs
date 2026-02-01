// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Services.WebDav;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.WebDav;

/// <summary>
/// Command for WebDAV MKCOL operation (create directory)
/// </summary>
public record WebDavMkColCommand(
    Guid UserId,
    string Path) : IRequest<WebDavMkColResult>;

/// <summary>
/// Result of WebDAV MKCOL operation
/// </summary>
public record WebDavMkColResult(
    bool Success,
    WebDavMkColError? Error = null);

public enum WebDavMkColError
{
    ParentNotFound,
    AlreadyExists,
    InvalidName,
    Conflict
}

/// <summary>
/// Handler for WebDAV MKCOL operation
/// </summary>
public class WebDavMkColCommandHandler(
    CottonDbContext _dbContext,
    ILayoutService _layouts,
    IWebDavPathResolver _pathResolver,
    ILogger<WebDavMkColCommandHandler> _logger)
    : IRequestHandler<WebDavMkColCommand, WebDavMkColResult>
{
    public async Task<WebDavMkColResult> Handle(WebDavMkColCommand request, CancellationToken ct)
    {
        // Check if path already exists
        var existing = await _pathResolver.ResolveMetadataAsync(request.UserId, request.Path, ct);
        if (existing.Found)
        {
            _logger.LogDebug("WebDAV MKCOL: Path already exists: {Path}", request.Path);
            return new WebDavMkColResult(false, WebDavMkColError.AlreadyExists);
        }

        // Get parent node
        var parentResult = await _pathResolver.GetParentNodeAsync(request.UserId, request.Path, ct);
        if (!parentResult.Found || parentResult.ParentNode is null || parentResult.ResourceName is null)
        {
            _logger.LogDebug("WebDAV MKCOL: Parent not found for path: {Path}", request.Path);
            return new WebDavMkColResult(false, WebDavMkColError.ParentNotFound);
        }

        // Validate name
        if (!NameValidator.TryNormalizeAndValidate(parentResult.ResourceName, out _, out var errorMessage))
        {
            _logger.LogDebug("WebDAV MKCOL: Invalid name: {Name}, Error: {Error}", parentResult.ResourceName, errorMessage);
            return new WebDavMkColResult(false, WebDavMkColError.InvalidName);
        }

        var nameKey = NameValidator.NormalizeAndGetNameKey(parentResult.ResourceName);

        // Check for conflicts with files
        var fileExists = await _dbContext.NodeFiles
            .AnyAsync(f => f.NodeId == parentResult.ParentNode.Id
                && f.OwnerId == request.UserId
                && f.NameKey == nameKey, ct);

        if (fileExists)
        {
            _logger.LogDebug("WebDAV MKCOL: Conflict with existing file: {Path}", request.Path);
            return new WebDavMkColResult(false, WebDavMkColError.Conflict);
        }

        // Create node
        var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(request.UserId);
        var newNode = new Node
        {
            OwnerId = request.UserId,
            ParentId = parentResult.ParentNode.Id,
            Type = WebDavPathResolver.DefaultNodeType,
            LayoutId = layout.Id,
        };
        newNode.SetName(parentResult.ResourceName);

        await _dbContext.Nodes.AddAsync(newNode, ct);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("WebDAV MKCOL: Created directory {Path} for user {UserId}", request.Path, request.UserId);
        return new WebDavMkColResult(true);
    }
}
