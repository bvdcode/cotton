// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Handlers.Moves;
using EasyExtensions.AspNetCore.Exceptions;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Handlers.Nodes;
using Cotton.Server.Services;
using Cotton.Server.Extensions;
using Cotton.Server.Services.WebDav;
using Cotton.Validators;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.WebDav
{
    public record WebDavMoveRequest(
        Guid UserId,
        string SourcePath,
        string DestinationPath,
        bool Overwrite = false) : IRequest<WebDavMoveResult>;

    public class WebDavMoveRequestHandler(
        CottonDbContext _dbContext,
        IMediator _mediator,
        IWebDavPathResolver _pathResolver,
        IEventNotificationService _eventNotification,
        CommittedMutationEffects _effects,
        ILayoutMutationGate _layoutGate,
        ILogger<WebDavMoveRequestHandler> _logger)
        : IRequestHandler<WebDavMoveRequest, WebDavMoveResult>
    {
        public async Task<WebDavMoveResult> Handle(WebDavMoveRequest request, CancellationToken ct)
        {
            PreTransactionSourceOutcome preTransaction = await ResolvePreTransactionSourceAsync(request, ct);
            if (preTransaction.Failure is not null)
            {
                return preTransaction.Failure;
            }

            await using IAsyncDisposable layoutGate = await _layoutGate.EnterAsync(preTransaction.LayoutId!.Value, ct);
            await using IDbContextTransaction tx = await _dbContext.Database.BeginTransactionAsync(ct);

            MovePreparationOutcome preparedMove = await PrepareMoveInTransactionAsync(request, preTransaction.LayoutId.Value, ct);
            if (preparedMove.Failure is not null)
            {
                return preparedMove.Failure;
            }

            bool replaced;
            try
            {
                replaced = await PerformMoveAsync(request, preparedMove.Source!, preparedMove.DestinationParent!, ct);
            }
            catch (DuplicateException)
            {
                return Fail(WebDavMoveError.DestinationExists);
            }
            catch (InvalidFolderMoveException)
            {
                return Fail(WebDavMoveError.CannotMoveIntoDescendant);
            }

            await tx.CommitAsync(ct);
            await NotifyAfterCommitAsync(request, preparedMove.Source!, preparedMove.OldParentId, ct);

            (Guid? NodeId, Guid? NodeFileId) movedIds = GetMovedIds(preparedMove.Source!);
            return Ok(!replaced, movedIds.NodeId, movedIds.NodeFileId);
        }

        private async Task<PreTransactionSourceOutcome> ResolvePreTransactionSourceAsync(WebDavMoveRequest request, CancellationToken ct)
        {
            WebDavResolveResult source = await ResolveSourceAsync(request, ct);
            WebDavMoveResult? failure = TryGetSourceValidationFailure(request, source);
            if (failure is not null)
            {
                return PreTransactionSourceOutcome.Failed(failure);
            }

            Guid layoutId = await GetSourceLayoutIdAsync(source, ct);
            return PreTransactionSourceOutcome.Success(layoutId);
        }

        private async Task<MovePreparationOutcome> PrepareMoveInTransactionAsync(
            WebDavMoveRequest request,
            Guid sourceLayoutId,
            CancellationToken ct)
        {
            WebDavResolveResult source = await ResolveSourceAsync(request, ct);
            WebDavMoveResult? sourceFailure = await ValidateSourceInTransactionAsync(request, source, sourceLayoutId, ct);
            if (sourceFailure is not null)
            {
                return MovePreparationOutcome.Failed(sourceFailure);
            }

            WebDavParentResult destination = await GetAndValidateDestinationParentAsync(request, ct);
            WebDavMoveResult? destinationFailure = ValidateDestinationInTransaction(destination, request, sourceLayoutId);
            if (destinationFailure is not null)
            {
                return MovePreparationOutcome.Failed(destinationFailure);
            }

            return MovePreparationOutcome.Success(source, destination, GetOldParentId(source));
        }

        private async Task<WebDavMoveResult?> ValidateSourceInTransactionAsync(
            WebDavMoveRequest request,
            WebDavResolveResult source,
            Guid sourceLayoutId,
            CancellationToken ct)
        {
            WebDavMoveResult? sourceFailure = TryGetSourceValidationFailure(request, source);
            if (sourceFailure is not null)
            {
                return sourceFailure;
            }

            if (await GetSourceLayoutIdAsync(source, ct) == sourceLayoutId)
            {
                return null;
            }

            _logger.LogDebug("WebDAV MOVE: Source layout changed during move preparation: {Path}", request.SourcePath);
            return Fail(WebDavMoveError.SourceNotFound);
        }

        private WebDavMoveResult? ValidateDestinationInTransaction(
            WebDavParentResult destination,
            WebDavMoveRequest request,
            Guid sourceLayoutId)
        {
            WebDavMoveResult? destinationFailure = TryGetDestinationParentFailure(destination);
            if (destinationFailure is not null)
            {
                return destinationFailure;
            }

            if (destination.ParentNode!.LayoutId == sourceLayoutId)
            {
                return null;
            }

            _logger.LogDebug("WebDAV MOVE: Destination parent layout differs from source layout: {Path}", request.DestinationPath);
            return Fail(WebDavMoveError.DestinationParentNotFound);
        }

        private static Guid? GetOldParentId(WebDavResolveResult source)
        {
            return source.IsCollection
                ? source.Node?.ParentId
                : source.NodeFile?.NodeId;
        }

        private record PreTransactionSourceOutcome(Guid? LayoutId, WebDavMoveResult? Failure)
        {
            public static PreTransactionSourceOutcome Success(Guid layoutId) => new(layoutId, null);

            public static PreTransactionSourceOutcome Failed(WebDavMoveResult failure) => new(null, failure);
        }

        private record MovePreparationOutcome(
            WebDavResolveResult? Source,
            WebDavParentResult? DestinationParent,
            Guid? OldParentId,
            WebDavMoveResult? Failure)
        {
            public static MovePreparationOutcome Success(
                WebDavResolveResult source,
                WebDavParentResult destinationParent,
                Guid? oldParentId) => new(source, destinationParent, oldParentId, null);

            public static MovePreparationOutcome Failed(WebDavMoveResult failure) => new(null, null, null, failure);
        }

        private async Task<Guid> GetSourceLayoutIdAsync(WebDavResolveResult source, CancellationToken ct)
        {
            // The resolver does not include Node on NodeFile, so we look the file's
            // layout up by its parent NodeId.
            if (source.IsCollection)
            {
                return source.Node!.LayoutId;
            }
            return await _dbContext.Nodes
                .AsNoTracking()
                .Where(n => n.Id == source.NodeFile!.NodeId)
                .Select(n => n.LayoutId)
                .SingleAsync(ct);
        }

        private static WebDavMoveResult Ok(bool created, Guid? movedNodeId, Guid? movedNodeFileId) =>
            new(true, created, null, movedNodeId, movedNodeFileId);

        private static WebDavMoveResult Fail(WebDavMoveError error) =>
            new(false, false, error);

        private WebDavMoveResult? TryGetSourceValidationFailure(WebDavMoveRequest request, WebDavResolveResult sourceResult)
        {
            if (!sourceResult.Found)
            {
                return Fail(WebDavMoveError.SourceNotFound);
            }

            if (sourceResult.IsCollection && sourceResult.Node?.ParentId is null)
            {
                _logger.LogWarning("WebDAV MOVE: Attempted to move root node for user {UserId}", request.UserId);
                return Fail(WebDavMoveError.CannotMoveRoot);
            }

            return null;
        }

        private static WebDavMoveResult? TryGetDestinationParentFailure(WebDavParentResult destParentResult)
        {
            if (!destParentResult.Found || destParentResult.ParentNode is null || destParentResult.ResourceName is null)
            {
                return Fail(WebDavMoveError.DestinationParentNotFound);
            }

            return null;
        }

        private static (Guid? NodeId, Guid? NodeFileId) GetMovedIds(WebDavResolveResult sourceResult)
        {
            Guid? movedNodeId = sourceResult.IsCollection && sourceResult.Node is not null
                ? sourceResult.Node.Id
                : (Guid?)null;

            return (movedNodeId, sourceResult.NodeFile?.Id);
        }

        private async Task NotifyAfterCommitAsync(
            WebDavMoveRequest request,
            WebDavResolveResult sourceResult,
            Guid? oldParentId,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "WebDAV MOVE: Moved {Source} to {Dest} for user {UserId}",
                request.SourcePath,
                request.DestinationPath,
                request.UserId);

            (Guid? NodeId, Guid? NodeFileId) movedIds = GetMovedIds(sourceResult);
            if (movedIds.NodeId.HasValue && oldParentId.HasValue)
            {
                await _effects.RunAsync(nameof(IEventNotificationService.NotifyNodeMovedAsync),
                    () => _eventNotification.NotifyNodeMovedAsync(movedIds.NodeId.Value, oldParentId.Value, CancellationToken.None));
            }
            else if (movedIds.NodeFileId.HasValue && oldParentId.HasValue)
            {
                await _effects.RunAsync(nameof(IEventNotificationService.NotifyFileMovedAsync),
                    () => _eventNotification.NotifyFileMovedAsync(movedIds.NodeFileId.Value, oldParentId.Value, CancellationToken.None));
            }
        }

        private async Task<WebDavResolveResult> ResolveSourceAsync(WebDavMoveRequest request, CancellationToken ct)
        {
            WebDavResolveResult sourceResult = await _pathResolver.ResolveMetadataAsync(request.UserId, request.SourcePath, ct);
            if (!sourceResult.Found)
            {
                _logger.LogDebug("WebDAV MOVE: Source not found: {Path}", request.SourcePath);
            }
            return sourceResult;
        }

        private async Task<WebDavParentResult> GetAndValidateDestinationParentAsync(WebDavMoveRequest request, CancellationToken ct)
        {
            WebDavParentResult destParentResult = await _pathResolver.GetParentNodeAsync(request.UserId, request.DestinationPath, ct);
            if (!destParentResult.Found || destParentResult.ParentNode is null || destParentResult.ResourceName is null)
            {
                _logger.LogDebug("WebDAV MOVE: Destination parent not found: {Path}", request.DestinationPath);
                return destParentResult;
            }

            if (!NameValidator.TryNormalizeAndValidate(destParentResult.ResourceName, out _, out string? errorMessage))
            {
                _logger.LogDebug("WebDAV MOVE: Invalid name: {Name}, Error: {Error}", destParentResult.ResourceName, errorMessage);
                return destParentResult with { Found = false };
            }

            return destParentResult;
        }

        private async Task<bool> PerformMoveAsync(
            WebDavMoveRequest request,
            WebDavResolveResult source,
            WebDavParentResult destination,
            CancellationToken ct)
        {
            MoveOverwritePolicy overwrite = MoveOverwritePolicy.None;
            if (request.Overwrite)
            {
                overwrite = MoveOverwritePolicy.All;
            }
            if (source.IsCollection)
            {
                Node folder = await _dbContext.Nodes.SingleAsync(node => node.Id == source.Node!.Id, ct);
                return await _mediator.Send(new RelocateFolderRequest(
                    folder, destination.ParentNode!, destination.ResourceName, Overwrite: overwrite), ct);
            }
            NodeFile file = await _dbContext.NodeFiles.Include(file => file.Node).Include(file => file.FileManifest)
                .SingleAsync(file => file.Id == source.NodeFile!.Id, ct);
            return await _mediator.Send(new RelocateFileRequest(
                file, destination.ParentNode!, destination.ResourceName, Overwrite: overwrite), ct);
        }
    }
}
