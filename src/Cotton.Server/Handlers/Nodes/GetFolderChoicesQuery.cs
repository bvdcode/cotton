// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Nodes;
using Cotton.Server.Models.Dto;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Nodes
{
    public record GetFolderChoicesQuery(Guid UserId, Guid NodeId, int Page) : IRequest<FolderChoicesDto>;

    public class GetFolderChoicesQueryHandler(CottonDbContext db) : IRequestHandler<GetFolderChoicesQuery, FolderChoicesDto>
    {
        private const int PageSize = 200;

        public async Task<FolderChoicesDto> Handle(GetFolderChoicesQuery request, CancellationToken ct)
        {
            if (request.Page < 1 || request.Page > int.MaxValue / PageSize)
            {
                throw new BadRequestException<Node>("Invalid page.");
            }
            Node current = await db.Nodes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.NodeId
                && x.OwnerId == request.UserId && x.Type == NodeType.Default && x.Layout.IsActive, ct)
                ?? throw new EntityNotFoundException<Node>("Folder not found.");
            List<Node> folders = await db.Nodes.AsNoTracking().Where(x => x.ParentId == current.Id
                && x.OwnerId == request.UserId && x.Type == NodeType.Default)
                .OrderBy(x => x.NameKey).ThenBy(x => x.Id).Skip((request.Page - 1) * PageSize)
                .Take(PageSize + 1).ToListAsync(ct);
            return new FolderChoicesDto
            {
                Current = current.Adapt<NodeDto>(),
                Folders = folders.Take(PageSize).Adapt<List<NodeDto>>(), HasMore = folders.Count > PageSize,
            };
        }
    }
}
