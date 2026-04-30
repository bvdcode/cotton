import type { Guid } from "../api/layoutsApi";
import { useNodesStore } from "../store/nodesStore";
import { useSettingsStore } from "../store/settingsStore";
import { AdaptiveConcurrencyController } from "./AdaptiveConcurrencyController";
import { uploadConfig } from "./config";
import { uploadFileToNode } from "./uploadFileToNode";
import { RollingBytesPerSecondEstimator } from "./RollingBytesPerSecondEstimator";
import { globalHashWorkerPool } from "./hash/HashWorkerPool";
import type { UploadServerParams } from "./types";

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
  uploadSpeedBytesPerSec?: number;
  completedAt?: number;
}

interface UploadTaskInternal extends UploadTask {
  _file: File;
  _startedAt?: number;
  _sawProgress?: boolean;
  _laneProbeConsumed?: boolean;
  _laneProbeTimeout?: ReturnType<typeof setTimeout>;
}

export interface UploadFilePickerContext {
  nodeId: Guid;
  nodeLabel: string;
  multiple?: boolean;
  accept?: string;
}

type Listener = () => void;

const makeId = () => `${Date.now()}-${Math.random().toString(16).slice(2)}`;

interface UploadOverallStats {
  bytesTotal: number;
  bytesUploaded: number;
  progress01: number;
  uploadSpeedBytesPerSec: number;
}

const MAX_FINISHED_TASKS = 10000;
const FINISHED_TASK_TTL_MS = 30 * 60 * 1000;
const PRUNE_INTERVAL_MS = 5 * 60 * 1000;

export class UploadManager {
  private readonly listeners = new Set<Listener>();
  private readonly tasks: UploadTaskInternal[] = [];
  private pumping = false;
  private activeUploads = 0;
  private readonly fileConcurrency = new AdaptiveConcurrencyController({
    maxConcurrency: uploadConfig.maxConcurrentFileUploads,
    rampUpDurationMs: uploadConfig.concurrencyRampUpMs,
  });
  private open = false;
  private snapshot: { open: boolean; tasks: UploadTask[]; overall: UploadOverallStats } = {
    open: false,
    tasks: [],
    overall: { bytesTotal: 0, bytesUploaded: 0, progress01: 0, uploadSpeedBytesPerSec: 0 },
  };
  private readonly refreshNodeIds = new Set<Guid>();
  private refreshTimeout: ReturnType<typeof setTimeout> | null = null;

  private overallBytesTotal = 0;
  private overallBytesUploaded = 0;
  private overallEstimator = new RollingBytesPerSecondEstimator({ windowMs: 2000, minDurationMs: 300 });

  private filePickerOpen: ((options: { multiple: boolean; accept?: string }) => void) | null = null;
  private pendingFilePickerContext: UploadFilePickerContext | null = null;
  private pruneIntervalId: ReturnType<typeof setInterval> | null = null;

