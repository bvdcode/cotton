// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Progress;

/// <summary>
/// Calculates rolling speed and remaining-time estimates for one sync-pair transfer stream.
/// </summary>
public sealed class AppTransferProgressEstimator
{
    private static readonly TimeSpan RollingWindow = TimeSpan.FromSeconds(5);
    private const double RemainingTimeSmoothingFactor = 0.35;
    private const int MaximumSamples = 8;
    private readonly Queue<TransferSample> _samples = new();
    private SyncTransferDirection _direction = SyncTransferDirection.Unknown;
    private TimeSpan? _smoothedEstimatedTimeRemaining;
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
        PruneSamples(currentSample.OccurredAtUtc);
        AppTransferProgressEstimate estimate = CreateEstimate(currentSample, totalBytes, isCompleted);
        if (isCompleted)
        {
            _samples.Clear();
            _smoothedEstimatedTimeRemaining = null;
            _lastEstimateOccurredAtUtc = null;
        }

        return estimate;
    }

    private void Reset(SyncTransferDirection direction, string relativePath)
    {
        _samples.Clear();
        _smoothedEstimatedTimeRemaining = null;
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
        if (seconds <= 0 || bytesTransferred <= 0)
        {
            return new AppTransferProgressEstimate(null, null);
        }

        double speedBytesPerSecond = bytesTransferred / seconds;
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

        double smoothedSeconds = agedPreviousEstimate.TotalSeconds
            + ((rawEstimate.TotalSeconds - agedPreviousEstimate.TotalSeconds) * RemainingTimeSmoothingFactor);
        TimeSpan smoothedEstimate = TimeSpan.FromSeconds(Math.Max(0, smoothedSeconds));
        _smoothedEstimatedTimeRemaining = smoothedEstimate;
        _lastEstimateOccurredAtUtc = occurredAtUtc;
        return smoothedEstimate;
    }

    private readonly record struct TransferSample(long TransferredBytes, DateTime OccurredAtUtc);
}
