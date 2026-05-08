export interface TransferSample {
  bytes: number;
  durationMs: number;
  succeeded: boolean;
}

export interface AdaptiveConcurrencyControllerOptions {
  maxConcurrency: number;
  minConcurrency?: number;
  rampUpDurationMs?: number;
}

/**
 * Small additive-increase controller for browser uploads.
 *
 * The signal available in browsers is intentionally modest: if a transfer lane
 * completes quickly, one lane is not keeping the connection busy enough and we
 * can safely try another. Slow transfers keep the current concurrency steady.
 */
export class AdaptiveConcurrencyController {
  private readonly minConcurrency: number;
  private readonly maxConcurrency: number;
  private readonly rampUpDurationMs: number;
  private currentConcurrency: number;

  constructor(options: AdaptiveConcurrencyControllerOptions) {
    this.minConcurrency = Math.max(1, options.minConcurrency ?? 1);
    this.maxConcurrency = Math.max(this.minConcurrency, options.maxConcurrency);
    this.rampUpDurationMs = Math.max(1, options.rampUpDurationMs ?? 1200);
    this.currentConcurrency = this.minConcurrency;
  }

  get current(): number {
    return this.currentConcurrency;
  }

  get max(): number {
    return this.maxConcurrency;
  }

  reset(): void {
    this.currentConcurrency = this.minConcurrency;
  }

  tryIncrease(): boolean {
    if (this.currentConcurrency >= this.maxConcurrency) {
      return false;
    }

    this.currentConcurrency += 1;
    return true;
  }

  observe(sample: TransferSample): boolean {
    if (!sample.succeeded) {
      this.currentConcurrency = Math.max(
        this.minConcurrency,
        Math.ceil(this.currentConcurrency / 2),
      );
      return false;
    }

    if (sample.bytes <= 0 || sample.durationMs > this.rampUpDurationMs) {
      return false;
    }

    return this.tryIncrease();
  }
}
