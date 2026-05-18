import { filesApi } from "../api/filesApi";
import type { NodeFileManifestDto } from "../api/nodesApi";
import {
  applyDisplayMetaToFile,
  assertClientEncryptionBlobPipelineSize,
  decryptBlobToBlob,
  getOriginalContentType,
  requireMasterKey,
} from "../crypto";
import type { ReadableFileCallbacks } from "../crypto/downloadDecrypt";
import type { UploadServerParams } from "./types";
import { uploadBlobToChunks } from "./uploadBlobToChunks";

type ExistingFileForDecryption = Pick<
  NodeFileManifestDto,
  "id" | "name" | "contentType" | "sizeBytes" | "metadata"
>;

export async function decryptExistingFileInPlace(options: {
  file: ExistingFileForDecryption;
  targetNodeId: string;
  server: UploadServerParams;
  onDecryptProgress?: ReadableFileCallbacks["onDecryptProgress"];
  onUploadProgress?: (bytesUploaded: number, bytesTotal: number) => void;
  onFinalizing?: () => void;
}): Promise<void> {
  const { file, server, targetNodeId } = options;
  assertClientEncryptionBlobPipelineSize(file.sizeBytes, "decrypt");

  const masterKey = requireMasterKey();
  const displayFile = await applyDisplayMetaToFile(file);
  const contentType =
    displayFile.contentType ||
    getOriginalContentType(file.metadata) ||
    "application/octet-stream";
  const downloadUrl = await filesApi.getDownloadLink(file.id);
  const response = await fetch(downloadUrl);
  if (!response.ok) {
    throw new Error(
      `Failed to fetch file for decryption: ${response.status} ${response.statusText}`,
    );
  }

  const ciphertext = await response.blob();
  const plaintext = await decryptBlobToBlob(ciphertext, masterKey, contentType, {
    onProgress: options.onDecryptProgress,
  });

  const { chunkHashes, fileHash } = await uploadBlobToChunks({
    blob: plaintext,
    fileName: displayFile.name,
    server,
    onProgress: (bytesUploaded) => {
      options.onUploadProgress?.(bytesUploaded, plaintext.size);
    },
  });

  options.onFinalizing?.();

  await filesApi.updateFileContent(file.id, {
    nodeId: targetNodeId,
    chunkHashes,
    name: displayFile.name,
    contentType,
    hash: fileHash,
    originalNodeFileId: null,
    metadata: {},
  });
}
