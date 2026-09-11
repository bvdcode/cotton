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
        public Task<bool> ExistsAsync(string uid)
        {
            return _backendProvider.GetBackend().ExistsAsync(uid);
        }

        public Task<bool> DeleteAsync(string uid)
        {
            return _backendProvider.GetBackend().DeleteAsync(uid);
        }

        public Task<long> GetSizeAsync(string uid)
        {
            return _backendProvider.GetBackend().GetSizeAsync(uid);
        }

        public async Task<Stream> ReadAsync(string uid, PipelineContext? context = null)
        {
            IStorageBackend backend = _backendProvider.GetBackend();
            IOrderedEnumerable<IStorageProcessor> orderedProcessors = _processors.OrderBy(p => p.Priority);
            Stream currentStream = await backend.ReadAsync(uid);
            foreach (IStorageProcessor? processor in orderedProcessors)
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
                if (orderedProcessors.Length > 0
                    && await backend.ExistsAsync(uid).ConfigureAwait(false))
                {
                    _logger.LogDebug("File {Uid} deduplicated, skipping processor pipeline", uid);
                    return await backend.GetSizeAsync(uid).ConfigureAwait(false);
                }
                Stream currentStream = stream;
                foreach (IStorageProcessor? processor in orderedProcessors)
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
                return await backend.WriteAsync(uid, currentStream);
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
    }
}
