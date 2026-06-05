// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Runs the database integrity bridge backfill after the application is already accepting traffic.
/// </summary>
public sealed class DatabaseIntegrityBridgeBackfillHostedService(
    IServiceProvider _serviceProvider,
    IConfiguration _configuration,
    ILogger<DatabaseIntegrityBridgeBackfillHostedService> _logger) : BackgroundService
{
    private static readonly TimeSpan DefaultStartupDelay = TimeSpan.FromMinutes(2);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("DatabaseIntegrity:BridgeBackfill:Enabled", true))
        {
            _logger.LogWarning("Database integrity bridge backfill is disabled by configuration.");
            return;
        }

        TimeSpan startupDelay = TimeSpan.FromSeconds(
            Math.Max(0, _configuration.GetValue(
                "DatabaseIntegrity:BridgeBackfill:StartupDelaySeconds",
                (int)DefaultStartupDelay.TotalSeconds)));

        _logger.LogInformation(
            "Database integrity bridge background backfill scheduled; startup delay {StartupDelay}.",
            startupDelay);

        try
        {
            await Task.Delay(startupDelay, stoppingToken);

            Stopwatch stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Database integrity bridge background backfill starting.");

            using IServiceScope scope = _serviceProvider.CreateScope();
            var backfill = scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityBridgeBackfillService>();
            int signed = await backfill.BackfillUnsignedPhaseOneRowsAsync(stoppingToken);
            stopwatch.Stop();

            _logger.LogInformation(
                "Database integrity bridge background backfill completed; signed {SignedRows} rows in {Elapsed}.",
                signed,
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Database integrity bridge background backfill canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database integrity bridge background backfill failed.");
        }
    }
}
