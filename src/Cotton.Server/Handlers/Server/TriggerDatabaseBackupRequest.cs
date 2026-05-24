// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Jobs;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using EasyExtensions.Quartz.Extensions;
using Quartz;

namespace Cotton.Server.Handlers.Server
{
    /// <summary>
    /// Represents the trigger database backup request request payload accepted by the API.
    /// </summary>
    public class TriggerDatabaseBackupRequest : IRequest
    {
    }

    /// <summary>
    /// Handles trigger database backup requests in the mediator pipeline.
    /// </summary>
    public class TriggerDatabaseBackupRequestHandler(ISchedulerFactory _scheduler) : IRequestHandler<TriggerDatabaseBackupRequest>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task Handle(TriggerDatabaseBackupRequest request, CancellationToken cancellationToken)
        {
            await _scheduler.TriggerJobAsync<DumpDatabaseJob>();
        }
    }
}
