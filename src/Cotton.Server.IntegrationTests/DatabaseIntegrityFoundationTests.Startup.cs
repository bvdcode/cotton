// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using Cotton.Server.Services.Startup;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public partial class DatabaseIntegrityFoundationTests
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
