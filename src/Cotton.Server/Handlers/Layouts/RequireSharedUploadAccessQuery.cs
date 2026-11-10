// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Layouts
{
    public record RequireSharedUploadAccessQuery(string Token, Guid NodeId) : IRequest<SharedNodeAccess>;

    public class RequireSharedUploadAccessQueryHandler(IMediator mediator, CottonDbContext db)
        : IRequestHandler<RequireSharedUploadAccessQuery, SharedNodeAccess>
    {
        public async Task<SharedNodeAccess> Handle(RequireSharedUploadAccessQuery request, CancellationToken ct)
        {
            foreach (EntityEntry entry in db.ChangeTracker.Entries().Where(entry => entry.State == EntityState.Unchanged
                && (entry.Entity is Node || entry.Entity is NodeShareToken)).ToArray())
            {
                await entry.ReloadAsync(ct);
            }
            SharedNodeAccess? access = await mediator.Send(new ResolveSharedNodeAccessQuery(request.Token), ct);
            if (access is null || !access.AllowUpload)
            {
                throw new EntityNotFoundException<NodeShareToken>("Upload is not available for this shared folder.");
            }
            IReadOnlyList<Node>? ancestry = await mediator.Send(
                new ResolveSharedNodeAncestryQuery(request.NodeId, access.NodeId, access.CreatedByUserId), ct);
            if (ancestry is null)
            {
                throw new EntityNotFoundException<Node>("Shared folder not found.");
            }
            return access;
        }
    }
}
