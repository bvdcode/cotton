// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

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

        try
        {
            await Task.Delay(startupDelay, stoppingToken);

            using IServiceScope scope = _serviceProvider.CreateScope();
            var backfill = scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityBridgeBackfillService>();
            int signed = await backfill.BackfillUnsignedPhaseOneRowsAsync(stoppingToken);
            _logger.LogInformation(
                "Database integrity bridge background backfill completed; signed {SignedRows} rows.",
                signed);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database integrity bridge background backfill failed.");
        }
    }
}
