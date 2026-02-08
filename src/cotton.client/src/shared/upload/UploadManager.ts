import type { Guid } from "../api/layoutsApi";
import { useNodesStore } from "../store/nodesStore";
import { useSettingsStore } from "../store/settingsStore";
import { uploadFileToNode } from "./uploadFileToNode";
import { RollingBytesPerSecondEstimator } from "./RollingBytesPerSecondEstimator";
import { globalHashWorkerPool } from "./hash/HashWorkerPool";

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

const MAX_FINISHED_TASKS = 200;

export class UploadManager {
  private readonly listeners = new Set<Listener>();
  private readonly tasks: UploadTask[] = [];
  private running = false;
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

    // If there are no active uploads, start a fresh overall session.
    if (!this.hasActiveTasks()) {
      this.overallBytesTotal = 0;
      this.overallBytesUploaded = 0;
      this.overallEstimator.reset();
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
      });
      // Store the File object on the task instance via a hidden symbol.
      (this.tasks[0] as unknown as { _file: File })._file = file;
    }

    // Pop open the widget.
    this.open = true;
    this.pruneFinishedTasks();
    this.emit();

    void this.pump();
  }

  private pruneFinishedTasks(): void {
    const finishedStatuses: UploadTaskStatus[] = ["completed", "failed"];
    const finished = this.tasks.filter((t) => finishedStatuses.includes(t.status));

    if (finished.length <= MAX_FINISHED_TASKS) return;

    const toRemove = finished.length - MAX_FINISHED_TASKS;
    let removed = 0;

    for (let i = this.tasks.length - 1; i >= 0 && removed < toRemove; i--) {
      if (finishedStatuses.includes(this.tasks[i].status)) {
        this.tasks.splice(i, 1);
        removed++;
      }
    }

    this.overallBytesTotal = this.tasks.reduce((sum, t) => sum + t.bytesTotal, 0);
    this.overallBytesUploaded = this.tasks.reduce((sum, t) => sum + t.bytesUploaded, 0);
  }

  private emit() {
    // useSyncExternalStore requires the snapshot result to be referentially stable
    // between store updates.
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

  private async pump() {
    if (this.running) return;
    this.running = true;

    try {
      while (true) {
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
          next.errorKey = "serverSettingsNotLoaded";
          this.emit();
          continue;
        }

        const file = (next as unknown as { _file: File })._file;
        next.status = "uploading";
        next.error = undefined;
        next.uploadSpeedBytesPerSec = 0;

        const taskEstimator = new RollingBytesPerSecondEstimator({ windowMs: 1500, minDurationMs: 250 });
        let lastEmitTime = 0;

        this.emit();

        try {
          await uploadFileToNode({
            file,
            nodeId: next.nodeId,
            server: {
              maxChunkSizeBytes: settings.maxChunkSizeBytes,
              supportedHashAlgorithm: settings.supportedHashAlgorithm,
            },
            onProgress: (bytesUploaded) => {
              const prevBytesUploaded = next.bytesUploaded;
              next.bytesUploaded = Math.min(next.bytesTotal, bytesUploaded);
              next.progress01 = next.bytesTotal > 0 ? next.bytesUploaded / next.bytesTotal : 1;

              const now = Date.now();
              const taskRate = taskEstimator.update(next.bytesUploaded, now);
              next.uploadSpeedBytesPerSec =
                taskRate.rollingBytesPerSec > 0 ? taskRate.rollingBytesPerSec : taskRate.averageBytesPerSec;

              const delta = next.bytesUploaded - prevBytesUploaded;
              if (delta > 0) {
                this.overallBytesUploaded += delta;
                this.overallEstimator.update(this.overallBytesUploaded, now);
              }

              // Throttle UI updates; speed estimation remains accurate.
              if (now - lastEmitTime >= 100 || next.bytesUploaded >= next.bytesTotal) {
                lastEmitTime = now;
                this.emit();
              }
            },
            onFinalizing: () => {
              next.status = "finalizing";
              this.emit();
            },
          });

          next.status = "completed";
          const beforeFinalize = next.bytesUploaded;
          next.bytesUploaded = next.bytesTotal;
          next.progress01 = 1;

          const finalizeDelta = next.bytesUploaded - beforeFinalize;
          if (finalizeDelta > 0) {
            this.overallBytesUploaded += finalizeDelta;
            this.overallEstimator.update(this.overallBytesUploaded, Date.now());
          }
          this.emit();

          // Ensure the Files UI picks up the new file manifests.
          this.scheduleNodeRefresh(next.nodeId);
        } catch (e) {
          next.status = "failed";
          next.error = e instanceof Error ? e.message : undefined;
          next.errorKey = "uploadFailed";
          this.emit();
        }
      }
    } finally {
      this.running = false;
    }
  }

  /**
   * Cleanup method to release hash worker pool resources.
   * Call this when the application is being unmounted or reset.
   */
  destroy(): void {
    globalHashWorkerPool.destroy();
  }

  /**
   * Get hash worker pool statistics for debugging/monitoring
   */
  getHashWorkerPoolStats() {
    return globalHashWorkerPool.getStats();
  }
}

export const uploadManager = new UploadManager();
