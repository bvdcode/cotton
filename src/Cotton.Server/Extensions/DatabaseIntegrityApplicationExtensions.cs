// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.DatabaseIntegrity;

namespace Cotton.Server.Extensions;

public static class DatabaseIntegrityApplicationExtensions
{
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
