// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.Startup;

using static Cotton.Server.IntegrationTests.Helpers.DatabaseIntegrityTestData;

namespace Cotton.Server.IntegrationTests
{
    public class DatabaseIntegrityStartupTests
    {
        [Test]
        public void StartupCheck_RejectsDescriptorAndModelMismatch()
        {
            using CottonDbContext dbContext = CreateDbContext();
            DatabaseIntegrityDescriptorRegistry descriptors = new([new NodeFileIntegrityDescriptor()]);
            DatabaseIntegrityStartupCheck check = new(dbContext, descriptors);

            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await check.ValidateAsync(CancellationToken.None));

            Assert.That(exception!.Message, Does.Contain("Missing descriptors"));
        }
    }
}
