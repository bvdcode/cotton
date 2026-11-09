// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Files;
using Cotton.Routines;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Cotton.Server.Handlers.Routines
{
    public record ExecuteRoutineRequest(Guid RoutineId, bool Force = false) : IRequest;

    public class ExecuteRoutineRequestHandler(CottonDbContext db, IMediator mediator, IServiceScopeFactory scopes,
        NodeSubtreeService subtree, SettingsProvider settings, IDatabaseIntegrityVerifier integrity,
        ILogger<ExecuteRoutineRequestHandler> logger) : IRequestHandler<ExecuteRoutineRequest>
    {
        private const int BatchSize = 500;

        public async Task Handle(ExecuteRoutineRequest request, CancellationToken ct)
        {
            Routine? routine = await db.Routines.SingleOrDefaultAsync(x => x.Id == request.RoutineId, ct);
            if (routine is null || !routine.Enabled)
            {
                return;
            }
            integrity.RequireValid(db, routine, "routine-execution");
            DateTime now = DateTime.UtcNow;
            DateTime? next = RoutineSchedule.NextExecution(routine, settings.GetServerSettings().GetTimezoneInfo());
            bool force = request.Force && !routine.LastExecutionAt.HasValue;
            if (!force && (!next.HasValue || next > now))
            {
                return;
            }
            routine.LastExecutionAt = now;
            RoutineRun run = new() { RoutineId = routine.Id, Status = RoutineRunStatus.Running };
            db.RoutineRuns.Add(run);
            await db.SaveChangesAsync(ct);
            await PublishAsync(routine.OwnerId, run, ct);
            try
            {
                bool exists = await db.Nodes.AnyAsync(x => x.Id == routine.TargetNodeId && x.OwnerId == routine.OwnerId
                    && x.Type == NodeType.Default && x.Layout.IsActive, ct);
                if (!exists)
                {
                    run.Error = "WorkingFolderUnavailable";
                    run.Status = RoutineRunStatus.Failed;
                }
                else
                {
                    switch (routine.Type)
                    {
                        case RoutineType.DeleteFiles:
                            await DeleteFilesAsync(routine, run, now, ct);
                            run.Status = RoutineRunStatus.Completed;
                            break;
                        default:
                            throw new InvalidOperationException("Unsupported routine type.");
                    }
                }
            }
            catch (OperationCanceledException exception) when (ct.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Routine {RoutineId} was interrupted.", routine.Id);
                run.Status = RoutineRunStatus.Interrupted;
                run.Error = "Interrupted";
                run.FinishedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(CancellationToken.None);
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Routine {RoutineId} failed after processing {ProcessedCount} files.", routine.Id, run.ProcessedCount);
                run.Status = RoutineRunStatus.Failed;
                run.Error = "ExecutionFailed";
            }
            run.FinishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await PublishAsync(routine.OwnerId, run, ct);
        }

        private async Task DeleteFilesAsync(Routine routine, RoutineRun run, DateTime now, CancellationToken ct)
        {
            DeleteFilesRoutineSettings policy = JsonSerializer.Deserialize<DeleteFilesRoutineSettings>(routine.Settings)
                ?? throw new InvalidOperationException("Routine settings are required.");
            System.ComponentModel.DataAnnotations.Validator.ValidateObject(policy,
                new System.ComponentModel.DataAnnotations.ValidationContext(policy), true);
            Guid[] nodeIds = policy.IncludeSubfolders
                ? (await subtree.CollectSubtreeIdsAsync(routine.OwnerId, routine.TargetNodeId, ct)).ToArray()
                : [routine.TargetNodeId];
            DateTime cutoff = now.AddDays(-policy.OlderThanDays);
            IQueryable<NodeFile> query = db.NodeFiles.AsNoTracking().Where(x => x.OwnerId == routine.OwnerId
                && nodeIds.Contains(x.NodeId) && x.Node.Type == NodeType.Default
                && (x.OriginalNodeFileId == Guid.Empty || x.Id == x.OriginalNodeFileId));
            query = policy.DateField switch
            {
                RoutineDateField.CreatedAt => query.Where(x => x.CreatedAt < cutoff),
                RoutineDateField.UpdatedAt => query.Where(x => x.UpdatedAt < cutoff),
                _ => throw new InvalidOperationException("Unsupported date field."),
            };
            Guid? cursor = null;
            while (true)
            {
                IQueryable<NodeFile> page = query;
                if (cursor.HasValue)
                {
                    page = page.Where(x => x.Id.CompareTo(cursor.Value) > 0);
                }
                var candidates = await page.OrderBy(x => x.Id).Select(x => new { x.Id, x.NodeId }).Take(BatchSize).ToArrayAsync(ct);
                Guid[] ids = candidates.Select(x => x.Id).ToArray();
                if (ids.Length == 0)
                {
                    break;
                }
                List<BatchItemRequestDto> items = ids.Select(id => new BatchItemRequestDto { Id = id, Kind = BatchItemKind.File }).ToList();
                await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                IMediator deletion = scope.ServiceProvider.GetRequiredService<IMediator>();
                IReadOnlyList<BatchItemResultDto> result = await deletion.Send(new DeleteBatchItemsRequest(routine.OwnerId,
                    items, policy.PermanentlyDelete, new FileDeletionCondition(routine.TargetNodeId,
                        policy.IncludeSubfolders, policy.DateField, cutoff)), ct);
                run.ProcessedCount += result.Count;
                run.DeletedCount += result.Count(x => x.Deleted);
                await db.SaveChangesAsync(ct);
                IEventNotificationService events = scope.ServiceProvider.GetRequiredService<IEventNotificationService>();
                Dictionary<Guid, Guid> parents = candidates.ToDictionary(x => x.Id, x => x.NodeId);
                foreach (BatchItemResultDto item in result.Where(x => x.Deleted))
                {
                    await events.NotifyFileDeletedAsync(routine.OwnerId, item.Id, parents[item.Id], ct);
                }
                await PublishAsync(routine.OwnerId, run, ct);
                cursor = ids[^1];
            }
        }

        private Task PublishAsync(Guid userId, RoutineRun run, CancellationToken ct)
            => mediator.Send(new PublishRoutineRunRequest(userId, RoutineRunDto.FromEntity(run)), ct);
    }
}
