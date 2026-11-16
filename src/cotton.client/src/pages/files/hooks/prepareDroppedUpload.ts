import type { NodeFileManifestDto } from "../../../shared/api/nodesApi";
import { uploadManager } from "../../../shared/upload/UploadManager";
import type { UploadQueueBatch } from "../../../shared/upload/UploadQueueEntries";
import type {
  ConflictAction,
  NameConflictPrompt,
} from "../../../shared/types/nameConflict";
import { resolveUploadConflicts } from "../utils/uploadConflicts";
import { DroppedFolderResolver } from "./DroppedFolderResolver";
import type { DroppedFile } from "./scanDroppedFiles";
import type { DropPreparationState } from "./fileUploadTypes";

interface DroppedUploadOptions {
  nodeId: string;
  baseLabel: string;
  rootPolicyEnabled: boolean;
  isPolicyEnabledForNode: (nodeId: string) => boolean;
  confirmConflict: (prompt: NameConflictPrompt) => Promise<ConflictAction>;
  onProgress: (state: DropPreparationState) => void;
  onVaultLocked: () => void;
  onFileUploaded?: (file: NodeFileManifestDto) => void;
}

export async function prepareDroppedUpload(
  dropped: DroppedFile[],
  options: DroppedUploadOptions,
): Promise<number> {
  const { onProgress } = options;
  const folderResolver = new DroppedFolderResolver(
    options.nodeId,
    options.baseLabel,
    options.rootPolicyEnabled,
    options.isPolicyEnabledForNode,
  );
  let lastProgressTime = 0;
  const updateProgress = (
    step: "folders" | "conflicts" | "enqueue",
    processed: number,
  ) => {
    onProgress({
      active: true,
      phase: "preparing",
      step,
      filesFound: dropped.length,
      processed,
    });
  };
  updateProgress("folders", 0);
  const filesByTarget = await folderResolver.groupFiles(
    dropped,
    (processed) => {
      const now = Date.now();
      if (now - lastProgressTime < 120 && processed < dropped.length) {
        return;
      }
      lastProgressTime = now;
      updateProgress("folders", processed);
    },
  );

  const batches: UploadQueueBatch[] = [];
  let processed = 0;
  let totalEnqueued = 0;
  updateProgress("conflicts", processed);
  for (const [nodeId, bucket] of filesByTarget) {
    const decision = folderResolver.decideEncryption(nodeId);
    if (decision.vaultLocked) {
      options.onVaultLocked();
    } else {
      const content = await folderResolver.lookupFileNames(
        nodeId,
        bucket.files.map((file) => file.name),
        decision.encrypt,
      );
      const result = await resolveUploadConflicts(
        bucket.files,
        content,
        options.confirmConflict,
        dropped.length - processed - bucket.files.length,
      );
      if (result.cancelled) {
        return 0;
      }
      if (result.files.length > 0) {
        batches.push({
          files: result.files,
          nodeId,
          nodeLabel: bucket.label,
          options: {
            encrypt: decision.encrypt,
            onFileUploaded: options.onFileUploaded,
          },
        });
        totalEnqueued += result.files.length;
      }
    }
    processed += bucket.files.length;
    updateProgress("conflicts", processed);
  }
  if (batches.length > 0) {
    updateProgress("enqueue", dropped.length);
    uploadManager.enqueueBatch(batches);
  }
  return totalEnqueued;
}
