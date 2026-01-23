using Cotton.Storage.Abstractions;
using EasyExtensions.Quartz.Attributes;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(hours: 36)]
    public class ClearTempFolderJob(IStorageBackendProvider _backendProvider) : IJob
    {
        private static readonly TimeSpan _ttl = TimeSpan.FromHours(1);

        public Task Execute(IJobExecutionContext context)
        {
            var backend = _backendProvider.GetBackend();
            backend.CleanupTempFiles(_ttl);
            return Task.CompletedTask;
        }
    }
}
