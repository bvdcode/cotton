import type { Guid } from "../api/layoutsApi";
import type { NodeFileManifestDto } from "../api/nodesApi";
import { filesApi } from "../api/filesApi";
import {
  DISPLAY_META_KEY,
  ENCRYPTED_CONTENT_TYPE,
  ENCRYPTED_FLAG_KEY,
  encryptDisplayMeta,
  encryptFileToBlob,
  assertClientEncryptionBlobPipelineSize,
  randomBytes,
  requireMasterKey,
} from "../crypto";
import { uploadConfig } from "./config";
import type {
  UploadFileToNodeCallbacks,
  UploadFileToNodeOptions,
  UploadServerParams,
} from "./types";
import { uploadBlobToChunks } from "./uploadBlobToChunks";

export type { UploadServerParams } from "./types";

export async function uploadFileToNode(options: {
  file: File;
  nodeId: Guid;
  replaceNodeFileId?: Guid | null;
  server: UploadServerParams;
  client?: UploadFileToNodeOptions;
  encrypt?: boolean;
  onProgress?: UploadFileToNodeCallbacks["onProgress"];
  onFinalizing?: UploadFileToNodeCallbacks["onFinalizing"];
  onEncryptProgress?: UploadFileToNodeCallbacks["onEncryptProgress"];
  onEncryptComplete?: UploadFileToNodeCallbacks["onEncryptComplete"];
}): Promise<NodeFileManifestDto> {
  const { file, nodeId, server } = options;
  const originalContentType =
    file.type && file.type.length > 0 ? file.type : ENCRYPTED_CONTENT_TYPE;
  let uploadBlob: Blob = file;
  let contentType = originalContentType;
  let name = file.name;
  let metadata: Record<string, string> | undefined;

  if (options.encrypt) {
    assertClientEncryptionBlobPipelineSize(file.size, "encrypt");

    const masterKey = requireMasterKey();
    const encryptedDisplayMeta = await encryptDisplayMeta({
      name: file.name,
      contentType: originalContentType,
    });

    uploadBlob = await encryptFileToBlob(file, masterKey, undefined, {
      onProgress: options.onEncryptProgress,
    });
    options.onEncryptComplete?.();
    contentType = ENCRYPTED_CONTENT_TYPE;
    name = createOpaqueServerFileName();
    metadata = {
      [ENCRYPTED_FLAG_KEY]: "true",
      [DISPLAY_META_KEY]: encryptedDisplayMeta,
    };
  }

  const { chunkHashes, fileHash } = await uploadBlobToChunks({
    blob: uploadBlob,
    fileName: file.name,
    server,
    client: {
      sendChunkHashForValidation:
        options.client?.sendChunkHashForValidation ??
        uploadConfig.sendChunkHashForValidation,
      concurrency:
        options.client?.concurrency ?? uploadConfig.maxChunkUploadConcurrency,
    },
    onProgress: options.onProgress,
  });

  options.onFinalizing?.();

  const request = {
    nodeId,
    chunkHashes,
    name,
    contentType,
    hash: fileHash,
    originalNodeFileId: null,
    metadata,
  };

  if (options.replaceNodeFileId) {
    return filesApi.updateFileContent(options.replaceNodeFileId, request);
  }

  return filesApi.createFromChunks(request);
}

export function createOpaqueServerFileName(): string {
  if (typeof globalThis.crypto?.randomUUID === "function") {
    return globalThis.crypto.randomUUID();
  }

  const bytes = randomBytes(16);
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = Array.from(bytes, (byte) => byte.toString(16).padStart(2, "0"));

  return [
    hex.slice(0, 4).join(""),
    hex.slice(4, 6).join(""),
    hex.slice(6, 8).join(""),
    hex.slice(8, 10).join(""),
    hex.slice(10, 16).join(""),
  ].join("-");
}
