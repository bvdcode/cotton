using Quartz;
using Cotton.Server.Abstractions;
using EasyExtensions.Quartz.Attributes;

namespace Cotton.Server.Jobs
{
    [JobTrigger(days: 7)]
    public class DumpDatabaseJob(IPostgresDumpService _dumper, ILogger<DumpDatabaseJob> _logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {

        }
    }
}
