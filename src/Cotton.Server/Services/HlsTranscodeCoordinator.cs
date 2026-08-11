// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Configuration;
using Microsoft.Extensions.Options;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Coordinates HLS work ownership and process-wide ffmpeg/ffprobe concurrency.
    /// </summary>
    public class HlsTranscodeCoordinator
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
    }
}
