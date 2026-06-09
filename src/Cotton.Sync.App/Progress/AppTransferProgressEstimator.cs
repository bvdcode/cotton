// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Progress
{
    /// <summary>
    /// Calculates rolling speed and remaining-time estimates for one sync-pair transfer stream.
    /// </summary>
    public sealed class AppTransferProgressEstimator
    {
        private static readonly TimeSpan RollingWindow = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan MinimumEstimateSampleDuration = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaximumInitialZeroBaselineAge = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan SpeedSmoothingTimeConstant = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan RemainingTimeSmoothingTimeConstant = TimeSpan.FromSeconds(10);
        private const long UnknownTotalEstimateTransferredBytes = 64L * 1024;
        private const long MaximumEstimateTransferredBytes = 1024L * 1024;
        private const long EstimateTransferredBytesDivisor = 100;
        private const int MaximumSamples = 2048;
        private readonly Queue<TransferSample> _samples = new();
        private SyncTransferDirection _direction = SyncTransferDirection.Unknown;
        private double? _smoothedSpeedBytesPerSecond;
        private TimeSpan? _smoothedEstimatedTimeRemaining;
        private DateTime? _lastSpeedOccurredAtUtc;
        private DateTime? _lastEstimateOccurredAtUtc;
        private string _relativePath = string.Empty;

        /// <summary>
        /// Adds one transfer-progress sample and returns the current rolling estimate.
        /// </summary>
        public AppTransferProgressEstimate AddSample(
            SyncTransferDirection direction,
            string relativePath,
            long transferredBytes,
            long? totalBytes,
            bool isCompleted,
            DateTime occurredAtUtc)
        {
            if (direction == SyncTransferDirection.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), "Transfer direction must be known.");
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
            ArgumentOutOfRangeException.ThrowIfNegative(transferredBytes);
            DateTime normalizedOccurredAtUtc = occurredAtUtc.ToUniversalTime();
            string normalizedPath = relativePath.Trim();
            if (direction != _direction || !string.Equals(normalizedPath, _relativePath, StringComparison.Ordinal))
            {
                Reset(direction, normalizedPath);
            }

            if (_samples.TryPeek(out TransferSample firstSample) && transferredBytes < firstSample.TransferredBytes)
            {
                Reset(direction, normalizedPath);
            }

            var currentSample = new TransferSample(transferredBytes, normalizedOccurredAtUtc);
            _samples.Enqueue(currentSample);
            PruneNonProgressSamples();
            PruneSamples(currentSample.OccurredAtUtc);
            if (TryUseCurrentSampleAsBaselineAfterStaleZero(currentSample))
            {
                return new AppTransferProgressEstimate(null, null);
            }

            AppTransferProgressEstimate estimate = CreateEstimate(currentSample, totalBytes, isCompleted);
            if (isCompleted)
            {
                _samples.Clear();
                _smoothedSpeedBytesPerSecond = null;
                _smoothedEstimatedTimeRemaining = null;
                _lastSpeedOccurredAtUtc = null;
                _lastEstimateOccurredAtUtc = null;
            }

            return estimate;
        }

        private void Reset(SyncTransferDirection direction, string relativePath)
        {
            _samples.Clear();
            _smoothedSpeedBytesPerSecond = null;
            _smoothedEstimatedTimeRemaining = null;
            _lastSpeedOccurredAtUtc = null;
            _lastEstimateOccurredAtUtc = null;
            _direction = direction;
            _relativePath = relativePath;
        }

        private void PruneSamples(DateTime occurredAtUtc)
        {
            while (_samples.Count > MaximumSamples)
            {
                _samples.Dequeue();
            }

            while (_samples.Count > 1 && occurredAtUtc - _samples.Peek().OccurredAtUtc > RollingWindow)
            {
                _samples.Dequeue();
            }
        }

        private void PruneNonProgressSamples()
        {
            while (_samples.Count > 1)
            {
                TransferSample firstSample = _samples.Peek();
                TransferSample secondSample = _samples.ElementAt(1);
                if (secondSample.TransferredBytes > firstSample.TransferredBytes)
                {
                    return;
                }

                _samples.Dequeue();
            }
        }

        private bool TryUseCurrentSampleAsBaselineAfterStaleZero(TransferSample currentSample)
        {
            if (_samples.Count != 2)
            {
                return false;
            }

            TransferSample firstSample = _samples.Peek();
            if (firstSample.TransferredBytes != 0
                || currentSample.TransferredBytes <= 0
                || currentSample.OccurredAtUtc - firstSample.OccurredAtUtc <= MaximumInitialZeroBaselineAge)
            {
                return false;
            }

            _samples.Dequeue();
            _smoothedSpeedBytesPerSecond = null;
            _smoothedEstimatedTimeRemaining = null;
            _lastSpeedOccurredAtUtc = null;
            _lastEstimateOccurredAtUtc = null;
            return true;
        }

        private AppTransferProgressEstimate CreateEstimate(
            TransferSample currentSample,
            long? totalBytes,
            bool isCompleted)
        {
            if (_samples.Count < 2 || isCompleted)
            {
                return new AppTransferProgressEstimate(null, null);
            }

            TransferSample firstSample = _samples.Peek();
            double seconds = (currentSample.OccurredAtUtc - firstSample.OccurredAtUtc).TotalSeconds;
            long bytesTransferred = currentSample.TransferredBytes - firstSample.TransferredBytes;
            if (seconds < MinimumEstimateSampleDuration.TotalSeconds
                || bytesTransferred < GetMinimumEstimateTransferredBytes(totalBytes))
            {
                return new AppTransferProgressEstimate(null, null);
            }

            double speedBytesPerSecond = SmoothSpeed(bytesTransferred / seconds, currentSample.OccurredAtUtc);
            TimeSpan? estimatedTimeRemaining = null;
            if (totalBytes.HasValue && totalBytes.Value > currentSample.TransferredBytes)
            {
                estimatedTimeRemaining = SmoothEstimatedTimeRemaining(
                    TimeSpan.FromSeconds((totalBytes.Value - currentSample.TransferredBytes) / speedBytesPerSecond),
                    currentSample.OccurredAtUtc);
            }
            else
            {
                _smoothedEstimatedTimeRemaining = null;
                _lastEstimateOccurredAtUtc = null;
            }

            return new AppTransferProgressEstimate(speedBytesPerSecond, estimatedTimeRemaining);
        }

        private double SmoothSpeed(double speedBytesPerSecond, DateTime occurredAtUtc)
        {
            if (!_smoothedSpeedBytesPerSecond.HasValue || !_lastSpeedOccurredAtUtc.HasValue)
            {
                _smoothedSpeedBytesPerSecond = speedBytesPerSecond;
                _lastSpeedOccurredAtUtc = occurredAtUtc;
                return speedBytesPerSecond;
            }

            double smoothingFactor = CalculateSmoothingFactor(occurredAtUtc - _lastSpeedOccurredAtUtc.Value, SpeedSmoothingTimeConstant);
            double smoothedSpeed = _smoothedSpeedBytesPerSecond.Value
                + ((speedBytesPerSecond - _smoothedSpeedBytesPerSecond.Value) * smoothingFactor);
            _smoothedSpeedBytesPerSecond = Math.Max(0, smoothedSpeed);
            _lastSpeedOccurredAtUtc = occurredAtUtc;
            return _smoothedSpeedBytesPerSecond.Value;
        }

        private TimeSpan SmoothEstimatedTimeRemaining(TimeSpan rawEstimate, DateTime occurredAtUtc)
        {
            if (!_smoothedEstimatedTimeRemaining.HasValue || !_lastEstimateOccurredAtUtc.HasValue)
            {
                _smoothedEstimatedTimeRemaining = rawEstimate;
                _lastEstimateOccurredAtUtc = occurredAtUtc;
                return rawEstimate;
            }

            TimeSpan elapsed = occurredAtUtc - _lastEstimateOccurredAtUtc.Value;
            TimeSpan agedPreviousEstimate = elapsed > TimeSpan.Zero
                ? _smoothedEstimatedTimeRemaining.Value - elapsed
                : _smoothedEstimatedTimeRemaining.Value;
            if (agedPreviousEstimate < TimeSpan.Zero)
            {
                agedPreviousEstimate = TimeSpan.Zero;
            }

            double smoothingFactor = CalculateSmoothingFactor(elapsed, RemainingTimeSmoothingTimeConstant);
            double smoothedSeconds = agedPreviousEstimate.TotalSeconds
                + ((rawEstimate.TotalSeconds - agedPreviousEstimate.TotalSeconds) * smoothingFactor);
            TimeSpan smoothedEstimate = TimeSpan.FromSeconds(Math.Max(0, smoothedSeconds));
            _smoothedEstimatedTimeRemaining = smoothedEstimate;
            _lastEstimateOccurredAtUtc = occurredAtUtc;
            return smoothedEstimate;
        }

        private static double CalculateSmoothingFactor(TimeSpan elapsed, TimeSpan timeConstant)
        {
            if (elapsed <= TimeSpan.Zero)
            {
                return 0;
            }

            return 1 - Math.Exp(-elapsed.TotalSeconds / timeConstant.TotalSeconds);
        }

        private static long GetMinimumEstimateTransferredBytes(long? totalBytes)
        {
            if (totalBytes is not > 0)
            {
                return UnknownTotalEstimateTransferredBytes;
            }

            return Math.Max(
                1,
                Math.Min(
                    totalBytes.Value / EstimateTransferredBytesDivisor,
                    MaximumEstimateTransferredBytes));
        }

        private readonly record struct TransferSample(long TransferredBytes, DateTime OccurredAtUtc);
    }
}
