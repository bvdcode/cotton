import type { Guid } from "../api/layoutsApi";
import { refreshNodeContent } from "../store/nodesActions";
import { getCachedServerSettings } from "../api/queries/serverSettings";
import { ClientEncryptionSizeLimitError, NoKeyError } from "../crypto";
import { AdaptiveConcurrencyController } from "./AdaptiveConcurrencyController";
import { uploadConfig } from "./config";
import { uploadFileToNode } from "./uploadFileToNode";
import { RollingBytesPerSecondEstimator } from "./RollingBytesPerSecondEstimator";
import { globalHashWorkerPool } from "./hash/HashWorkerPool";
import type { UploadServerParams } from "./types";
import { formatBytes } from "../utils/formatBytes";
import type {
  AppTask,
  AppTaskHandle,
  AppTaskSnapshot,
  AppTaskStatus,
  CreateAppTaskOptions,
  UpdateAppTaskOptions,
} from "../tasks/types";

export type UploadTaskStatus =
  | "queued"
  | "uploading"
  | "finalizing"
  | "completed"
  | "failed";

export interface UploadTask {
  id: string;
  nodeId: Guid;
  nodeLabel: string;
  fileName: string;
  bytesTotal: number;
  bytesUploaded: number;
  progress01: number;
  status: UploadTaskStatus;
  error?: string;
  errorKey?: string;
  errorParams?: Record<string, string | number>;
  uploadSpeedBytesPerSec?: number;
  completedAt?: number;
}

interface UploadTaskInternal extends UploadTask {
  _file: File;
  _encrypt: boolean;
  _startedAt?: number;
  _sawProgress?: boolean;
  _laneProbeConsumed?: boolean;
  _laneProbeTimeout?: ReturnType<typeof setTimeout>;
  _bytesTransferredForSpeed?: number;
}

interface ExternalTaskInternal extends AppTask {
  _external: true;
}

export interface EnqueueOptions {
  encrypt?: boolean;
}

export interface UploadFilePickerContext {
  nodeId: Guid;
  nodeLabel: string;
  multiple?: boolean;
  accept?: string;
}

type Listener = () => void;

const makeId = () => `${Date.now()}-${Math.random().toString(16).slice(2)}`;

const MAX_FINISHED_TASKS = 10000;
const FINISHED_TASK_TTL_MS = 30 * 60 * 1000;
const PRUNE_INTERVAL_MS = 5 * 60 * 1000;
const FINISHED_TASK_STATUSES = new Set<AppTaskStatus>(["completed", "failed"]);

export class UploadManager {
  private readonly listeners = new Set<Listener>();
  private readonly tasks: UploadTaskInternal[] = [];
  private readonly externalTasks: ExternalTaskInternal[] = [];
  private pumping = false;
  private activeUploads = 0;
  private readonly fileConcurrency = new AdaptiveConcurrencyController({
    maxConcurrency: uploadConfig.maxConcurrentFileUploads,
    rampUpDurationMs: uploadConfig.concurrencyRampUpMs,
  });
  private open = false;
  private snapshot: AppTaskSnapshot = {
    open: false,
    tasks: [],
    overall: {
      bytesTotal: 0,
      bytesCompleted: 0,
      progress01: 0,
      speedBytesPerSec: 0,
    },
  };
  private readonly refreshNodeIds = new Set<Guid>();
  private refreshTimeout: ReturnType<typeof setTimeout> | null = null;

  private overallBytesTotal = 0;
  private overallBytesUploaded = 0;
  private overallBytesTransferredForSpeed = 0;
  private overallEstimator = new RollingBytesPerSecondEstimator({ windowMs: 2000, minDurationMs: 300 });

  private filePickerOpen: ((options: { multiple: boolean; accept?: string }) => void) | null = null;
  private pendingFilePickerContext: UploadFilePickerContext | null = null;
  private pruneIntervalId: ReturnType<typeof setInterval> | null = null;

  constructor() {
    this.pruneIntervalId = setInterval(() => {
      if (this.tasks.length > 0 || this.externalTasks.length > 0) {
        const before = this.tasks.length + this.externalTasks.length;
        this.pruneFinishedTasks();
        if (this.tasks.length + this.externalTasks.length !== before) {
          this.emit();
        }
      }
    }, PRUNE_INTERVAL_MS);
  }

  subscribe(listener: Listener): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  getOpen(): boolean {
    return this.open;
  }

  setOpen(open: boolean) {
    this.open = open;
    this.emit();
  }

  getSnapshot(): AppTaskSnapshot {
    return this.snapshot;
  }

