// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal static class DatabaseIntegrityTestSignatures
    {
        public static async Task SetVersionAsync<T>(CottonDbContext dbContext, T entity, int version, IServiceProvider services)
            where T : class
        {
            IDatabaseIntegrityDescriptor<T> descriptor = services.GetRequiredService<IDatabaseIntegrityDescriptorRegistry>().Get<T>(version);
            using HMACSHA256 hmac = services.GetRequiredService<DatabaseIntegrityKeyProvider>().CreateHmac();
            byte[] mac = hmac.ComputeHash(descriptor.BuildCanonicalPayload(entity));
            Guid id = dbContext.Entry(entity).Property<Guid>("Id").CurrentValue;
            await dbContext.Set<T>().Where(row => EF.Property<Guid>(row, "Id") == id).ExecuteUpdateAsync(setters => setters
                .SetProperty(row => EF.Property<int?>(row, DatabaseIntegrityColumns.VersionProperty), version)
                .SetProperty(row => EF.Property<byte[]?>(row, DatabaseIntegrityColumns.MacProperty), mac));
        }
    }
}
