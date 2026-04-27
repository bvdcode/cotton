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

        public async Task Execute(IJobExecutionContext context)
        {
            await Task.Delay(420_000); // Wait for 7 minutes for the server to start up and stabilize

            if (_perf.IsNightTime())
            {
                return;
            }

            var backend = _backendProvider.GetBackend();
            backend.CleanupTempFiles(_ttl);
            return;
        }
    }
}
