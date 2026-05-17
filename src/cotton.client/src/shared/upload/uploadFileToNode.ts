import type { Guid } from "../api/layoutsApi";
import { filesApi } from "../api/filesApi";
import {
  ENCRYPTED_CONTENT_TYPE,
  ENCRYPTED_FLAG_KEY,
  ORIGINAL_CONTENT_TYPE_KEY,
  encryptFileToBlob,
  requireMasterKey,
} from "../crypto";
import { uploadConfig } from "./config";
import type { UploadFileToNodeCallbacks, UploadFileToNodeOptions, UploadServerParams } from "./types";
import { uploadBlobToChunks } from "./uploadBlobToChunks";

export type { UploadServerParams } from "./types";

export async function uploadFileToNode(options: {
  file: File;
  nodeId: Guid;
  server: UploadServerParams;
  client?: UploadFileToNodeOptions;
  encrypt?: boolean;
  onProgress?: UploadFileToNodeCallbacks["onProgress"];
  onFinalizing?: UploadFileToNodeCallbacks["onFinalizing"];
}): Promise<void> {
  const { file, nodeId, server } = options;
  const originalContentType =
    file.type && file.type.length > 0 ? file.type : ENCRYPTED_CONTENT_TYPE;
  let uploadBlob: Blob = file;
  let contentType = originalContentType;
  let metadata: Record<string, string> | undefined;

  if (options.encrypt) {
    const masterKey = requireMasterKey();
    uploadBlob = await encryptFileToBlob(file, masterKey);
    contentType = ENCRYPTED_CONTENT_TYPE;
    metadata = {
      [ENCRYPTED_FLAG_KEY]: "true",
      [ORIGINAL_CONTENT_TYPE_KEY]: originalContentType,
    };
  }

  const { chunkHashes, fileHash } = await uploadBlobToChunks({
    blob: uploadBlob,
    fileName: file.name,
    server,
    client: {
      sendChunkHashForValidation:
        options.client?.sendChunkHashForValidation ?? uploadConfig.sendChunkHashForValidation,
      concurrency: options.client?.concurrency ?? uploadConfig.maxChunkUploadConcurrency,
    },
    onProgress: options.onProgress,
  });

  options.onFinalizing?.();

  await filesApi.createFromChunks({
    nodeId,
    chunkHashes,
    name: file.name,
    contentType,
    hash: fileHash,
    originalNodeFileId: null,
    metadata,
  });
}