  createTask(options: CreateAppTaskOptions): AppTaskHandle {
    const bytesTotal = Math.max(0, options.bytesTotal ?? 0);
    const task: ExternalTaskInternal = {
      _external: true,
      id: makeId(),
      kind: options.kind,
      label: options.label,
      scopeLabel: options.scopeLabel ?? "",
      bytesTotal,
      bytesCompleted: 0,
      progress01: bytesTotal > 0 ? 0 : 1,
      status: "queued",
    };

    this.externalTasks.unshift(task);
    this.open = true;
    this.pruneFinishedTasks();
    this.emit();

    return {
      id: task.id,
      update: (update) => this.updateExternalTask(task.id, update),
      complete: () =>
        this.updateExternalTask(task.id, {
          status: "completed",
          bytesCompleted: task.bytesTotal,
          progress01: 1,
        }),
      fail: (error) => {
        const current = this.externalTasks.find((x) => x.id === task.id);
        if (!current) return;

        current.status = "failed";
        current.completedAt = Date.now();
        current.error = error?.message;
        current.errorKey = error?.key;
        current.errorParams = error?.params;
        this.emit();
      },
    };
  }

  clearFinished(options?: { includeCompleted?: boolean; includeFailed?: boolean }) {
    const includeCompleted = options?.includeCompleted ?? true;
    const includeFailed = options?.includeFailed ?? true;

    const remainingUploadTasks = this.tasks.filter((t) => {
      if (t.status === "completed") return !includeCompleted;
      if (t.status === "failed") return !includeFailed;
      return true;
    });
    const remainingExternalTasks = this.externalTasks.filter((t) => {
      if (t.status === "completed") return !includeCompleted;
      if (t.status === "failed") return !includeFailed;
      return true;
    });

    if (
      remainingUploadTasks.length === this.tasks.length &&
      remainingExternalTasks.length === this.externalTasks.length
    ) {
      return;
    }

    this.tasks.length = 0;
    this.tasks.push(...remainingUploadTasks);
    this.externalTasks.length = 0;
    this.externalTasks.push(...remainingExternalTasks);

    this.overallBytesTotal = this.tasks.reduce((sum, t) => sum + t.bytesTotal, 0);
    this.overallBytesUploaded = this.tasks.reduce((sum, t) => sum + t.bytesUploaded, 0);
    this.overallBytesTransferredForSpeed = this.tasks.reduce(
      (sum, t) => sum + (t._bytesTransferredForSpeed ?? t.bytesUploaded),
      0,
    );
    this.overallEstimator.reset();

    if (this.tasks.length === 0 && this.externalTasks.length === 0) {
      this.open = false;
    }

    this.emit();
  }

  setFilePickerOpen(fn: ((options: { multiple: boolean; accept?: string }) => void) | null) {
    this.filePickerOpen = fn;
  }

  openFilePicker(context: UploadFilePickerContext) {
    this.pendingFilePickerContext = context;
    this.filePickerOpen?.({
      multiple: context.multiple ?? true,
      accept: context.accept,
    });
  }

  handleFilePickerSelection(files: FileList | File[]) {
    const context = this.pendingFilePickerContext;
    this.pendingFilePickerContext = null;
    if (!context) return;
    this.enqueue(files, context.nodeId, context.nodeLabel);
  }

  enqueue(
    files: FileList | File[],
    nodeId: Guid,
    nodeLabel: string,
    options?: EnqueueOptions,
  ) {
    const list = Array.isArray(files) ? files : Array.from(files);

    if (!this.hasActiveTasks()) {
      this.overallBytesTotal = 0;
      this.overallBytesUploaded = 0;
      this.overallBytesTransferredForSpeed = 0;
      this.overallEstimator.reset();
      this.fileConcurrency.reset();
    }

    for (const file of list) {
      this.overallBytesTotal += file.size;
      this.tasks.unshift({
        id: makeId(),
        nodeId,
        nodeLabel,
        fileName: file.name,
        bytesTotal: file.size,
        bytesUploaded: 0,
        progress01: 0,
        status: "queued",
        _file: file,
        _encrypt: options?.encrypt ?? false,
      });
    }

    this.open = true;
    this.pruneFinishedTasks();
    this.emit();
    this.pump();
  }

