// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Models;
using Cotton.Server.Helpers;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Cotton.Server.Jobs
{
    /// <summary>
    /// Runs the scheduled collect performance maintenance task.
    /// </summary>
    [JobTrigger(days: 1)]
    public class CollectPerformanceJob(
        PerfTracker _perf,
        CottonDbContext _dbContext,
        SettingsProvider _settingsProvider,
        StoragePipelineProbeService _storagePipelineProbe,
        ILogger<CollectPerformanceJob> _logger) : IJob
    {
        /// <summary>
        /// Executes the scheduled Quartz job.
        /// </summary>
        public async Task Execute(IJobExecutionContext context)
        {
            await Task.Delay(360_000); // Wait for 6 minutes for the server to start up and stabilize

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

            StoragePipelineProbeResult? storagePipelineProbe = await TryRunStoragePipelineProbeAsync(settings.StorageType.ToString().ToLowerInvariant(), context.CancellationToken).ConfigureAwait(false);

            TelemetryRequest request = new()
            {
                InstanceId = settings.InstanceId,
                ServerUrl = settings.PublicBaseUrl,
                Nodes = await _dbContext.Nodes.CountAsync(),
                Users = await _dbContext.Users.CountAsync(),
                Files = await _dbContext.FileManifests.CountAsync(),
                Version = AppVersionHelpers.GetAppVersion() ?? "Unknown",
                StoragePipelineProbe = storagePipelineProbe,
            };
            using var httpClient = new HttpClient();
            await httpClient.PostAsJsonAsync(global::Cotton.Constants.CottonBridgeTelemetryUrl, request);
            _logger.LogInformation("CollectPerformanceJob completed - telemetry data was sent to Cotton Bridge");
        }

        private async Task<StoragePipelineProbeResult?> TryRunStoragePipelineProbeAsync(string storageBackend, CancellationToken cancellationToken)
        {
            try
            {
                return await _storagePipelineProbe.RunAsync(storageBackend, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Storage pipeline telemetry probe failed; sending telemetry without storage speed metrics.");
                return null;
            }
        }
    }
}
