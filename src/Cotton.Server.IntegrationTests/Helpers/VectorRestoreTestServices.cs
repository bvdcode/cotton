// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Server;
using EasyExtensions.EntityFrameworkCore.Npgsql.Models;
using EasyExtensions.Mediator;
using Quartz;

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal static class VectorRestoreTestServices
    {
        public static ServiceProvider Create(CottonDbContext context)
        {
            Quartz.Logging.LogContext.SetCurrentLogProvider(NullLoggerFactory.Instance);
            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            services.AddSingleton(context);
            services.AddMediator();
            services.AddTransient<IRequestHandler<RestoreVectorSearchRequest, Models.DatabaseBackup.VectorSearchRestoreStatus>, RestoreVectorSearchRequestHandler>();
            services.AddTransient<IRequestHandler<EnsureVectorExtensionRequest>, EnsureVectorExtensionRequestHandler>();
            services.AddTransient<IRequestHandler<GetVectorIndexMetadataQuery, PostgresIndexStatus>, GetVectorIndexMetadataQueryHandler>();
            services.AddTransient<IRequestHandler<BuildVectorIndexRequest, string?>, BuildVectorIndexRequestHandler>();
            services.AddQuartz(options =>
            {
                options.SchedulerName = $"VectorRestoreTests-{Guid.NewGuid():N}";
                options.AddJob<BuildVectorIndexJob>(job => job.WithIdentity(nameof(BuildVectorIndexJob)).StoreDurably());
            });
            return services.BuildServiceProvider();
        }
    }
}