  private pruneFinishedTasks(): void {
    const now = Date.now();

    for (let i = this.tasks.length - 1; i >= 0; i--) {
      const t = this.tasks[i];
      if (FINISHED_TASK_STATUSES.has(this.toAppTaskStatus(t.status)) && t.completedAt && now - t.completedAt > FINISHED_TASK_TTL_MS) {
        this.tasks.splice(i, 1);
      }
    }
    for (let i = this.externalTasks.length - 1; i >= 0; i--) {
      const t = this.externalTasks[i];
      if (FINISHED_TASK_STATUSES.has(t.status) && t.completedAt && now - t.completedAt > FINISHED_TASK_TTL_MS) {
        this.externalTasks.splice(i, 1);
      }
    }

    const finished = this.tasks.filter((t) =>
      FINISHED_TASK_STATUSES.has(this.toAppTaskStatus(t.status)),
    );
    if (finished.length > MAX_FINISHED_TASKS) {
      const toRemove = finished.length - MAX_FINISHED_TASKS;
      let removed = 0;
      for (let i = this.tasks.length - 1; i >= 0 && removed < toRemove; i--) {
        if (FINISHED_TASK_STATUSES.has(this.toAppTaskStatus(this.tasks[i].status))) {
          this.tasks.splice(i, 1);
          removed++;
        }
      }
    }

    const externalFinished = this.externalTasks.filter((t) =>
      FINISHED_TASK_STATUSES.has(t.status),
    );
    if (externalFinished.length > MAX_FINISHED_TASKS) {
      const toRemove = externalFinished.length - MAX_FINISHED_TASKS;
      let removed = 0;
      for (let i = this.externalTasks.length - 1; i >= 0 && removed < toRemove; i--) {
        if (FINISHED_TASK_STATUSES.has(this.externalTasks[i].status)) {
          this.externalTasks.splice(i, 1);
          removed++;
        }
      }
    }

    this.overallBytesTotal = this.tasks.reduce((sum, t) => sum + t.bytesTotal, 0);
    this.overallBytesUploaded = this.tasks.reduce((sum, t) => sum + t.bytesUploaded, 0);
  }

  private emit() {
    const tasks = [
      ...this.externalTasks.map((task) => this.toPublicExternalTask(task)),
      ...this.tasks.map((task) => this.toAppTask(task)),
    ];
    const bytesTotal = tasks.reduce((sum, task) => sum + task.bytesTotal, 0);
    const bytesCompleted = tasks.reduce((sum, task) => sum + task.bytesCompleted, 0);
    const progress01 = bytesTotal > 0 ? bytesCompleted / bytesTotal : 0;

    this.overallBytesTotal = bytesTotal;
    this.overallBytesUploaded = bytesCompleted;

    this.snapshot = {
      open: this.open,
      tasks,
      overall: {
        bytesTotal,
        bytesCompleted,
        progress01,
        speedBytesPerSec: this.overallEstimator.getSnapshot().rollingBytesPerSec,
      },
    };
    for (const l of this.listeners) l();
  }

  private updateExternalTask(
    taskId: string,
    update: UpdateAppTaskOptions,
  ): void {
    const task = this.externalTasks.find((x) => x.id === taskId);
    if (!task) return;

    if (update.label !== undefined) task.label = update.label;
    if (update.scopeLabel !== undefined) task.scopeLabel = update.scopeLabel;
    if (update.bytesTotal !== undefined) {
      task.bytesTotal = Math.max(0, update.bytesTotal);
      task.bytesCompleted = Math.min(task.bytesCompleted, task.bytesTotal);
      task.progress01 = task.bytesTotal > 0
        ? task.bytesCompleted / task.bytesTotal
        : 1;
    }
    if (update.status !== undefined) {
      task.status = update.status;
      if (update.status === "completed" || update.status === "failed") {
        task.completedAt = Date.now();
      }
    }
    if (update.bytesCompleted !== undefined) {
      task.bytesCompleted = Math.max(
        0,
        Math.min(task.bytesTotal, update.bytesCompleted),
      );
      task.progress01 = task.bytesTotal > 0
        ? task.bytesCompleted / task.bytesTotal
        : 1;
    }
    if (update.progress01 !== undefined) {
      task.progress01 = Math.max(0, Math.min(1, update.progress01));
      if (task.bytesTotal > 0 && update.bytesCompleted === undefined) {
        task.bytesCompleted = Math.round(task.bytesTotal * task.progress01);
      }
    }
    if (update.speedBytesPerSec !== undefined) {
      task.speedBytesPerSec = update.speedBytesPerSec;
    }

    this.emit();
  }

