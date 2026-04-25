using Cotton.Database;
using Cotton.Models;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(days: 1)]
    public class CollectPerformanceJob(
        PerfTracker _perf,
        CottonDbContext _dbContext,
        SettingsProvider _settingsProvider,
        ILogger<CollectPerformanceJob> _logger) : IJob
    {
        private const string CloudTelemetryUrl = "https://cotton-gateway.splidex.com/api/v1/telemetry";

        public async Task Execute(IJobExecutionContext context)
        {
            var settings = _settingsProvider.GetServerSettings();
            if (!settings.TelemetryEnabled)
            {
                _logger.LogInformation("Performance data collection skipped: telemetry is disabled.");
                return;
            }

            if (_perf.IsUploading())
            {
                _logger.LogInformation("Performance data collection skipped: upload in progress.");
                return;
            }

            // TODO: Collect metrics and send to monitoring system, if user has opted in to telemetry.
            TelemetryRequest request = new()
            {
                InstanceId = settings.InstanceId,
                ServerUrl = settings.PublicBaseUrl,
                Nodes = await _dbContext.Nodes.CountAsync(),
                Users = await _dbContext.Users.CountAsync(),
                Files = await _dbContext.FileManifests.CountAsync(),
            };
            using var httpClient = new HttpClient();
            await httpClient.PostAsJsonAsync(CloudTelemetryUrl, request);
            _logger.LogInformation("CollectPerformanceJob completed - telemetry data was sent to Cotton Cloud");
        }
    }
}
