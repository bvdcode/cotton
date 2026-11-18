import type { Guid } from "../api/layoutsApi";
import type { NodeFileManifestDto } from "../api/nodesApi";
import { refreshNodeContent } from "../store/nodesActions";
import { useNodesStore } from "../store/nodesStore";

const UPLOAD_LISTING_UPDATE_DELAY_MS = 300;

export class UploadListingUpdates {
  private readonly pending = new Map<Guid, Map<Guid, NodeFileManifestDto>>();
  private timer: ReturnType<typeof setTimeout> | null = null;

  enqueue(nodeId: Guid, file: NodeFileManifestDto | undefined): void {
    const files =
      this.pending.get(nodeId) ?? new Map<Guid, NodeFileManifestDto>();
    if (file) {
      files.set(file.id, file);
    }
    this.pending.set(nodeId, files);
    this.timer ??= setTimeout(
      () => this.flush(),
      UPLOAD_LISTING_UPDATE_DELAY_MS,
    );
  }

  private flush(): void {
    this.timer = null;
    for (const [nodeId, files] of this.pending) {
      if (files.size > 0) {
        useNodesStore
          .getState()
          .upsertFilesInCache(nodeId, [...files.values()]);
      }
      void refreshNodeContent(nodeId);
    }
    this.pending.clear();
  }

  destroy(): void {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
    this.pending.clear();
  }
}