  private toAppTask(task: UploadTaskInternal): AppTask {
    return {
      id: task.id,
      kind: "upload",
      label: task.fileName,
      scopeLabel: task.nodeLabel,
      bytesTotal: task.bytesTotal,
      bytesCompleted: task.bytesUploaded,
      progress01: task.progress01,
      status: this.toAppTaskStatus(task.status),
      speedBytesPerSec: task.uploadSpeedBytesPerSec,
      error: task.error,
      errorKey: task.errorKey,
      errorParams: task.errorParams,
      completedAt: task.completedAt,
    };
  }

  private toPublicExternalTask(task: ExternalTaskInternal): AppTask {
    return {
      id: task.id,
      kind: task.kind,
      label: task.label,
      scopeLabel: task.scopeLabel,
      bytesTotal: task.bytesTotal,
      bytesCompleted: task.bytesCompleted,
      progress01: task.progress01,
      status: task.status,
      speedBytesPerSec: task.speedBytesPerSec,
      error: task.error,
      errorKey: task.errorKey,
      errorParams: task.errorParams,
      completedAt: task.completedAt,
    };
  }

  private toAppTaskStatus(status: UploadTaskStatus): AppTaskStatus {
    return status === "uploading" ? "running" : status;
  }

  private scheduleNodeRefresh(nodeId: Guid) {
    this.refreshNodeIds.add(nodeId);
    if (this.refreshTimeout) return;

    this.refreshTimeout = setTimeout(() => {
      const ids = Array.from(this.refreshNodeIds);
      this.refreshNodeIds.clear();
      this.refreshTimeout = null;

      for (const id of ids) {
        void refreshNodeContent(id);
      }
    }, 300);
  }

  private hasActiveTasks(): boolean {
    return this.tasks.some((t) => t.status === "queued" || t.status === "uploading" || t.status === "finalizing");
  }

  private pump() {
    if (this.pumping) return;
    this.pumping = true;

    try {
      while (this.activeUploads < this.fileConcurrency.current) {
        const next = this.tasks.find((t) => t.status === "queued");
        if (!next) {
          if (!this.hasActiveTasks()) {
            this.emit();
          }
          return;
        }

        const settings = getCachedServerSettings();
        if (!settings) {
          next.status = "failed";
          next.completedAt = Date.now();
          next.errorKey = "serverSettingsNotLoaded";
          this.emit();
          continue;
        }

        this.startTask(next, {
          maxChunkSizeBytes: settings.maxChunkSizeBytes,
          supportedHashAlgorithm: settings.supportedHashAlgorithm,
        });
      }
    } finally {
      this.pumping = false;
    }
  }

