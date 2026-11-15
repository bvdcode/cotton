import { reportClientError } from "@shared/utils/clientDiagnostics";
import { getApiErrorMessage } from "../api/httpClient";
import type { Guid } from "../api/layoutsApi";
import type { NodeFileManifestDto } from "../api/nodesApi";
import { refreshNodeContent } from "../store/nodesActions";
import { useNodesStore } from "../store/nodesStore";
import { getCachedServerSettings } from "../api/queries/serverSettings";
import {
  getEncryptionErrorKey,
  getUploadErrorKey,
  getUploadErrorParams,
} from "./UploadErrorDetails";
import { UploadQuotaTracker } from "./UploadQuotaTracker";
import { UploadExternalTasks } from "./UploadExternalTasks";
import { toAppTask } from "./UploadTaskView";
import {
  filterFinishedTasks,
  pruneUploadTasks,
  pruneExternalTasks,
  releaseUploadInputs,
} from "./UploadTaskRetention";
import { UploadProgressTracker } from "./UploadProgressTracker";
import {
  UploadTaskRunner,
  type UploadExecutionState,
} from "./UploadTaskRunner";
import { globalHashWorkerPool } from "./hash/HashWorkerPool";
import {
  normalizeUploadQueueEntries,
  type UploadQueueEntry,
} from "./UploadQueueEntries";
import type {
  AppTaskHandle,
  AppTaskSnapshot,
  CreateAppTaskOptions,
} from "../tasks/types";

import type {
  UploadTaskInternal,
  EnqueueOptions,
  UploadFilePickerContext,
} from "./UploadTaskTypes";
export type {
  UploadTask,
  UploadTaskStatus,
  UploadTaskInternal,
  EnqueueOptions,
  UploadFilePickerContext,
} from "./UploadTaskTypes";

type Listener = () => void;

const makeId = () => `${Date.now()}-${Math.random().toString(16).slice(2)}`;

const PRUNE_INTERVAL_MS = 5 * 60 * 1000;

