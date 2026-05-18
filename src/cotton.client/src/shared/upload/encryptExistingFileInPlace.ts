import type { Guid } from "../api/layoutsApi";
import { filesApi } from "../api/filesApi";
import type { NodeFileManifestDto } from "../api/nodesApi";
import {
  DISPLAY_META_KEY,
  ENCRYPTED_CONTENT_TYPE,
  ENCRYPTED_FLAG_KEY,
  assertClientEncryptionBlobPipelineSize,
  encryptDisplayMeta,
  encryptFileToBlob,
  requireMasterKey,
} from "../crypto";
import type { UploadFileToNodeCallbacks, UploadServerParams } from "./types";
import { uploadBlobToChunks } from "./uploadBlobToChunks";
import { createOpaqueServerFileName } from "./uploadFileToNode";

type ExistingFileForEncryption = Pick<
  NodeFileManifestDto,
  "id" | "name" | "contentType" | "sizeBytes"
>;

export async function encryptExistingFileInPlace(options: {
  file: ExistingFileForEncryption;
  targetNodeId: Guid;
  server: UploadServerParams;
  onEncryptProgress?: UploadFileToNodeCallbacks["onEncryptProgress"];
  onUploadProgress?: (bytesUploaded: number, bytesTotal: number) => void;
  onFinalizing?: UploadFileToNodeCallbacks["onFinalizing"];
}): Promise<void> {
  const { file, server, targetNodeId } = options;
  assertClientEncryptionBlobPipelineSize(file.sizeBytes, "encrypt");

  const masterKey = requireMasterKey();
  const downloadUrl = await filesApi.getDownloadLink(file.id);
  const response = await fetch(downloadUrl);
  if (!response.ok) {
    throw new Error(
      `Failed to fetch file for encryption: ${response.status} ${response.statusText}`,
    );
  }

  const plaintext = await response.blob();
  const contentType =
    file.contentType || plaintext.type || "application/octet-stream";
  const encryptedDisplayMeta = await encryptDisplayMeta({
    name: file.name,
    contentType,
  });
  const encryptedBlob = await encryptFileToBlob(
    plaintext,
    masterKey,
    undefined,
    { onProgress: options.onEncryptProgress },
  );

  const opaqueName = createOpaqueServerFileName();
  const { chunkHashes, fileHash } = await uploadBlobToChunks({
    blob: encryptedBlob,
    fileName: file.name,
    server,
    onProgress: (bytesUploaded) => {
      options.onUploadProgress?.(bytesUploaded, encryptedBlob.size);
    },
  });

  options.onFinalizing?.();

  await filesApi.updateFileContent(file.id, {
    nodeId: targetNodeId,
    chunkHashes,
    name: opaqueName,
    contentType: ENCRYPTED_CONTENT_TYPE,
    hash: fileHash,
    originalNodeFileId: null,
    metadata: {
      [ENCRYPTED_FLAG_KEY]: "true",
      [DISPLAY_META_KEY]: encryptedDisplayMeta,
    },
  });
}
