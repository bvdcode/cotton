import type { Guid } from "../api/layoutsApi";
import type { UploadServerParams } from "./uploadFileToNode";
import { uploadFileToNode } from "./uploadFileToNode";

export type UploadQueueItemStatus = "queued" | "uploading" | "completed" | "failed";

export interface UploadQueueItem {
  id: string;
  file: File;
  nodeId: Guid;
  status: UploadQueueItemStatus;
  error?: string;
}

export class UploadQueue {
  private items: UploadQueueItem[] = [];
  private running = false;

  private readonly getServerParams: () => UploadServerParams | null;

  constructor(getServerParams: () => UploadServerParams | null) {
    this.getServerParams = getServerParams;
  }

  getSnapshot(): UploadQueueItem[] {
    return this.items.slice();
  }

  enqueue(files: FileList | File[], nodeId: Guid) {
    const list = Array.isArray(files) ? files : Array.from(files);
    const now = Date.now();

    for (let i = 0; i < list.length; i += 1) {
      const file = list[i];
      this.items.push({
        id: `${now}-${i}-${file.name}`,
        file,
        nodeId,
        status: "queued",
      });
    }

    void this.pump();
  }

  private async pump() {
    if (this.running) return;
    this.running = true;

    try {
      while (true) {
        const next = this.items.find((x) => x.status === "queued");
        if (!next) return;

        const server = this.getServerParams();
        if (!server) {
          next.status = "failed";
          next.error = "Server settings are not loaded";
          continue;
        }

        next.status = "uploading";
        try {
          await uploadFileToNode({ file: next.file, nodeId: next.nodeId, server });
          next.status = "completed";
        } catch (e) {
          next.status = "failed";
          next.error = e instanceof Error ? e.message : "Upload failed";
        }
      }
    } finally {
      this.running = false;
    }
  }
}
