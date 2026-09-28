// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Handlers.Layouts;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Archives
{
    public record PrepareArchiveDownloadRequest(ArchiveDownloadTicket Ticket, ArchiveDownloadPlan Plan) : IRequest;

    public class PrepareArchiveDownloadRequestHandler(IMediator mediator) : IRequestHandler<PrepareArchiveDownloadRequest>
    {
        public async Task Handle(PrepareArchiveDownloadRequest request, CancellationToken ct)
        {
            await RequireSharedAccessAsync(request.Ticket, ct);
            IAsyncEnumerable<ArchiveDownloadEntry> entries = await mediator.Send(new ReadArchiveEntriesQuery(request.Ticket), ct);
            await foreach (ArchiveDownloadEntry entry in entries.WithCancellation(ct))
            {
                if (request.Ticket.EnforcePublicShareLimits && request.Plan.EntryCount >= ArchiveDownloadLimits.PublicShareMaxEntries)
                {
                    throw new BadRequestException(ArchiveDownloadLimits.PublicShareLimitMessage);
                }
                await request.Plan.AppendAsync(entry, ct);
            }
        }

        private async Task RequireSharedAccessAsync(ArchiveDownloadTicket ticket, CancellationToken ct)
        {
            if (ticket.ShareToken is null)
            {
                return;
            }
            SharedNodeAccess? access = await mediator.Send(new ResolveSharedNodeAccessQuery(ticket.ShareToken), ct);
            if (access is null || access.CreatedByUserId != ticket.UserId)
            {
                throw new EntityNotFoundException(nameof(Node), "Shared folder is no longer available.");
            }
            foreach (Guid nodeId in ticket.NodeIds)
            {
                IReadOnlyList<Node>? ancestry = await mediator.Send(
                    new ResolveSharedNodeAncestryQuery(nodeId, access.NodeId, ticket.UserId), ct);
                if (ancestry is null)
                {
                    throw new EntityNotFoundException(nameof(Node), "Folder is no longer shared.");
                }
            }
        }
    }
}