  constructor() {
    this.pruneIntervalId = setInterval(() => {
      if (this.tasks.length > 0) {
        const before = this.tasks.length;
        this.pruneFinishedTasks();
        if (this.tasks.length !== before) {
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

  getSnapshot(): { open: boolean; tasks: UploadTask[]; overall: UploadOverallStats } {
    return this.snapshot;
  }

  clearFinished(options?: { includeCompleted?: boolean; includeFailed?: boolean }) {
    const includeCompleted = options?.includeCompleted ?? true;
    const includeFailed = options?.includeFailed ?? true;

    const remaining = this.tasks.filter((t) => {
      if (t.status === "completed") return !includeCompleted;
      if (t.status === "failed") return !includeFailed;
      return true;
    });

    if (remaining.length === this.tasks.length) return;

    this.tasks.length = 0;
    this.tasks.push(...remaining);

    this.overallBytesTotal = this.tasks.reduce((sum, t) => sum + t.bytesTotal, 0);
    this.overallBytesUploaded = this.tasks.reduce((sum, t) => sum + t.bytesUploaded, 0);
    this.overallEstimator.reset();

    if (this.tasks.length === 0) {
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

  enqueue(files: FileList | File[], nodeId: Guid, nodeLabel: string) {
    const list = Array.isArray(files) ? files : Array.from(files);

    if (!this.hasActiveTasks()) {
      this.overallBytesTotal = 0;
      this.overallBytesUploaded = 0;
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
      });
    }

    this.open = true;
    this.pruneFinishedTasks();
    this.emit();
    this.pump();
  }

  private pruneFinishedTasks(): void {
    const finishedStatuses: UploadTaskStatus[] = ["completed", "failed"];
    const now = Date.now();

    for (let i = this.tasks.length - 1; i >= 0; i--) {
      const t = this.tasks[i];
      if (finishedStatuses.includes(t.status) && t.completedAt && now - t.completedAt > FINISHED_TASK_TTL_MS) {
        this.tasks.splice(i, 1);
      }
    }

    const finished = this.tasks.filter((t) => finishedStatuses.includes(t.status));
    if (finished.length > MAX_FINISHED_TASKS) {
      const toRemove = finished.length - MAX_FINISHED_TASKS;
      let removed = 0;
      for (let i = this.tasks.length - 1; i >= 0 && removed < toRemove; i--) {
        if (finishedStatuses.includes(this.tasks[i].status)) {
          this.tasks.splice(i, 1);
          removed++;
        }
      }
    }

    this.overallBytesTotal = this.tasks.reduce((sum, t) => sum + t.bytesTotal, 0);
    this.overallBytesUploaded = this.tasks.reduce((sum, t) => sum + t.bytesUploaded, 0);
  }

  private emit() {
    const progress01 = this.overallBytesTotal > 0 ? this.overallBytesUploaded / this.overallBytesTotal : 0;
    this.snapshot = {
      open: this.open,
      tasks: this.tasks.slice(),
      overall: {
        bytesTotal: this.overallBytesTotal,
        bytesUploaded: this.overallBytesUploaded,
        progress01,
        uploadSpeedBytesPerSec: this.overallEstimator.getSnapshot().rollingBytesPerSec,
      },
    };
    for (const l of this.listeners) l();
  }

  private scheduleNodeRefresh(nodeId: Guid) {
    this.refreshNodeIds.add(nodeId);
    if (this.refreshTimeout) return;

    this.refreshTimeout = setTimeout(() => {
      const ids = Array.from(this.refreshNodeIds);
      this.refreshNodeIds.clear();
      this.refreshTimeout = null;

      for (const id of ids) {
        void useNodesStore.getState().refreshNodeContent(id);
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

        const settings = useSettingsStore.getState().data;
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

    const taskEstimator = new RollingBytesPerSecondEstimator({ windowMs: 1500, minDurationMs: 250 });
    let lastEmitTime = 0;

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
          onProgress: (bytesUploaded) => {
            const prevBytesUploaded = task.bytesUploaded;
            task.bytesUploaded = Math.min(
              task.bytesTotal,
              Math.max(task.bytesUploaded, bytesUploaded),
            );
            task.progress01 = task.bytesTotal > 0 ? task.bytesUploaded / task.bytesTotal : 1;

            const now = Date.now();
            if (task.bytesUploaded > 0) {
              task._sawProgress = true;
              this.maybeOpenLaneForHeadOfLine(task, now);
            }

            const taskRate = taskEstimator.update(task.bytesUploaded, now);
            task.uploadSpeedBytesPerSec =
              taskRate.rollingBytesPerSec > 0 ? taskRate.rollingBytesPerSec : taskRate.averageBytesPerSec;

            const delta = task.bytesUploaded - prevBytesUploaded;
            if (delta > 0) {
              this.overallBytesUploaded += delta;
              this.overallEstimator.update(this.overallBytesUploaded, now);
            }

            if (
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
        task.status = "failed";
        task.completedAt = Date.now();
        task.error = e instanceof Error ? e.message : undefined;
        task.errorKey = "uploadFailed";
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
