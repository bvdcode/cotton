// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Layouts
{
    public record CreateSharedArchiveDownloadLinkRequest(
        string Token,
        Guid? NodeId) : IRequest<CreateSharedArchiveDownloadLinkResult>;

    public class CreateSharedArchiveDownloadLinkRequestHandler(
        IMediator _mediator,
        ArchiveDownloadService _archives)
        : IRequestHandler<CreateSharedArchiveDownloadLinkRequest, CreateSharedArchiveDownloadLinkResult>
    {
        public async Task<CreateSharedArchiveDownloadLinkResult> Handle(
            CreateSharedArchiveDownloadLinkRequest request,
            CancellationToken ct)
        {
            SharedNodeAccess? access = await _mediator.Send(
                new ResolveSharedNodeAccessQuery(request.Token),
                ct);
            if (access is null)
            {
                return new CreateSharedArchiveDownloadLinkResult(
                    CreateSharedArchiveDownloadLinkStatus.SharedFolderNotFound);
            }

            Guid targetNodeId = request.NodeId ?? access.NodeId;
            bool canAccessNode = await _mediator.Send(
                new VerifySharedNodeSubtreeAccessQuery(
                    targetNodeId,
                    access.NodeId,
                    access.CreatedByUserId),
                ct);
            if (!canAccessNode)
            {
                return new CreateSharedArchiveDownloadLinkResult(
                    CreateSharedArchiveDownloadLinkStatus.FolderNotFound);
            }

            CreateArchiveDownloadLinkResult archive = await _archives.CreateDownloadLinkAsync(
                access.CreatedByUserId,
                new CreateArchiveDownloadLinkRequest
                {
                    NodeIds = [targetNodeId],
                    EnforcePublicShareLimits = true,
                },
                ct);

            return new CreateSharedArchiveDownloadLinkResult(
                CreateSharedArchiveDownloadLinkStatus.ArchiveResult,
                archive);
        }
    }
}
