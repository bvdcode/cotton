// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.DatabaseIntegrity;

namespace Cotton.Server.Extensions;

/// <summary>
/// Contains extension methods for configuring database integrity application.
/// </summary>
public static class DatabaseIntegrityApplicationExtensions
{
    /// <summary>
    /// Applies database integrity bridge backfill.
    /// </summary>
    public static async Task ApplyDatabaseIntegrityBridgeBackfillAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        using IServiceScope scope = app.Services.CreateScope();
        var backfill = scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityBridgeBackfillService>();
        await backfill.BackfillUnsignedPhaseOneRowsAsync(cancellationToken);
    }
}
