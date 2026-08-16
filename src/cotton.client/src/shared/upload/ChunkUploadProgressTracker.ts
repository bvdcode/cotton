import type { UploadProgressSnapshot } from "./types";

type ProgressCallback = (
  bytesUploaded: number,
  snapshot?: UploadProgressSnapshot,
) => void;

export class ChunkUploadProgressTracker {
  private readonly activeAttempts = new Map<number, number>();
  private nextAttemptId = 0;
  private bytesConfirmed = 0;
  private bytesTransmitted = 0;
  private lastSnapshot: UploadProgressSnapshot | null = null;
  private readonly totalBytes: number;
  private readonly onProgress?: ProgressCallback;

  constructor(totalBytes: number, onProgress?: ProgressCallback) {
    this.totalBytes = totalBytes;
    this.onProgress = onProgress;
  }

  beginAttempt(): number {
    const attemptId = this.nextAttemptId++;
    this.activeAttempts.set(attemptId, 0);
    return attemptId;
  }

  updateTransmission(attemptId: number, bytesUploaded: number): void {
    const previous = this.activeAttempts.get(attemptId);
    if (previous === undefined) {
      return;
    }

    const next = Math.max(previous, bytesUploaded);
    if (next === previous) {
      return;
    }

    this.activeAttempts.set(attemptId, next);
    this.bytesTransmitted += next - previous;
    this.report();
  }

  completeAttempt(attemptId: number, chunkBytes: number): void {
    this.updateTransmission(attemptId, chunkBytes);
    this.activeAttempts.delete(attemptId);
    this.bytesConfirmed += chunkBytes;
    this.report();
  }

  completeWithoutTransfer(chunkBytes: number): void {
    this.bytesConfirmed += chunkBytes;
    this.report();
  }

  discardAttempt(attemptId: number): void {
    if (this.activeAttempts.delete(attemptId)) {
      this.report();
    }
  }

  private report(): void {
    if (!this.onProgress) {
      return;
    }

    let bytesInFlight = 0;
    for (const bytes of this.activeAttempts.values()) {
      bytesInFlight += bytes;
    }

    let bytesUploaded = Math.min(
      this.totalBytes,
      this.bytesConfirmed + bytesInFlight,
    );
    if (this.totalBytes > 0 && this.bytesConfirmed < this.totalBytes) {
      bytesUploaded = Math.min(bytesUploaded, this.totalBytes - 1);
    }

    const snapshot: UploadProgressSnapshot = {
      bytesUploaded,
      bytesConfirmed: this.bytesConfirmed,
      bytesInFlight,
      bytesTransmitted: this.bytesTransmitted,
    };

    if (this.isSameSnapshot(snapshot)) {
      return;
    }

    this.lastSnapshot = snapshot;
    this.onProgress(bytesUploaded, snapshot);
  }

  private isSameSnapshot(snapshot: UploadProgressSnapshot): boolean {
    if (!this.lastSnapshot) {
      return false;
    }

    return (
      this.lastSnapshot.bytesUploaded === snapshot.bytesUploaded &&
      this.lastSnapshot.bytesConfirmed === snapshot.bytesConfirmed &&
      this.lastSnapshot.bytesInFlight === snapshot.bytesInFlight &&
      this.lastSnapshot.bytesTransmitted === snapshot.bytesTransmitted
    );
  }
}