  private startTask(task: UploadTaskInternal, server: UploadServerParams) {
    this.activeUploads += 1;
    task.status = "uploading";
    task.error = undefined;
    task.uploadSpeedBytesPerSec = 0;
    task._startedAt = Date.now();
    task._sawProgress = false;
    task._laneProbeConsumed = false;
    task._bytesTransferredForSpeed = 0;

    const taskEstimator = new RollingBytesPerSecondEstimator({ windowMs: 1500, minDurationMs: 250 });
    let lastEmitTime = 0;
    const encryptionTaskRef: { current: AppTaskHandle | null } = {
      current: null,
    };
    let encryptionTaskFinished = false;

    task._laneProbeTimeout = setTimeout(() => {
      this.maybeOpenLaneForHeadOfLine(task, Date.now());
    }, uploadConfig.fileHeadOfLineProbeMs);

    this.emit();

    void (async () => {
      try {
        await uploadFileToNode({
          file: task._file,
          nodeId: task.nodeId,
          server,
          encrypt: task._encrypt,
          onEncryptProgress: (bytesEncrypted, bytesTotal) => {
            encryptionTaskRef.current ??= this.createTask({
              kind: "encrypt",
              label: task.fileName,
              scopeLabel: task.nodeLabel,
              bytesTotal,
            });
            encryptionTaskRef.current.update({
              status: "running",
              bytesTotal,
              bytesCompleted: bytesEncrypted,
            });
          },
          onEncryptComplete: () => {
            encryptionTaskFinished = true;
            encryptionTaskRef.current?.complete();
          },
          onProgress: (bytesUploaded, snapshot) => {
            const prevBytesUploaded = task.bytesUploaded;
            task.bytesUploaded = Math.min(
              task.bytesTotal,
              Math.max(0, bytesUploaded),
            );
            task.progress01 = task.bytesTotal > 0 ? task.bytesUploaded / task.bytesTotal : 1;

            const now = Date.now();
            if (task.bytesUploaded > 0) {
              task._sawProgress = true;
              this.maybeOpenLaneForHeadOfLine(task, now);
            }

            const prevSpeedBytes = task._bytesTransferredForSpeed ?? 0;
            const nextSpeedBytes = Math.max(
              prevSpeedBytes,
              snapshot?.bytesTransmitted ?? task.bytesUploaded,
            );
            const speedDelta = nextSpeedBytes - prevSpeedBytes;
            if (speedDelta > 0) {
              task._bytesTransferredForSpeed = nextSpeedBytes;
              const taskRate = taskEstimator.update(nextSpeedBytes, now);
              task.uploadSpeedBytesPerSec =
                taskRate.rollingBytesPerSec > 0 ? taskRate.rollingBytesPerSec : taskRate.averageBytesPerSec;

              this.overallBytesTransferredForSpeed += speedDelta;
              this.overallEstimator.update(this.overallBytesTransferredForSpeed, now);
            }

            const delta = task.bytesUploaded - prevBytesUploaded;
            if (delta !== 0) {
              this.overallBytesUploaded += delta;
              this.overallBytesUploaded = Math.max(
                0,
                Math.min(this.overallBytesTotal, this.overallBytesUploaded),
              );
            }

            if (
              delta < 0 ||
              now - lastEmitTime >= uploadConfig.progressEmitIntervalMs ||
              task.bytesUploaded >= task.bytesTotal
            ) {
              lastEmitTime = now;
              this.emit();
            }
          },
          onFinalizing: () => {
            task.status = "finalizing";
            this.emit();
          },
        });

        task.status = "completed";
        task.completedAt = Date.now();
        const beforeFinalize = task.bytesUploaded;
        task.bytesUploaded = task.bytesTotal;
        task.progress01 = 1;

        const finalizeDelta = task.bytesUploaded - beforeFinalize;
        if (finalizeDelta > 0) {
          this.overallBytesUploaded += finalizeDelta;
          this.overallEstimator.update(this.overallBytesUploaded, Date.now());
        }

        this.fileConcurrency.observe({
          bytes: task.bytesTotal,
          durationMs: task.completedAt - (task._startedAt ?? task.completedAt),
          succeeded: true,
        });
        this.emit();

        this.scheduleNodeRefresh(task.nodeId);
      } catch (e) {
        if (encryptionTaskRef.current && !encryptionTaskFinished) {
          encryptionTaskRef.current.fail({
            message: e instanceof Error ? e.message : undefined,
            key: e instanceof NoKeyError
              ? "encryptionVaultLocked"
              : e instanceof ClientEncryptionSizeLimitError
                ? "clientEncryptionFileTooLarge"
                : "encryptionFailed",
            params: e instanceof ClientEncryptionSizeLimitError
              ? { maxSize: formatBytes(e.maxBytes) }
              : undefined,
          });
        }

        task.status = "failed";
        task.completedAt = Date.now();
        task.error = e instanceof Error ? e.message : undefined;
        if (e instanceof NoKeyError) {
          task.errorKey = "encryptionVaultLocked";
          task.errorParams = undefined;
        } else if (e instanceof ClientEncryptionSizeLimitError) {
          task.errorKey = "clientEncryptionFileTooLarge";
          task.errorParams = { maxSize: formatBytes(e.maxBytes) };
        } else {
          task.errorKey = "uploadFailed";
          task.errorParams = undefined;
        }
        this.fileConcurrency.observe({
          bytes: task.bytesTotal,
          durationMs: task.completedAt - (task._startedAt ?? task.completedAt),
          succeeded: false,
        });
        this.emit();
      } finally {
        if (task._laneProbeTimeout) {
          clearTimeout(task._laneProbeTimeout);
          task._laneProbeTimeout = undefined;
        }
        this.activeUploads = Math.max(0, this.activeUploads - 1);
        this.pump();
      }
    })();
  }

  private maybeOpenLaneForHeadOfLine(task: UploadTaskInternal, now: number) {
    if (
      task._laneProbeConsumed ||
      !task._sawProgress ||
      this.fileConcurrency.current > 1 ||
      !this.tasks.some((t) => t.status === "queued")
    ) {
      return;
    }

    const startedAt = task._startedAt ?? now;
    if (now - startedAt < uploadConfig.fileHeadOfLineProbeMs) {
      return;
    }

    task._laneProbeConsumed = true;
    if (this.fileConcurrency.tryIncrease()) {
      this.pump();
    }
  }

  destroy(): void {
    if (this.pruneIntervalId) {
      clearInterval(this.pruneIntervalId);
      this.pruneIntervalId = null;
    }
    globalHashWorkerPool.destroy();
  }

  getHashWorkerPoolStats() {
    return globalHashWorkerPool.getStats();
  }
}

export const uploadManager = new UploadManager();
