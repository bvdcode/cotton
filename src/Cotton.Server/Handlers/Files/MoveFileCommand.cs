// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Handlers.Moves;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.Files
{
    public class MoveFileCommand : IRequest<NodeFileManifestDto>
    {
        public Guid NodeFileId { get; set; }

        public Guid ParentId { get; set; }

        public string? Name { get; set; }

        public Dictionary<string, string?>? Metadata { get; set; }

        public bool Overwrite { get; set; }

        public Guid UserId { get; set; }

        public string? ExpectedETag { get; set; }
    }

    public class MoveFileCommandHandler(
        CottonDbContext db,
        IMediator mediator,
        IEventNotificationService notifications,
        ILayoutMutationGate layoutGate,
        CommittedMutationEffects effects) : IRequestHandler<MoveFileCommand, NodeFileManifestDto>
    {
        public async Task<NodeFileManifestDto> Handle(MoveFileCommand request, CancellationToken ct)
        {
            if (request.ParentId == Guid.Empty)
            {
                throw new BadRequestException<NodeFile>("Target parent id is required.");
            }
            Guid layoutId = await db.NodeFiles.AsNoTracking()
                .Where(file => file.Id == request.NodeFileId && file.OwnerId == request.UserId)
                .Select(file => (Guid?)file.Node.LayoutId)
                .SingleOrDefaultAsync(ct)
                ?? throw new EntityNotFoundException<NodeFile>("Source file not found.");

            await using IAsyncDisposable gate = await layoutGate.EnterAsync(layoutId, ct);
            await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(ct);
            NodeFile file = await db.NodeFiles.Include(file => file.Node).Include(file => file.FileManifest)
                .SingleOrDefaultAsync(file => file.Id == request.NodeFileId && file.OwnerId == request.UserId, ct)
                ?? throw new EntityNotFoundException<NodeFile>("Source file not found.");
            if (file.Node.Type != NodeType.Default)
            {
                throw new EntityNotFoundException<NodeFile>("Source file is not in the active layout.");
            }
            if (!FileETags.MatchesIfMatchHeader(request.ExpectedETag, file))
            {
                throw new FilePreconditionFailedException<NodeFile>("File content changed before move.");
            }
            if (file.NodeId == request.ParentId)
            {
                return file.Adapt<NodeFileManifestDto>();
            }
            Node parent = await db.Nodes.SingleOrDefaultAsync(node => node.Id == request.ParentId
                && node.OwnerId == request.UserId && node.Type == NodeType.Default, ct)
                ?? throw new EntityNotFoundException<Node>("Destination folder not found in the active layout.");
            Guid oldParentId = file.NodeId;
            MoveOverwritePolicy overwrite = MoveOverwritePolicy.None;
            if (request.Overwrite)
            {
                overwrite = MoveOverwritePolicy.Files;
            }
            await mediator.Send(new RelocateFileRequest(file, parent, request.Name, request.Metadata, overwrite), ct);
            await transaction.CommitAsync(ct);
            await effects.RunAsync(nameof(IEventNotificationService.NotifyFileMovedAsync),
                () => notifications.NotifyFileMovedAsync(file.Id, oldParentId, CancellationToken.None));
            return file.Adapt<NodeFileManifestDto>();
        }
    }
}
