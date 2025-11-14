// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Cotton.Storage.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cotton.Storage.Pipelines
{
    /*
     * Idea:
     * L1: Memory Cache (RAM)
     * L2: Compression
     * L3: Encryption
     * L4: Fast Storage Cache (SSD)
     * L5: Cold Storage Cache (HDD, Cloud, S3 etc.)
     */

    public class FileStoragePipeline(
        ILogger<FileStoragePipeline> _logger,
        IEnumerable<IStorageProcessor> _processors) : IStoragePipeline
    {
        public async Task<Stream> ReadAsync(string uid)
        {
            var orderedProcessors = _processors.OrderBy(p => p.Priority);
            Stream currentStream = Stream.Null;
            foreach (var processor in orderedProcessors)
            {
                currentStream = await processor.ReadAsync(uid, currentStream);
                _logger.LogDebug("Processor {Processor} processed stream for UID {UID}", processor, uid);
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

        public async Task WriteAsync(string uid, Stream stream)
        {
            var orderedProcessors = _processors.OrderByDescending(p => p.Priority);
            Stream currentStream = stream;
            foreach (var processor in orderedProcessors)
            {
                if (currentStream == Stream.Null)
                {
                    throw new InvalidOperationException($"Processor BEFORE {processor} returned Stream.Null for UID {uid} but it should pass a valid stream to the next processor.");
                }
                currentStream = await processor.WriteAsync(uid, currentStream);
                _logger.LogDebug("Processor {Processor} processed stream for UID {UID}", processor, uid);
            }
            if (currentStream != Stream.Null)
            {
                throw new InvalidOperationException($"The final stream after writing should be Stream.Null for UID {uid}");
            }
        }
    }
}
