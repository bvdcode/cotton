import type { Guid } from "../api/layoutsApi";
import { useNodesStore } from "../store/nodesStore";
import { useSettingsStore } from "../store/settingsStore";
import { uploadFileToNode } from "./uploadFileToNode";

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

export class UploadManager {
  private readonly listeners = new Set<Listener>();
  private readonly tasks: UploadTask[] = [];
  private running = false;
  private open = false;
  private snapshot: { open: boolean; tasks: UploadTask[] } = { open: false, tasks: [] };

  private autoCloseTimeout: ReturnType<typeof setTimeout> | null = null;
  private readonly refreshNodeIds = new Set<Guid>();
  private refreshTimeout: ReturnType<typeof setTimeout> | null = null;

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

  getSnapshot(): { open: boolean; tasks: UploadTask[] } {
    return this.snapshot;
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
    if (this.autoCloseTimeout) {
      clearTimeout(this.autoCloseTimeout);
      this.autoCloseTimeout = null;
    }

    const list = Array.isArray(files) ? files : Array.from(files);
    for (const file of list) {
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
    this.emit();

    void this.pump();
  }

  private emit() {
    // useSyncExternalStore requires the snapshot result to be referentially stable
    // between store updates.
    this.snapshot = { open: this.open, tasks: this.tasks.slice() };
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
        void useNodesStore.getState().loadNode(id);
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

            // Auto-close a moment after completion so user sees the result.
            if (this.open && !this.autoCloseTimeout) {
              this.autoCloseTimeout = setTimeout(() => {
                // Only close if nothing new started.
                if (!this.hasActiveTasks()) {
                  this.open = false;
                  this.emit();
                }
                this.autoCloseTimeout = null;
              }, 10_000);
            }
          }
          return;
        }

        const settings = useSettingsStore.getState().data;
        if (!settings) {
          next.status = "failed";
          next.error = "Server settings are not loaded";
          this.emit();
          continue;
        }

        const file = (next as unknown as { _file: File })._file;
        next.status = "uploading";
        next.error = undefined;
        next.uploadSpeedBytesPerSec = 0;
        
        let lastProgressTime = Date.now();
        let lastProgressBytes = 0;
        
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
              next.bytesUploaded = Math.min(next.bytesTotal, bytesUploaded);
              next.progress01 = next.bytesTotal > 0 ? next.bytesUploaded / next.bytesTotal : 1;
              
              // Calculate upload speed
              const now = Date.now();
              const timeDelta = now - lastProgressTime;
              if (timeDelta > 0) {
                const bytesDelta = next.bytesUploaded - lastProgressBytes;
                // Calculate speed in bytes/sec with smoothing
                const currentSpeed = (bytesDelta / timeDelta) * 1000;
                next.uploadSpeedBytesPerSec = next.uploadSpeedBytesPerSec 
                  ? next.uploadSpeedBytesPerSec * 0.7 + currentSpeed * 0.3 
                  : currentSpeed;
                lastProgressTime = now;
                lastProgressBytes = next.bytesUploaded;
              }
              
              this.emit();
            },
            onFinalizing: () => {
              next.status = "finalizing";
              this.emit();
            },
          });

          next.status = "completed";
          next.bytesUploaded = next.bytesTotal;
          next.progress01 = 1;
          this.emit();

          // Ensure the Files UI picks up the new file manifests.
          this.scheduleNodeRefresh(next.nodeId);
        } catch (e) {
          next.status = "failed";
          next.error = e instanceof Error ? e.message : "Upload failed";
          this.emit();
        }
      }
    } finally {
      this.running = false;
    }
  }
}

export const uploadManager = new UploadManager();
