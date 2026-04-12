using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using EasyExtensions.Quartz.Attributes;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(hours: 36)]
    public class ClearTempFolderJob(PerfTracker _perf, IStorageBackendProvider _backendProvider) : IJob
    {
        private static readonly TimeSpan _ttl = TimeSpan.FromHours(1);

        public Task Execute(IJobExecutionContext context)
        {
            if (_perf.IsNightTime())
            {
                return Task.CompletedTask;
            }

            var backend = _backendProvider.GetBackend();
            backend.CleanupTempFiles(_ttl);
            return Task.CompletedTask;
        }
    }
}
