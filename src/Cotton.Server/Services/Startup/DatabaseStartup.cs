// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Providers;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services.Startup
{
    internal static class DatabaseStartup
    {
        public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            using (IServiceScope migrationScope = services.CreateScope())
            {
                CottonDbContext dbContext = migrationScope.ServiceProvider.GetRequiredService<CottonDbContext>();
                ILogger logger = migrationScope.ServiceProvider.GetRequiredService<ILogger<CottonDbContext>>();
                await ApplyMigrationsAsync(dbContext, logger, cancellationToken);
            }

            using (IServiceScope scope = services.CreateScope())
            {
                DatabaseAutoRestoreService autoRestore = scope.ServiceProvider.GetRequiredService<DatabaseAutoRestoreService>();
                await autoRestore.TryRestoreIfEmptyAsync(cancellationToken);
                MasterKeyStartupValidator masterKeyValidator = scope.ServiceProvider
                    .GetRequiredService<MasterKeyStartupValidator>();
                await masterKeyValidator.ValidateAsync(cancellationToken);
#pragma warning disable CS0618 // TEMPORARY 0.5 RECOVERY: remove after the upgrade window.
                await scope.ServiceProvider.GetRequiredService<LegacyZeroKeySettingsRecovery>()
                    .RepairAsync(scope.ServiceProvider);
#pragma warning restore CS0618
                scope.ServiceProvider.GetRequiredService<SettingsProvider>().GetServerSettings();
            }
        }

        private static async Task ApplyMigrationsAsync(
            CottonDbContext dbContext, ILogger logger, CancellationToken cancellationToken)
        {
            IEnumerable<string> migrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
            string[] pending = migrations.ToArray();
            if (pending.Length == 0)
            {
                return;
            }

            foreach (string migration in pending)
            {
                logger.LogInformation("Applying migration {Migration}.", migration);
            }

            await dbContext.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Migrations applied.");
        }
    }
}
