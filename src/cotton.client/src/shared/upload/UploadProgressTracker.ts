import { RollingBytesPerSecondEstimator } from "./RollingBytesPerSecondEstimator";
import { uploadConfig } from "./config";
import type { UploadTaskInternal } from "./UploadManager";
import type { UploadExecutionState } from "./UploadTaskRunner";

export class UploadProgressTracker {
  private overallBytesTotal = 0;
  private overallBytesUploaded = 0;
  private overallBytesProcessedForSpeed = 0;
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
    this.overallBytesProcessedForSpeed = 0;
    this.overallEstimator.reset();
    this.overallEstimator.update(0);
  }

  addFile(size: number): void {
    this.overallBytesTotal += size;
  }

  recount(tasks: UploadTaskInternal[]): void {
    this.countTasks(tasks);
    this.overallBytesProcessedForSpeed = tasks.reduce(
      (sum, task) => sum + (task._bytesProcessedForSpeed ?? task.bytesUploaded),
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
  ): void {
    const previousBytesUploaded = task.bytesUploaded;
    task.bytesUploaded = Math.min(task.bytesTotal, Math.max(0, bytesUploaded));
    task.progress01 =
      task.bytesTotal > 0 ? task.bytesUploaded / task.bytesTotal : 1;

    const now = Date.now();
    this.updateSpeed(task, state, now);
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
    now: number,
  ): void {
    if (task.bytesUploaded > 0) {
      task._sawProgress = true;
      this.onProgress(task, now);
    }

    const previousSpeedBytes = task._bytesProcessedForSpeed ?? 0;
    const nextSpeedBytes = Math.max(previousSpeedBytes, task.bytesUploaded);
    const speedDelta = nextSpeedBytes - previousSpeedBytes;
    if (speedDelta <= 0) {
      return;
    }

    task._bytesProcessedForSpeed = nextSpeedBytes;
    const taskRate = state.taskEstimator.update(nextSpeedBytes, now);
    task.uploadSpeedBytesPerSec =
      taskRate.rollingBytesPerSec > 0
        ? taskRate.rollingBytesPerSec
        : taskRate.averageBytesPerSec;
    this.overallBytesProcessedForSpeed += speedDelta;
    this.overallEstimator.update(this.overallBytesProcessedForSpeed, now);
  }

  complete(task: UploadTaskInternal, previousBytesUploaded: number): void {
    const finalizeDelta = task.bytesUploaded - previousBytesUploaded;
    if (finalizeDelta > 0) {
      this.overallBytesUploaded += finalizeDelta;
      const speedDelta = Math.max(
        0,
        task.bytesUploaded - (task._bytesProcessedForSpeed ?? 0),
      );
      task._bytesProcessedForSpeed = task.bytesUploaded;
      this.overallBytesProcessedForSpeed += speedDelta;
      this.overallEstimator.update(
        this.overallBytesProcessedForSpeed,
        Date.now(),
      );
    }
  }
}
