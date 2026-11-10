// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Routines;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.Routines
{
    public record DeleteRoutineRequest(Guid UserId, Guid RoutineId) : IRequest;

    public class DeleteRoutineRequestHandler(CottonDbContext db) : IRequestHandler<DeleteRoutineRequest>
    {
        public async Task Handle(DeleteRoutineRequest request, CancellationToken ct)
        {
            Routine routine = await db.Routines.SingleOrDefaultAsync(x => x.Id == request.RoutineId && x.OwnerId == request.UserId, ct)
                ?? throw new EntityNotFoundException<Routine>("Routine not found.");
            if (await db.RoutineRuns.AnyAsync(x => x.RoutineId == routine.Id && x.Status == RoutineRunStatus.Running, ct))
            {
                throw new BadRequestException<Routine>("The routine is running.");
            }
            await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
            db.RoutineRuns.RemoveRange(await db.RoutineRuns.Where(x => x.RoutineId == routine.Id).ToListAsync(ct));
            await db.SaveChangesAsync(ct);
            db.Routines.Remove(routine);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
    }
}
