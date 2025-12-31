import type { Guid } from "../api/layoutsApi";
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
}

type Listener = () => void;

const makeId = () => `${Date.now()}-${Math.random().toString(16).slice(2)}`;

export class UploadManager {
  private readonly listeners = new Set<Listener>();
  private readonly tasks: UploadTask[] = [];
  private running = false;
  private open = false;
  private snapshot: { open: boolean; tasks: UploadTask[] } = { open: false, tasks: [] };

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

  enqueue(files: FileList | File[], nodeId: Guid, nodeLabel: string) {
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
          // Auto-close only if user closed it; if open, keep open.
          if (!this.hasActiveTasks()) this.emit();
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
