// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Providers;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Routines
{
    public record GetRoutineRunsQuery(Guid UserId, Guid RoutineId) : IRequest<IReadOnlyList<RoutineRunDto>>;

    public class GetRoutineRunsQueryHandler(CottonDbContext db) : IRequestHandler<GetRoutineRunsQuery, IReadOnlyList<RoutineRunDto>>
    {
        public async Task<IReadOnlyList<RoutineRunDto>> Handle(GetRoutineRunsQuery request, CancellationToken ct)
        {
            if (!await db.Routines.AnyAsync(x => x.Id == request.RoutineId && x.OwnerId == request.UserId, ct))
            {
                throw new EntityNotFoundException<Routine>("Routine not found.");
            }
            List<RoutineRun> runs = await db.RoutineRuns.AsNoTracking().Where(x => x.RoutineId == request.RoutineId)
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(50).ToListAsync(ct);
            return runs.Select(RoutineRunDto.FromEntity).ToList();
        }
    }
}
