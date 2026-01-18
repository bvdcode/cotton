export interface BytesPerSecondSnapshot {
  /** Rolling (windowed) speed for display, in bytes/sec. */
  rollingBytesPerSec: number;
  /** Average speed since the estimator start/reset, in bytes/sec. */
  averageBytesPerSec: number;
}

export interface RollingBytesPerSecondEstimatorOptions {
  /** Rolling window used for the rolling speed calculation. */
  windowMs?: number;
  /**
   * Minimum duration used when converting bytes to a rate.
   * Prevents "0 B/s" for very fast uploads and also avoids huge spikes.
   */
  minDurationMs?: number;
}

/**
 * Estimates an upload speed from monotonically increasing byte counters.
 *
 * - Rolling speed is computed over a recent time window.
 * - Average speed is computed since reset/start.
 * - Both clamp the effective duration to avoid unstable 0/spike values.
 */
export class RollingBytesPerSecondEstimator {
  private readonly windowMs: number;
  private readonly minDurationMs: number;

  private startMs: number | null = null;
  private samples: Array<{ t: number; bytes: number }> = [];
  private last: BytesPerSecondSnapshot = { rollingBytesPerSec: 0, averageBytesPerSec: 0 };

  constructor(options: RollingBytesPerSecondEstimatorOptions = {}) {
    this.windowMs = options.windowMs ?? 1500;
    this.minDurationMs = options.minDurationMs ?? 250;
  }

  reset() {
    this.startMs = null;
    this.samples = [];
    this.last = { rollingBytesPerSec: 0, averageBytesPerSec: 0 };
  }

  update(totalBytes: number, nowMs: number = Date.now()): BytesPerSecondSnapshot {
    if (!Number.isFinite(totalBytes) || totalBytes < 0) {
      return this.last;
    }

    if (this.startMs === null) {
      this.startMs = nowMs;
    }

    this.samples.push({ t: nowMs, bytes: totalBytes });

    const windowStart = nowMs - this.windowMs;
    while (this.samples.length > 2 && this.samples[0].t < windowStart) {
      this.samples.shift();
    }

    const startMs = this.startMs;
    const avgDtMs = Math.max(nowMs - startMs, this.minDurationMs);
    const averageBytesPerSec = totalBytes / (avgDtMs / 1000);

    let rollingBytesPerSec = 0;
    if (this.samples.length >= 2) {
      const oldest = this.samples[0];
      const newest = this.samples[this.samples.length - 1];
      const dBytes = newest.bytes - oldest.bytes;
      const dtMs = Math.max(newest.t - oldest.t, this.minDurationMs);
      if (dBytes > 0) {
        rollingBytesPerSec = dBytes / (dtMs / 1000);
      }
    }

    this.last = {
      rollingBytesPerSec,
      averageBytesPerSec,
    };

    return this.last;
  }

  getSnapshot(): BytesPerSecondSnapshot {
    return this.last;
  }
}
