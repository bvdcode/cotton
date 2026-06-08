// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
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

/// <summary>
/// Lists the supported web dav mk col error values.
/// </summary>
public enum WebDavMkColError
{
    /// <summary>
    /// Represents the parent not found option.
    /// </summary>
    ParentNotFound,
    /// <summary>
    /// Represents the already exists option.
    /// </summary>
    AlreadyExists,
    /// <summary>
    /// Represents the invalid name option.
    /// </summary>
    InvalidName,
    /// <summary>
    /// Represents the conflict option.
    /// </summary>
    Conflict
}

/// <summary>
/// Handler for WebDAV MKCOL operation
/// </summary>
public class WebDavMkColRequestHandler(
    CottonDbContext _dbContext,
    IWebDavPathResolver _pathResolver,
    IEventNotificationService _eventNotification,
    ISyncChangeRecorder _syncChanges,
    ILogger<WebDavMkColRequestHandler> _logger)
    : IRequestHandler<WebDavMkColRequest, WebDavMkColResult>
{
    /// <summary>
    /// Handles the request through the mediator pipeline.
    /// </summary>
    public async Task<WebDavMkColResult> Handle(WebDavMkColRequest request, CancellationToken ct)
    {
        var parent = await ResolveValidatedParentAsync(request, "path", ct);
        if (parent.Failure is not null)
        {
            return parent.Failure;
        }

        Guid lockedLayoutId = parent.Result!.ParentNode!.LayoutId;
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        await LayoutLocks.AcquireForLayoutAsync(_dbContext, lockedLayoutId, ct);

        WebDavMkColResult result = await CreateCollectionInsideLockAsync(request, lockedLayoutId, ct);
        if (!result.Success)
        {
            return result;
        }

        await tx.CommitAsync(ct);
        return result;
    }

    private async Task<WebDavMkColParent> ResolveValidatedParentAsync(
        WebDavMkColRequest request,
        string context,
        CancellationToken ct)
    {
        var parentResult = await _pathResolver.GetParentNodeAsync(request.UserId, request.Path, ct);
        var parentFailure = TryGetParentFailure(request.Path, parentResult, context);
        if (parentFailure is not null)
        {
            return new WebDavMkColParent(null, parentFailure);
        }

        var nameFailure = ValidateResourceName(parentResult.ResourceName!);
        return new WebDavMkColParent(parentResult, nameFailure);
    }

    private WebDavMkColResult? TryGetParentFailure(
        string path,
        WebDavParentResult parentResult,
        string context)
    {
        if (parentResult.Found && parentResult.ParentNode is not null && parentResult.ResourceName is not null)
        {
            return null;
        }

        _logger.LogDebug("WebDAV MKCOL: Parent not found for {Context}: {Path}", context, path);
        return new WebDavMkColResult(false, WebDavMkColError.ParentNotFound);
    }

    private async Task<WebDavMkColResult> CreateCollectionInsideLockAsync(
        WebDavMkColRequest request,
        Guid lockedLayoutId,
        CancellationToken ct)
    {
        var parent = await ResolveValidatedParentAsync(request, "path after locking", ct);
        if (parent.Failure is not null)
        {
            return parent.Failure;
        }

        WebDavParentResult parentResult = parent.Result!;
        if (parentResult.ParentNode!.LayoutId != lockedLayoutId)
        {
            _logger.LogDebug("WebDAV MKCOL: Parent layout changed while waiting for lock: {Path}", request.Path);
            return new WebDavMkColResult(false, WebDavMkColError.ParentNotFound);
        }

        var conflict = await TryGetCreateConflictAsync(request, parentResult, ct);
        if (conflict is not null)
        {
            return conflict;
        }

        Node newNode = BuildNode(request.UserId, parentResult);
        await _dbContext.Nodes.AddAsync(newNode, ct);
        _syncChanges.StageFolderChange(SyncChangeKind.FolderCreated, newNode, parentResult.ParentNode.Id);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("WebDAV MKCOL: Created directory {Path} for user {UserId}", request.Path, request.UserId);
        await _eventNotification.NotifyNodeCreatedAsync(newNode.Id, ct);
        return new WebDavMkColResult(true, null, newNode.Id);
    }

    private async Task<WebDavMkColResult?> TryGetCreateConflictAsync(
        WebDavMkColRequest request,
        WebDavParentResult parentResult,
        CancellationToken ct)
    {
        var existing = await _pathResolver.ResolveMetadataAsync(request.UserId, request.Path, ct);
        if (existing.Found)
        {
            _logger.LogDebug("WebDAV MKCOL: Path already exists: {Path}", request.Path);
            return new WebDavMkColResult(false, WebDavMkColError.AlreadyExists);
        }

        string nameKey = NameValidator.NormalizeAndGetNameKey(parentResult.ResourceName!);
        bool fileExists = await _dbContext.NodeFiles.AnyAsync(f =>
            f.NodeId == parentResult.ParentNode!.Id &&
            f.OwnerId == request.UserId &&
            f.NameKey == nameKey, ct);
        if (!fileExists)
        {
            return null;
        }

        _logger.LogDebug("WebDAV MKCOL: Conflict with existing file: {Path}", request.Path);
        return new WebDavMkColResult(false, WebDavMkColError.Conflict);
    }

    private static Node BuildNode(Guid userId, WebDavParentResult parentResult)
    {
        var newNode = new Node
        {
            OwnerId = userId,
            Type = WebDavPathResolver.DefaultNodeType,
            LayoutId = parentResult.ParentNode!.LayoutId,
        };
        newNode.SetParent(parentResult.ParentNode);
        newNode.SetName(parentResult.ResourceName!);
        return newNode;
    }

    private sealed record WebDavMkColParent(WebDavParentResult? Result, WebDavMkColResult? Failure);

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
