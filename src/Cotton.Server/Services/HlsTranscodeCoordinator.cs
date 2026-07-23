// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Configuration;
using Microsoft.Extensions.Options;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Coordinates HLS segment ownership and process-wide ffmpeg concurrency.
    /// </summary>
    public sealed class HlsTranscodeCoordinator
    {
        private readonly SemaphoreSlim _transcodeGate;
        private readonly object _segmentGatesLock = new();
        private readonly Dictionary<string, SegmentGateEntry> _segmentGates =
            new(StringComparer.Ordinal);

        /// <summary>
        /// Initializes a new instance of the <see cref="HlsTranscodeCoordinator"/> type.
        /// </summary>
        public HlsTranscodeCoordinator(IOptions<ResourceConcurrencyOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);
            ResourceConcurrencyOptions value = options.Value;
            value.Validate();
            _transcodeGate = new SemaphoreSlim(value.HlsTranscodes, value.HlsTranscodes);
        }

        /// <summary>
        /// Serializes production of one exact HLS segment cache key.
        /// </summary>
        public async ValueTask<IAsyncDisposable> EnterSegmentAsync(
            string cacheKey,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(cacheKey);

            SegmentGateEntry entry;
            lock (_segmentGatesLock)
            {
                if (!_segmentGates.TryGetValue(cacheKey, out entry!))
                {
                    entry = new SegmentGateEntry();
                    _segmentGates.Add(cacheKey, entry);
                }

                entry.ReferenceCount++;
            }

            try
            {
                await entry.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                return new AsyncGateLease(() => ReleaseSegment(cacheKey, entry));
            }
            catch
            {
                ReleaseSegmentReference(cacheKey, entry);
                throw;
            }
        }

        /// <summary>
        /// Waits for process-wide HLS transcode capacity.
        /// </summary>
        public async ValueTask<IAsyncDisposable> EnterTranscodeAsync(
            CancellationToken cancellationToken)
        {
            await _transcodeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new AsyncGateLease(() => _transcodeGate.Release());
        }

        internal int SegmentGateCount
        {
            get
            {
                lock (_segmentGatesLock)
                {
                    return _segmentGates.Count;
                }
            }
        }

        private void ReleaseSegment(string cacheKey, SegmentGateEntry entry)
        {
            entry.Gate.Release();
            ReleaseSegmentReference(cacheKey, entry);
        }

        private void ReleaseSegmentReference(string cacheKey, SegmentGateEntry entry)
        {
            lock (_segmentGatesLock)
            {
                entry.ReferenceCount--;
                if (entry.ReferenceCount != 0)
                {
                    return;
                }

                _segmentGates.Remove(cacheKey);
                entry.Gate.Dispose();
            }
        }

        private sealed class SegmentGateEntry
        {
            public SemaphoreSlim Gate { get; } = new(1, 1);

            public int ReferenceCount { get; set; }
        }

        private sealed class AsyncGateLease(Action release) : IAsyncDisposable
        {
            private Action? _release = release;

            public ValueTask DisposeAsync()
            {
                Interlocked.Exchange(ref _release, null)?.Invoke();
                return ValueTask.CompletedTask;
            }
        }
    }
}
