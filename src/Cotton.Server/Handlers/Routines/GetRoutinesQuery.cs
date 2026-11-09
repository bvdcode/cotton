// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Nodes;
using Mapster;
using Cotton.Server.Providers;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Routines
{
    public record GetRoutinesQuery(Guid UserId) : IRequest<IReadOnlyList<RoutineDto>>;

    public class GetRoutinesQueryHandler(CottonDbContext db, SettingsProvider settings) : IRequestHandler<GetRoutinesQuery, IReadOnlyList<RoutineDto>>
    {
        public async Task<IReadOnlyList<RoutineDto>> Handle(GetRoutinesQuery request, CancellationToken ct)
        {
            List<Routine> routines = await db.Routines.AsNoTracking().Where(x => x.OwnerId == request.UserId)
                .OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(ct);
            List<RoutineRun> runs = await db.RoutineRuns.AsNoTracking().Where(x => x.Routine.OwnerId == request.UserId)
                .GroupBy(x => x.RoutineId).Select(x => x.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).First())
                .ToListAsync(ct);
            Dictionary<Guid, RoutineRun> latest = runs.ToDictionary(x => x.RoutineId);
            Guid[] targetIds = routines.Select(x => x.TargetNodeId).Distinct().ToArray();
            Dictionary<Guid, Node> folders = await db.Nodes.AsNoTracking()
                .Where(x => targetIds.Contains(x.Id) && x.OwnerId == request.UserId).ToDictionaryAsync(x => x.Id, ct);
            TimeZoneInfo timezone = settings.GetServerSettings().GetTimezoneInfo();
            return routines.Select(x =>
            {
                RoutineDto dto = RoutineDto.FromEntity(x, timezone, latest.GetValueOrDefault(x.Id));
                dto.TargetNode = folders.GetValueOrDefault(x.TargetNodeId)?.Adapt<NodeDto>();
                return dto;
            }).ToList();
        }
    }

}
