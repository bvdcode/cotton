// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Services;
using Cotton.Server.Services.WebDav;
using Cotton.Validators;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.WebDav;

/// <summary>
/// Command for WebDAV MKCOL operation (create directory)
/// </summary>
public record WebDavMkColRequest(
    Guid UserId,
    string Path) : IRequest<WebDavMkColResult>;

/// <summary>
/// Result of WebDAV MKCOL operation
/// </summary>
public record WebDavMkColResult(
    bool Success,
    WebDavMkColError? Error = null,
    Guid? NodeId = null);

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
public class WebDavMkColRequestHandler(
    CottonDbContext _dbContext,
    IWebDavPathResolver _pathResolver,
    IEventNotificationService _eventNotification,
    ILogger<WebDavMkColRequestHandler> _logger)
    : IRequestHandler<WebDavMkColRequest, WebDavMkColResult>
{
    public async Task<WebDavMkColResult> Handle(WebDavMkColRequest request, CancellationToken ct)
    {
        // Resolve parent first to know the layout for the advisory lock.
        var parentResult = await _pathResolver.GetParentNodeAsync(request.UserId, request.Path, ct);
        if (!parentResult.Found || parentResult.ParentNode is null || parentResult.ResourceName is null)
        {
            _logger.LogDebug("WebDAV MKCOL: Parent not found for path: {Path}", request.Path);
            return new WebDavMkColResult(false, WebDavMkColError.ParentNotFound);
        }

        var nameFailure = ValidateResourceName(parentResult.ResourceName);
        if (nameFailure is not null)
        {
            return nameFailure;
        }

        // Per-layout namespace serialization - see LayoutLocks.
        // Existence and cross-table conflict checks must happen INSIDE the lock,
        // otherwise a concurrent CreateFile/PUT with the same NameKey can land
        // a cross-table duplicate.
        Guid lockedLayoutId = parentResult.ParentNode.LayoutId;
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        await LayoutLocks.AcquireForLayoutAsync(_dbContext, lockedLayoutId, ct);

        parentResult = await _pathResolver.GetParentNodeAsync(request.UserId, request.Path, ct);
        if (!parentResult.Found || parentResult.ParentNode is null || parentResult.ResourceName is null)
        {
            _logger.LogDebug("WebDAV MKCOL: Parent not found for path after locking: {Path}", request.Path);
            return new WebDavMkColResult(false, WebDavMkColError.ParentNotFound);
        }

        if (parentResult.ParentNode.LayoutId != lockedLayoutId)
        {
            _logger.LogDebug("WebDAV MKCOL: Parent layout changed while waiting for lock: {Path}", request.Path);
            return new WebDavMkColResult(false, WebDavMkColError.ParentNotFound);
        }

        nameFailure = ValidateResourceName(parentResult.ResourceName);
        if (nameFailure is not null)
        {
            return nameFailure;
        }

        var nameKey = NameValidator.NormalizeAndGetNameKey(parentResult.ResourceName);

        var existing = await _pathResolver.ResolveMetadataAsync(request.UserId, request.Path, ct);
        if (existing.Found)
        {
            _logger.LogDebug("WebDAV MKCOL: Path already exists: {Path}", request.Path);
            return new WebDavMkColResult(false, WebDavMkColError.AlreadyExists);
        }

        var fileExists = await _dbContext.NodeFiles
            .AnyAsync(f => f.NodeId == parentResult.ParentNode.Id
                && f.OwnerId == request.UserId
                && f.NameKey == nameKey, ct);
        if (fileExists)
        {
            _logger.LogDebug("WebDAV MKCOL: Conflict with existing file: {Path}", request.Path);
            return new WebDavMkColResult(false, WebDavMkColError.Conflict);
        }

        var newNode = new Node
        {
            OwnerId = request.UserId,
            ParentId = parentResult.ParentNode.Id,
            Type = WebDavPathResolver.DefaultNodeType,
            LayoutId = parentResult.ParentNode.LayoutId,
        };
        newNode.SetName(parentResult.ResourceName);

        await _dbContext.Nodes.AddAsync(newNode, ct);
        await _dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation("WebDAV MKCOL: Created directory {Path} for user {UserId}", request.Path, request.UserId);

        await _eventNotification.NotifyNodeCreatedAsync(newNode.Id, ct);

        return new WebDavMkColResult(true, null, newNode.Id);
    }

    private WebDavMkColResult? ValidateResourceName(string resourceName)
    {
        if (NameValidator.TryNormalizeAndValidate(resourceName, out _, out var errorMessage))
        {
            return null;
        }

        _logger.LogDebug("WebDAV MKCOL: Invalid name: {Name}, Error: {Error}", resourceName, errorMessage);
        return new WebDavMkColResult(false, WebDavMkColError.InvalidName);
    }
}
