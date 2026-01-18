import type { Guid } from "../api/layoutsApi";
import { filesApi } from "../api/filesApi";
import { uploadConfig } from "./config";
import type { UploadFileToNodeCallbacks, UploadFileToNodeOptions, UploadServerParams } from "./types";
import { uploadBlobToChunks } from "./uploadBlobToChunks";

export type { UploadServerParams } from "./types";

export async function uploadFileToNode(options: {
  file: File;
  nodeId: Guid;
  server: UploadServerParams;
  client?: UploadFileToNodeOptions;
  onProgress?: UploadFileToNodeCallbacks["onProgress"];
  onFinalizing?: UploadFileToNodeCallbacks["onFinalizing"];
}): Promise<void> {
  const { file, nodeId, server } = options;

  const { chunkHashes, fileHash } = await uploadBlobToChunks({
    blob: file,
    fileName: file.name,
    server,
    client: {
      sendChunkHashForValidation:
        options.client?.sendChunkHashForValidation ?? uploadConfig.sendChunkHashForValidation,
      concurrency: options.client?.concurrency ?? uploadConfig.chunkUploadConcurrency,
    },
    onProgress: options.onProgress,
  });

  options.onFinalizing?.();

  await filesApi.createFromChunks({
    nodeId,
    chunkHashes,
    name: file.name,
    contentType: file.type && file.type.length > 0 ? file.type : "application/octet-stream",
    hash: fileHash,
    originalNodeFileId: null,
  });
}
