import type { NodeFileManifestDto } from "../api/nodesApi";
import type { AppTaskHandle } from "../tasks/types";
import { AdaptiveConcurrencyController } from "./AdaptiveConcurrencyController";
import { uploadConfig } from "./config";
import { RollingBytesPerSecondEstimator } from "./RollingBytesPerSecondEstimator";
import { uploadFileToNode } from "./uploadFileToNode";
import type { UploadProgressSnapshot, UploadServerParams } from "./types";
import type { UploadTaskInternal } from "./UploadManager";

export interface UploadExecutionState {
  encryptionTask: AppTaskHandle | null;
  encryptionTaskFinished: boolean;
  lastEmitTime: number;
  taskEstimator: RollingBytesPerSecondEstimator;
}

interface UploadTaskRunnerCallbacks {
  createEncryptionTask: (
    task: UploadTaskInternal,
    bytesTotal: number,
  ) => AppTaskHandle;
  onProgress: (
    task: UploadTaskInternal,
    state: UploadExecutionState,
    bytesUploaded: number,
    snapshot?: UploadProgressSnapshot,
  ) => void;
  onComplete: (
    task: UploadTaskInternal,
    uploadedFile: NodeFileManifestDto | undefined,
  ) => void;
  onFailure: (
    task: UploadTaskInternal,
    state: UploadExecutionState,
    error: Error | null,
  ) => void;
  onStatusChange: () => void;
  onCapacityAvailable: () => void;
  hasQueuedTasks: () => boolean;
}

export class UploadTaskRunner {
  private activeUploads = 0;
  private readonly fileConcurrency = new AdaptiveConcurrencyController({
    maxConcurrency: uploadConfig.maxConcurrentFileUploads,
    rampUpDurationMs: uploadConfig.concurrencyRampUpMs,
  });
  private readonly callbacks: UploadTaskRunnerCallbacks;

  constructor(callbacks: UploadTaskRunnerCallbacks) {
    this.callbacks = callbacks;
  }

  canStart(): boolean {
    return this.activeUploads < this.fileConcurrency.current;
  }

  reset(): void {
    this.fileConcurrency.reset();
  }

  observe(task: UploadTaskInternal, succeeded: boolean): void {
    const completedAt = task.completedAt ?? Date.now();
    this.fileConcurrency.observe({
      bytes: task.bytesTotal,
      durationMs: completedAt - (task._startedAt ?? completedAt),
      succeeded,
    });
  }

  startTask(task: UploadTaskInternal, server: UploadServerParams): void {
    this.activeUploads += 1;
    task.status = "uploading";
    task.error = undefined;
    task.uploadSpeedBytesPerSec = 0;
    task._startedAt = Date.now();
    task._sawProgress = false;
    task._laneProbeConsumed = false;
    task._bytesTransferredForSpeed = 0;

    const state: UploadExecutionState = {
      encryptionTask: null,
      encryptionTaskFinished: false,
      lastEmitTime: 0,
      taskEstimator: new RollingBytesPerSecondEstimator({
        windowMs: 1500,
        minDurationMs: 250,
      }),
    };

    task._laneProbeTimeout = setTimeout(() => {
      this.maybeOpenLaneForHeadOfLine(task, Date.now());
    }, uploadConfig.fileHeadOfLineProbeMs);

    this.callbacks.onStatusChange();
    void this.executeTask(task, server, state);
  }

  private async executeTask(
    task: UploadTaskInternal,
    server: UploadServerParams,
    state: UploadExecutionState,
  ): Promise<void> {
    try {
      const uploadedFile = await uploadFileToNode({
        file: task._file,
        nodeId: task.nodeId,
        replaceNodeFileId: task._replaceNodeFileId,
        server,
        encrypt: task._encrypt,
        onEncryptProgress: (bytesEncrypted, bytesTotal) => {
          state.encryptionTask ??= this.callbacks.createEncryptionTask(
            task,
            bytesTotal,
          );
          state.encryptionTask.update({
            status: "running",
            bytesTotal,
            bytesCompleted: bytesEncrypted,
          });
        },
        onEncryptComplete: () => {
          state.encryptionTaskFinished = true;
          state.encryptionTask?.complete();
        },
        onProgress: (bytesUploaded, snapshot) =>
          this.callbacks.onProgress(task, state, bytesUploaded, snapshot),
        onFinalizing: () => {
          task.status = "finalizing";
          this.callbacks.onStatusChange();
        },
      });

      this.callbacks.onComplete(task, uploadedFile);
    } catch (error) {
      this.callbacks.onFailure(
        task,
        state,
        error instanceof Error ? error : null,
      );
    } finally {
      if (task._laneProbeTimeout) {
        clearTimeout(task._laneProbeTimeout);
        task._laneProbeTimeout = undefined;
      }
      this.activeUploads = Math.max(0, this.activeUploads - 1);
      this.callbacks.onCapacityAvailable();
    }
  }

  maybeOpenLaneForHeadOfLine(task: UploadTaskInternal, now: number): void {
    if (
      task._laneProbeConsumed ||
      !task._sawProgress ||
      this.fileConcurrency.current > 1 ||
      !this.callbacks.hasQueuedTasks()
    ) {
      return;
    }

    const startedAt = task._startedAt ?? now;
    if (now - startedAt < uploadConfig.fileHeadOfLineProbeMs) {
      return;
    }

    task._laneProbeConsumed = true;
    if (this.fileConcurrency.tryIncrease()) {
      this.callbacks.onCapacityAvailable();
    }
  }

  destroy(tasks: UploadTaskInternal[]): void {
    for (const task of tasks) {
      if (task._laneProbeTimeout) {
        clearTimeout(task._laneProbeTimeout);
        task._laneProbeTimeout = undefined;
      }
    }
  }
}