export class UploadManager {
  private readonly listeners = new Set<Listener>();
  private readonly tasks: UploadTaskInternal[] = [];
  private readonly external = new UploadExternalTasks(() => this.emit());
  private pumping = false;
  private readonly runner = new UploadTaskRunner({
    createEncryptionTask: (task, bytesTotal) =>
      this.createTask({
        kind: "encrypt",
        label: task.fileName,
        scopeLabel: task.nodeLabel,
        bytesTotal,
      }),
    onProgress: (task, state, bytesUploaded) =>
      this.progress.record(task, state, bytesUploaded),
    onComplete: (task, file) => this.completeUpload(task, file),
    onFailure: (task, state, error) => this.failUpload(task, state, error),
    onStatusChange: () => this.emit(),
    onCapacityAvailable: () => this.pump(),
    hasQueuedTasks: () => this.tasks.some((task) => task.status === "queued"),
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
  private readonly quota = new UploadQuotaTracker(() => this.pump());

  private readonly progress = new UploadProgressTracker(
    (task, now) => this.runner.maybeOpenLaneForHeadOfLine(task, now),
    () => this.emit(),
  );

  private filePickerOpen:
    ((options: { multiple: boolean; accept?: string }) => void) | null = null;
  private pendingFilePickerContext: UploadFilePickerContext | null = null;
  private pruneIntervalId: ReturnType<typeof setInterval> | null = null;

  constructor() {
    this.pruneIntervalId = setInterval(() => {
      if (this.tasks.length > 0 || this.external.tasks.length > 0) {
        const before = this.tasks.length + this.external.tasks.length;
        this.pruneFinishedTasks();
        if (this.tasks.length + this.external.tasks.length !== before) {
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
    const handle = this.external.createTask(options);
    this.open = true;
    this.pruneFinishedTasks();
    this.emit();
    return handle;
  }

  clearFinished(options?: {
    includeCompleted?: boolean;
    includeFailed?: boolean;
  }) {
    const includeCompleted = options?.includeCompleted ?? true;
    const includeFailed = options?.includeFailed ?? true;

    const remainingUploadTasks = filterFinishedTasks(
      this.tasks,
      includeCompleted,
      includeFailed,
    );
    const remainingExternalTasks = filterFinishedTasks(
      this.external.tasks,
      includeCompleted,
      includeFailed,
    );

    if (
      remainingUploadTasks.length === this.tasks.length &&
      remainingExternalTasks.length === this.external.tasks.length
    ) {
      return;
    }

    this.tasks.length = 0;
    this.tasks.push(...remainingUploadTasks);
    this.external.tasks.length = 0;
    this.external.tasks.push(...remainingExternalTasks);

    this.progress.recount(this.tasks);

    if (this.tasks.length === 0 && this.external.tasks.length === 0) {
      this.open = false;
    }

    this.emit();
  }

  setFilePickerOpen(
    fn: ((options: { multiple: boolean; accept?: string }) => void) | null,
  ) {
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
    if (context.onFilesSelected) {
      context.onFilesSelected(files);
      return;
    }
    this.enqueue(files, context.nodeId, context.nodeLabel);
  }

  enqueue(
    files: FileList | UploadQueueEntry[],
    nodeId: Guid,
    nodeLabel: string,
    options?: EnqueueOptions,
  ): string[] {
    const list = normalizeUploadQueueEntries(files);
    const taskIds: string[] = [];
    const startsNewBatch = !this.hasActiveTasks();

    if (startsNewBatch) {
      this.progress.beginBatch();
      this.runner.reset();
      if (!options?.sharedUpload) {
        this.quota.beginBatch();
      }
    }

    for (const file of list) {
      this.progress.addFile(file.file.size);
      const id = makeId();
      taskIds.push(id);
      this.tasks.unshift({
        id,
        nodeId,
        nodeLabel,
        fileName: file.file.name,
        bytesTotal: file.file.size,
        bytesUploaded: 0,
        progress01: 0,
        status: "queued",
        _file: file.file,
        _sharedUpload: options?.sharedUpload,
        _encrypt: file.encrypt === true || options?.encrypt === true,
        _replaceNodeFileId: file.replaceNodeFileId,
        _onFileUploaded: options?.onFileUploaded,
      });
    }

    this.open = true;
    this.pruneFinishedTasks();
    this.emit();
    this.pump();
    return taskIds;
  }

  private pruneFinishedTasks(): void {
    pruneUploadTasks(this.tasks);
    pruneExternalTasks(this.external.tasks);
    this.progress.countTasks(this.tasks);
  }

  private emit() {
    const tasks = [
      ...this.external.getPublicTasks(),
      ...this.tasks.map((task) => toAppTask(task)),
    ];
    const bytesTotal = tasks.reduce((sum, task) => sum + task.bytesTotal, 0);
    const bytesCompleted = tasks.reduce(
      (sum, task) => sum + task.bytesCompleted,
      0,
    );
    const progress01 = bytesTotal > 0 ? bytesCompleted / bytesTotal : 0;

    this.progress.syncSnapshot(bytesTotal, bytesCompleted);

    this.snapshot = {
      open: this.open,
      tasks,
      overall: {
        bytesTotal,
        bytesCompleted,
        progress01,
        speedBytesPerSec: this.progress.getSpeed(),
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
        void refreshNodeContent(id);
      }
    }, 300);
  }

  private hasActiveTasks(): boolean {
    return this.tasks.some(
      (t) =>
        t.status === "queued" ||
        t.status === "uploading" ||
        t.status === "finalizing",
    );
  }

  private pump() {
    if (this.pumping) return;
    this.pumping = true;

    try {
      while (this.runner.canStart()) {
        const next = this.tasks.find((t) => t.status === "queued");
        if (!next) {
          if (!this.hasActiveTasks()) {
            this.emit();
          }
          return;
        }

        const settings =
          next._sharedUpload?.server ?? getCachedServerSettings();
        if (!settings) {
          next.status = "failed";
          next.completedAt = Date.now();
          next.errorKey = "serverSettingsNotLoaded";
          releaseUploadInputs(next);
          this.emit();
          continue;
        }

        if (!next._sharedUpload && this.quota.shouldRefresh()) {
          this.quota.refresh();
          return;
        }

        if (!next._sharedUpload && !this.quota.tryReserve(next)) {
          releaseUploadInputs(next);
          this.emit();
          continue;
        }

        this.runner.startTask(next, {
          maxChunkSizeBytes: settings.maxChunkSizeBytes,
          supportedHashAlgorithm: settings.supportedHashAlgorithm,
        });
      }
    } finally {
      this.pumping = false;
    }
  }

  private completeUpload(
    task: UploadTaskInternal,
    uploadedFile: NodeFileManifestDto | undefined,
  ): void {
    if (uploadedFile) {
      if (!task._sharedUpload) {
        useNodesStore.getState().upsertFileInCache(task.nodeId, uploadedFile);
      }
      try {
        task._onFileUploaded?.(uploadedFile);
      } catch (listenerError) {
        reportClientError("Upload completion listener failed:", listenerError);
      }
    }

    task.status = "completed";
    this.quota.release(task, true);
    task.completedAt = Date.now();
    const beforeFinalize = task.bytesUploaded;
    task.bytesUploaded = task.bytesTotal;
    task.progress01 = 1;

    this.progress.complete(task, beforeFinalize);

    this.runner.observe(task, true);
    const refreshPrivateNode = !task._sharedUpload;
    releaseUploadInputs(task);
    this.emit();

    if (refreshPrivateNode) {
      this.scheduleNodeRefresh(task.nodeId);
    }
  }

  private failUpload(
    task: UploadTaskInternal,
    state: UploadExecutionState,
    error: Error | null,
  ): void {
    this.quota.release(task, false);
    const errorMessage = getApiErrorMessage(error) ?? error?.message;
    if (state.encryptionTask && !state.encryptionTaskFinished) {
      state.encryptionTask.fail({
        message: errorMessage,
        key: getEncryptionErrorKey(error),
        params: getUploadErrorParams(error),
      });
    }

    task.status = "failed";
    task.completedAt = Date.now();
    task.error = errorMessage;
    task.errorKey = getUploadErrorKey(error);
    task.errorParams = getUploadErrorParams(error);
    this.runner.observe(task, false);
    releaseUploadInputs(task);
    this.emit();
  }

  destroy(): void {
    this.progress.destroy();
    if (this.pruneIntervalId) {
      clearInterval(this.pruneIntervalId);
      this.pruneIntervalId = null;
    }
    if (this.refreshTimeout) {
      clearTimeout(this.refreshTimeout);
      this.refreshTimeout = null;
      this.refreshNodeIds.clear();
    }
    this.runner.destroy(this.tasks);
    globalHashWorkerPool.destroy();
  }

  getHashWorkerPoolStats() {
    return globalHashWorkerPool.getStats();
  }
}

export const uploadManager = new UploadManager();
