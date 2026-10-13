// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Nodes
{
    public record CaptureTrashParentsQuery(Guid UserId, Guid ParentId) : IRequest<IReadOnlyList<TrashParent>>;

    public class CaptureTrashParentsQueryHandler(IMediator _mediator)
        : IRequestHandler<CaptureTrashParentsQuery, IReadOnlyList<TrashParent>>
    {
        public async Task<IReadOnlyList<TrashParent>> Handle(CaptureTrashParentsQuery request, CancellationToken ct)
        {
            IReadOnlyDictionary<Guid, IReadOnlyList<TrashParent>> parents = await _mediator.Send(
                new CaptureBatchTrashParentsQuery(request.UserId, [request.ParentId]), ct);
            return parents[request.ParentId];
        }
    }
}
