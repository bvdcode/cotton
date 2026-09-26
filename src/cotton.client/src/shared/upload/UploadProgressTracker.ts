import type { UploadProgressSnapshot } from "./types";
import { RollingBytesPerSecondEstimator } from "./RollingBytesPerSecondEstimator";
import { uploadConfig } from "./config";
import type { UploadTaskInternal } from "./UploadManager";
import type { UploadExecutionState } from "./UploadTaskRunner";

export class UploadProgressTracker {
  private overallBytesTotal = 0;
  private overallBytesUploaded = 0;
  private overallBytesTransferredForSpeed = 0;
  private readonly overallEstimator = new RollingBytesPerSecondEstimator({
    windowMs: 2000,
    minDurationMs: 300,
  });
  private readonly onProgress: (task: UploadTaskInternal, now: number) => void;
  private readonly emit: () => void;

  constructor(
    onProgress: (task: UploadTaskInternal, now: number) => void,
    emit: () => void,
  ) {
    this.onProgress = onProgress;
    this.emit = emit;
  }

  beginBatch(): void {
    this.overallBytesTotal = 0;
    this.overallBytesUploaded = 0;
    this.overallBytesTransferredForSpeed = 0;
    this.overallEstimator.reset();
  }

  addFile(size: number): void {
    this.overallBytesTotal += size;
  }

  recount(tasks: UploadTaskInternal[]): void {
    this.countTasks(tasks);
    this.overallBytesTransferredForSpeed = tasks.reduce(
      (sum, task) =>
        sum + (task._bytesTransferredForSpeed ?? task.bytesUploaded),
      0,
    );
    this.overallEstimator.reset();
  }

  countTasks(tasks: UploadTaskInternal[]): void {
    this.overallBytesTotal = tasks.reduce(
      (sum, task) => sum + task.bytesTotal,
      0,
    );
    this.overallBytesUploaded = tasks.reduce(
      (sum, task) => sum + task.bytesUploaded,
      0,
    );
  }

  syncSnapshot(bytesTotal: number, bytesCompleted: number): void {
    this.overallBytesTotal = bytesTotal;
    this.overallBytesUploaded = bytesCompleted;
  }

  getSpeed(): number {
    return this.overallEstimator.getSnapshot().rollingBytesPerSec;
  }

  record(
    task: UploadTaskInternal,
    state: UploadExecutionState,
    bytesUploaded: number,
    snapshot?: UploadProgressSnapshot,
  ): void {
    const previousBytesUploaded = task.bytesUploaded;
    task.bytesUploaded = Math.min(task.bytesTotal, Math.max(0, bytesUploaded));
    task.progress01 =
      task.bytesTotal > 0 ? task.bytesUploaded / task.bytesTotal : 1;

    const now = Date.now();
    this.updateSpeed(task, state, snapshot, now);
    const delta = task.bytesUploaded - previousBytesUploaded;
    if (delta !== 0) {
      this.overallBytesUploaded += delta;
      this.overallBytesUploaded = Math.max(
        0,
        Math.min(this.overallBytesTotal, this.overallBytesUploaded),
      );
    }

    if (
      delta < 0 ||
      now - state.lastEmitTime >= uploadConfig.progressEmitIntervalMs ||
      task.bytesUploaded >= task.bytesTotal
    ) {
      state.lastEmitTime = now;
      this.emit();
    }
  }

  private updateSpeed(
    task: UploadTaskInternal,
    state: UploadExecutionState,
    snapshot: UploadProgressSnapshot | undefined,
    now: number,
  ): void {
    if (task.bytesUploaded > 0) {
      task._sawProgress = true;
      this.onProgress(task, now);
    }

    const previousSpeedBytes = task._bytesTransferredForSpeed ?? 0;
    const nextSpeedBytes = Math.max(
      previousSpeedBytes,
      snapshot ? snapshot.bytesTransmitted : task.bytesUploaded,
    );
    const speedDelta = nextSpeedBytes - previousSpeedBytes;
    if (speedDelta <= 0) {
      return;
    }

    task._bytesTransferredForSpeed = nextSpeedBytes;
    const taskRate = state.taskEstimator.update(nextSpeedBytes, now);
    task.uploadSpeedBytesPerSec =
      taskRate.rollingBytesPerSec > 0
        ? taskRate.rollingBytesPerSec
        : taskRate.averageBytesPerSec;
    this.overallBytesTransferredForSpeed += speedDelta;
    this.overallEstimator.update(this.overallBytesTransferredForSpeed, now);
  }

  complete(task: UploadTaskInternal, previousBytesUploaded: number): void {
    const finalizeDelta = task.bytesUploaded - previousBytesUploaded;
    if (finalizeDelta > 0) {
      this.overallBytesUploaded += finalizeDelta;
      this.overallEstimator.update(this.overallBytesUploaded, Date.now());
    }
  }
}
