// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cotton.Storage.Pipelines
{
    public class FileStoragePipeline(
        ILogger<FileStoragePipeline> _logger,
        IStorageBackendProvider _backendProvider,
        IEnumerable<IStorageProcessor> _processors) : IStoragePipeline
    {
        private static readonly SemaphoreSlim _maxParallel = new(initialCount: Environment.ProcessorCount);

        public Task<bool> ExistsAsync(string uid)
        {
            return _backendProvider.GetBackend().ExistsAsync(uid);
        }

        public Task<bool> DeleteAsync(string uid)
        {
            return _backendProvider.GetBackend().DeleteAsync(uid);
        }

        public async Task<Stream> ReadAsync(string uid, PipelineContext? context = null)
        {
            var backend = _backendProvider.GetBackend();
            var orderedProcessors = _processors.OrderBy(p => p.Priority);
            Stream currentStream = await backend.ReadAsync(uid);
            foreach (var processor in orderedProcessors)
            {
                currentStream = await processor.ReadAsync(uid, currentStream, context);
                if (currentStream == Stream.Null)
                {
                    throw new InvalidOperationException($"Processor {processor} returned Stream.Null for UID {uid} but it should return a valid stream.");
                }
            }
            if (currentStream == Stream.Null)
            {
                throw new InvalidOperationException($"No registered processor could retrieve the file with UID {uid}");
            }
            return currentStream;
        }

        public async Task WriteAsync(string uid, Stream stream, PipelineContext? context = null)
        {
            await _maxParallel.WaitAsync().ConfigureAwait(false);
            try
            {
                var backend = _backendProvider.GetBackend();
                if (!_processors.Any())
                {
                    _logger.LogWarning("No storage processors are registered. Writing the stream directly to the backend.");
                }
                var orderedProcessors = _processors.OrderByDescending(p => p.Priority);
                Stream currentStream = stream;
                foreach (var processor in orderedProcessors)
                {
                    if (currentStream == Stream.Null)
                    {
                        throw new InvalidOperationException($"Processor BEFORE {processor} returned Stream.Null for UID {uid} but it should pass a valid stream to the next processor.");
                    }
                    currentStream = await processor.WriteAsync(uid, currentStream, context);
                }
                if (currentStream == Stream.Null)
                {
                    throw new InvalidOperationException($"No registered processor produced a valid stream to write for UID {uid}");
                }
                await backend.WriteAsync(uid, currentStream);
            }
            finally
            {
                _maxParallel.Release();
            }
        }
    }
}
