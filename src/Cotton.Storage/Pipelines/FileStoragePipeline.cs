// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cotton.Storage.Pipelines
{
    public class FileStoragePipeline(
        ILogger<FileStoragePipeline> _logger,
        IStorageBackendProvider _backendProvider,
        IEnumerable<IStorageProcessor> _processors,
        StorageWriteAdmissionGate _writeAdmissionGate) : IStoragePipeline
    {
        public Task<bool> ExistsAsync(string uid, CancellationToken cancellationToken = default)
        {
            return _backendProvider.GetBackend().ExistsAsync(uid, cancellationToken);
        }

        public Task<bool> DeleteAsync(string uid, CancellationToken cancellationToken = default)
        {
            return _backendProvider.GetBackend().DeleteAsync(uid, cancellationToken);
        }

        public Task<long> GetSizeAsync(string uid, CancellationToken cancellationToken = default)
        {
            return _backendProvider.GetBackend().GetSizeAsync(uid, cancellationToken);
        }

        public async Task<Stream> ReadAsync(string uid, PipelineContext? context = null, CancellationToken cancellationToken = default)
        {
            IStorageBackend backend = _backendProvider.GetBackend();
            IOrderedEnumerable<IStorageProcessor> orderedProcessors = _processors.OrderBy(p => p.Priority);
            Stream currentStream = await backend.ReadAsync(uid, cancellationToken);
            try
            {
                foreach (IStorageProcessor processor in orderedProcessors)
                {
                    Stream next = await processor.ReadAsync(uid, currentStream, context, cancellationToken);
                    if (next == Stream.Null)
                    {
                        throw new InvalidOperationException($"Processor {processor} returned Stream.Null for UID {uid}.");
                    }
                    currentStream = next;
                }
                return currentStream;
            }
            catch
            {
                await currentStream.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        public async Task<long> WriteAsync(
            string uid,
            Stream stream,
            PipelineContext? context = null,
            CancellationToken cancellationToken = default)
        {
            await _writeAdmissionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                IStorageBackend backend = _backendProvider.GetBackend();
                IStorageProcessor[] orderedProcessors = _processors.OrderByDescending(p => p.Priority).ToArray();
                if (orderedProcessors.Length == 0)
                {
                    _logger.LogWarning("No storage processors are registered. Writing the stream directly to the backend.");
                }
                if (context?.Overwrite != true && orderedProcessors.Length > 0
                    && await backend.ExistsAsync(uid, cancellationToken).ConfigureAwait(false))
                {
                    _logger.LogDebug("File {Uid} deduplicated, skipping processor pipeline", uid);
                    return await backend.GetSizeAsync(uid, cancellationToken).ConfigureAwait(false);
                }
                Stream currentStream = stream;
                try
                {
                    foreach (IStorageProcessor processor in orderedProcessors)
                    {
                        Stream next = await processor.WriteAsync(uid, currentStream, context, cancellationToken);
                        if (next == Stream.Null)
                        {
                            throw new InvalidOperationException($"Processor {processor} returned Stream.Null for UID {uid}.");
                        }
                        currentStream = next;
                    }
                    return await backend.WriteAsync(uid, currentStream, context?.Overwrite == true, cancellationToken);
                }
                finally
                {
                    if (!ReferenceEquals(currentStream, stream))
                    {
                        await currentStream.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                _writeAdmissionGate.Release();
            }
        }

        public IAsyncEnumerable<string> ListAllKeysAsync(CancellationToken ct = default)
        {
            return _backendProvider.GetBackend().ListAllKeysAsync(ct);
        }

        public IAsyncEnumerable<string> ListKeysByPrefixAsync(char prefix, CancellationToken ct = default)
        {
            return _backendProvider.GetBackend().ListKeysByPrefixAsync(prefix, ct);
        }
    }
}
