// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Configuration;
using Microsoft.Extensions.Options;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Coordinates HLS work ownership and process-wide ffmpeg/ffprobe concurrency.
    /// </summary>
    public sealed class HlsTranscodeCoordinator
    {
        private readonly SemaphoreSlim _transcodeGate;
        private readonly SemaphoreSlim _probeGate;
        private readonly KeyedAsyncGate<string> _segmentGates =
            new(StringComparer.Ordinal);
        private readonly KeyedAsyncGate<Guid> _probeManifestGates = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="HlsTranscodeCoordinator"/> type.
        /// </summary>
        public HlsTranscodeCoordinator(IOptions<ResourceConcurrencyOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);
            ResourceConcurrencyOptions value = options.Value;
            value.Validate();
            _transcodeGate = new SemaphoreSlim(value.HlsTranscodes, value.HlsTranscodes);
            _probeGate = new SemaphoreSlim(value.HlsProbes, value.HlsProbes);
        }

        /// <summary>
        /// Serializes production of one exact HLS segment cache key.
        /// </summary>
        public ValueTask<IAsyncDisposable> EnterSegmentAsync(
            string cacheKey,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(cacheKey);
            return _segmentGates.EnterAsync(cacheKey, cancellationToken);
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

        /// <summary>
        /// Serializes media probing for one immutable file manifest.
        /// </summary>
        public ValueTask<IAsyncDisposable> EnterProbeManifestAsync(
            Guid manifestId,
            CancellationToken cancellationToken)
        {
            return _probeManifestGates.EnterAsync(manifestId, cancellationToken);
        }

        /// <summary>
        /// Waits for process-wide HLS media-probe capacity.
        /// </summary>
        public async ValueTask<IAsyncDisposable> EnterProbeAsync(
            CancellationToken cancellationToken)
        {
            await _probeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new AsyncGateLease(() => _probeGate.Release());
        }

        internal int SegmentGateCount => _segmentGates.Count;

        internal int ProbeManifestGateCount => _probeManifestGates.Count;

        private sealed class KeyedAsyncGate<TKey>(IEqualityComparer<TKey>? comparer = null)
            where TKey : notnull
        {
            private readonly object _lock = new();
            private readonly Dictionary<TKey, GateEntry> _gates = new(comparer);

            public int Count
            {
                get
                {
                    lock (_lock)
                    {
                        return _gates.Count;
                    }
                }
            }

            public async ValueTask<IAsyncDisposable> EnterAsync(
                TKey key,
                CancellationToken cancellationToken)
            {
                GateEntry entry;
                lock (_lock)
                {
                    if (!_gates.TryGetValue(key, out entry!))
                    {
                        entry = new GateEntry();
                        _gates.Add(key, entry);
                    }

                    entry.ReferenceCount++;
                }

                try
                {
                    await entry.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    return new AsyncGateLease(() => Release(key, entry));
                }
                catch
                {
                    ReleaseReference(key, entry);
                    throw;
                }
            }

            private void Release(TKey key, GateEntry entry)
            {
                entry.Gate.Release();
                ReleaseReference(key, entry);
            }

            private void ReleaseReference(TKey key, GateEntry entry)
            {
                lock (_lock)
                {
                    entry.ReferenceCount--;
                    if (entry.ReferenceCount != 0)
                    {
                        return;
                    }

                    _gates.Remove(key);
                    entry.Gate.Dispose();
                }
            }

            private sealed class GateEntry
            {
                public SemaphoreSlim Gate { get; } = new(1, 1);

                public int ReferenceCount { get; set; }
            }
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
