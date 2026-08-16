interface QueuedValue<T> {
  value: T;
  bytes: number;
}

interface ValueWaiter<T> {
  resolve: (value: T | null) => void;
  reject: (error: Error) => void;
}

interface SpaceWaiter {
  resolve: () => void;
  reject: (error: Error) => void;
}

export class BoundedByteQueue<T> {
  private readonly values: QueuedValue<T>[] = [];
  private readonly valueWaiters: ValueWaiter<T>[] = [];
  private readonly spaceWaiters: SpaceWaiter[] = [];
  private queuedBytes = 0;
  private closed = false;
  private failure: Error | null = null;
  private readonly maxItems: number;
  private readonly maxBytes: number;

  constructor(maxItems: number, maxBytes: number) {
    if (maxItems < 1 || maxBytes < 1) {
      throw new Error("Queue capacity must be positive.");
    }

    this.maxItems = maxItems;
    this.maxBytes = maxBytes;
  }

  async enqueue(value: T, bytes: number): Promise<void> {
    if (bytes < 0 || bytes > this.maxBytes) {
      throw new Error("Queued value exceeds the byte capacity.");
    }

    while (!this.canAccept(bytes)) {
      this.throwIfUnavailable();
      await new Promise<void>((resolve, reject) => {
        this.spaceWaiters.push({ resolve, reject });
      });
    }

    this.throwIfUnavailable();
    const waiter = this.valueWaiters.shift();
    if (waiter) {
      waiter.resolve(value);
      return;
    }

    this.values.push({ value, bytes });
    this.queuedBytes += bytes;
  }

  async dequeue(): Promise<T | null> {
    if (this.failure) {
      throw this.failure;
    }

    const queued = this.values.shift();
    if (queued) {
      this.queuedBytes -= queued.bytes;
      this.wakeSpaceWaiters();
      return queued.value;
    }

    if (this.closed) {
      return null;
    }

    return new Promise<T | null>((resolve, reject) => {
      this.valueWaiters.push({ resolve, reject });
    });
  }

  close(): void {
    if (this.closed || this.failure) {
      return;
    }

    this.closed = true;
    if (this.values.length === 0) {
      this.resolveValueWaiters();
    }

    this.rejectSpaceWaiters(new Error("Queue is closed."));
  }

  fail(error: Error): void {
    if (this.failure) {
      return;
    }

    this.failure = error;
    this.closed = true;
    this.values.length = 0;
    this.queuedBytes = 0;
    this.rejectValueWaiters(error);
    this.rejectSpaceWaiters(error);
  }

  private canAccept(bytes: number): boolean {
    return (
      !this.closed &&
      !this.failure &&
      this.values.length < this.maxItems &&
      this.queuedBytes + bytes <= this.maxBytes
    );
  }

  private throwIfUnavailable(): void {
    if (this.failure) {
      throw this.failure;
    }

    if (this.closed) {
      throw new Error("Queue is closed.");
    }
  }

  private wakeSpaceWaiters(): void {
    const waiters = this.spaceWaiters.splice(0);
    for (const waiter of waiters) {
      waiter.resolve();
    }
  }

  private resolveValueWaiters(): void {
    const waiters = this.valueWaiters.splice(0);
    for (const waiter of waiters) {
      waiter.resolve(null);
    }
  }

  private rejectValueWaiters(error: Error): void {
    const waiters = this.valueWaiters.splice(0);
    for (const waiter of waiters) {
      waiter.reject(error);
    }
  }

  private rejectSpaceWaiters(error: Error): void {
    const waiters = this.spaceWaiters.splice(0);
    for (const waiter of waiters) {
      waiter.reject(error);
    }
  }
}
