// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Pipelines;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Storage.Tests.Pipelines
{
    public partial class FileStoragePipelineTests
    {
        [Test]
        public async Task Overwrite_ReprocessesExistingKey_WhileNormalWriteDeduplicates()
        {
            FakeStorageBackend backend = new();
            CountingProcessor counter = new();
            FileStoragePipeline pipeline = new(NullLogger<FileStoragePipeline>.Instance,
                new FakeBackendProvider(backend), [counter, new MarkerProcessor(1, 42)], new StorageWriteAdmissionGate(1));
            await pipeline.WriteAsync("pointer", new MemoryStream([1]));
            await pipeline.WriteAsync("pointer", new MemoryStream([2]));
            Assert.That(counter.WriteCalls, Is.EqualTo(1));
            await pipeline.WriteAsync("pointer", new MemoryStream([3]), new PipelineContext { Overwrite = true });
            Assert.That(counter.WriteCalls, Is.EqualTo(2));
            await using Stream stream = await pipeline.ReadAsync("pointer");
            using MemoryStream content = new();
            await stream.CopyToAsync(content);
            Assert.That(content.ToArray(), Is.EqualTo(new byte[] { 3 }));
        }
    }
}
