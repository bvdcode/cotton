using Cotton.Server.Jobs;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using EasyExtensions.Quartz.Extensions;
using Quartz;

namespace Cotton.Server.Handlers.Server
{
    public class TriggerDatabaseBackupRequest : IRequest
    {
    }

    public class TriggerDatabaseBackupRequestHandler(ISchedulerFactory _scheduler) : IRequestHandler<TriggerDatabaseBackupRequest>
    {
        public async Task Handle(TriggerDatabaseBackupRequest request, CancellationToken cancellationToken)
        {
            await _scheduler.TriggerJobAsync<DumpDatabaseJob>();
        }
    }
}
