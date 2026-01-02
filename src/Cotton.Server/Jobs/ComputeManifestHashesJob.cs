using EasyExtensions.Quartz.Attributes;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(hours: 1)]
    public class ComputeManifestHashesJob : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            // TODO: Implement manifest hash computation logic here
            return Task.CompletedTask;
        }
    }
}
