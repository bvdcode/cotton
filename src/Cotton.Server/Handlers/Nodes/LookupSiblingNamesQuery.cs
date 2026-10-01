// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Nodes;
using Cotton.Server.Models.Dto;
using Cotton.Topology;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Nodes
{
    public record LookupSiblingNamesQuery(
        Guid UserId,
        Guid NodeId,
        IReadOnlyList<string> Names,
        bool IncludeTakenNamesOnConflict) : IRequest<SiblingNameLookupDto>;

    public class LookupSiblingNamesQueryHandler(
        ILayoutService _layouts,
        CottonDbContext _dbContext)
        : IRequestHandler<LookupSiblingNamesQuery, SiblingNameLookupDto>
    {
        private const int MaxNames = 20_000;

        public async Task<SiblingNameLookupDto> Handle(LookupSiblingNamesQuery request, CancellationToken ct)
        {
            if (request.Names.Count > MaxNames)
            {
                throw new BadRequestException($"At most {MaxNames} names can be checked at once.");
            }

            HashSet<string> requestedKeys = new(StringComparer.Ordinal);
            foreach (string name in request.Names)
            {
                if (!NameValidator.TryNormalizeAndValidate(name, out string normalized, out _))
                {
                    throw new BadRequestException("Invalid file or folder name.");
                }

                requestedKeys.Add(NameValidator.GetNameKey(normalized));
            }

            Layout layout = await _layouts.GetOrCreateLatestUserLayoutAsync(request.UserId, ct);
            Node parent = await _dbContext.Nodes.AsNoTracking()
                .AccessibleTo(request.UserId)
                .SingleOrDefaultAsync(node => node.Id == request.NodeId
                    && node.LayoutId == layout.Id
                    && node.Type == NodeType.Default, ct)
                ?? throw new EntityNotFoundException(nameof(Node), "Folder not found in the requested layout.");

            if (requestedKeys.Count == 0)
            {
                return new SiblingNameLookupDto();
            }

            string[] keys = [.. requestedKeys];
            IQueryable<Node> nodes = _dbContext.Nodes.AsNoTracking()
                .Where(node => node.ParentId == parent.Id
                    && node.LayoutId == layout.Id
                    && node.OwnerId == request.UserId
                    && node.Type == NodeType.Default);
            IQueryable<NodeFile> files = _dbContext.NodeFiles.AsNoTracking()
                .Where(file => file.NodeId == parent.Id && file.OwnerId == request.UserId);

            List<NodeDto> matchingNodes = await nodes
                .Where(node => keys.Contains(node.NameKey))
                .Select(node => new NodeDto
                {
                    Id = node.Id,
                    CreatedAt = node.CreatedAt,
                    UpdatedAt = node.UpdatedAt,
                    LayoutId = node.LayoutId,
                    ParentId = node.ParentId,
                    Name = node.Name,
                    Metadata = node.Metadata!,
                })
                .ToListAsync(ct);
            List<FileNameMatchDto> matchingFiles = await files
                .Where(file => keys.Contains(file.NameKey))
                .Select(file => new FileNameMatchDto
                {
                    Id = file.Id,
                    CreatedAt = file.CreatedAt,
                    UpdatedAt = file.UpdatedAt,
                    Name = file.Name,
                })
                .ToListAsync(ct);

            bool needsTakenNames = request.IncludeTakenNamesOnConflict
                && (matchingNodes.Count > 0
                    || matchingFiles.Count > 0
                    || requestedKeys.Count < request.Names.Count);
            List<string> takenNameKeys = [];
            if (needsTakenNames)
            {
                takenNameKeys = await nodes.Select(node => node.NameKey)
                    .Concat(files.Select(file => file.NameKey))
                    .ToListAsync(ct);
            }

            return new SiblingNameLookupDto
            {
                Nodes = matchingNodes,
                Files = matchingFiles,
                TakenNameKeys = takenNameKeys,
            };
        }
    }
}
