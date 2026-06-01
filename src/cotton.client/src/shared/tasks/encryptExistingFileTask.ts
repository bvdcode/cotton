import type { NodeFileManifestDto } from "../api/nodesApi";
import {
  ClientEncryptionSizeLimitError,
  NoKeyError,
} from "../crypto";
import { encryptExistingFileInPlace } from "../upload/encryptExistingFileInPlace";
import { decryptExistingFileInPlace } from "../upload/decryptExistingFileInPlace";
import type { UploadServerParams } from "../upload/types";
import { formatBytes } from "../utils/formatBytes";
import { taskManager } from "./taskManager";
import type { AppTaskHandle } from "./types";

export type ExistingFileEncryptionTaskFile = Pick<
  NodeFileManifestDto,
  "id" | "name" | "contentType" | "sizeBytes"
>;

export type ExistingFileDecryptionTaskFile = Pick<
  NodeFileManifestDto,
  "id" | "name" | "contentType" | "sizeBytes" | "metadata"
>;

export async function encryptExistingFileWithTask(options: {
  file: ExistingFileEncryptionTaskFile;
  targetNodeId: string;
  scopeLabel?: string;
  server: UploadServerParams;
  task?: AppTaskHandle;
}): Promise<void> {
  const { file, targetNodeId, scopeLabel, server } = options;
  const task = options.task ?? taskManager.createTask({
    kind: "encrypt",
    label: file.name,
    scopeLabel,
    bytesTotal: file.sizeBytes,
  });

  try {
    task.update({ status: "running" });
    await encryptExistingFileInPlace({
      file,
      targetNodeId,
      server,
      onEncryptProgress: (bytesEncrypted, bytesTotal) => {
        task.update({
          status: "running",
          bytesTotal,
          bytesCompleted: bytesEncrypted,
        });
      },
      onUploadProgress: (bytesUploaded, uploadTotal) => {
        const bytesTotal = file.sizeBytes + uploadTotal;
        task.update({
          status: "running",
          bytesTotal,
          bytesCompleted: file.sizeBytes + bytesUploaded,
        });
      },
      onFinalizing: () => {
        task.update({ status: "finalizing" });
      },
    });
    task.complete();
  } catch (error) {
    task.fail({
      message: error instanceof Error ? error.message : undefined,
      key:
        error instanceof NoKeyError
          ? "encryptionVaultLocked"
          : error instanceof ClientEncryptionSizeLimitError
            ? "clientEncryptionFileTooLarge"
            : "encryptionFailed",
      params:
        error instanceof ClientEncryptionSizeLimitError
          ? { maxSize: formatBytes(error.maxBytes) }
          : undefined,
    });
    throw error;
  }
}

export async function decryptExistingFileWithTask(options: {
  file: ExistingFileDecryptionTaskFile;
  targetNodeId: string;
  scopeLabel?: string;
  server: UploadServerParams;
  task?: AppTaskHandle;
}): Promise<void> {
  const { file, targetNodeId, scopeLabel, server } = options;
  const task = options.task ?? taskManager.createTask({
    kind: "decrypt",
    label: file.name,
    scopeLabel,
    bytesTotal: file.sizeBytes,
  });

  try {
    task.update({ status: "running" });
    await decryptExistingFileInPlace({
      file,
      targetNodeId,
      server,
      onDecryptProgress: (bytesDecrypted, bytesTotal) => {
        task.update({
          status: "running",
          bytesTotal,
          bytesCompleted: bytesDecrypted,
        });
      },
      onUploadProgress: (bytesUploaded, uploadTotal) => {
        const bytesTotal = file.sizeBytes + uploadTotal;
        task.update({
          status: "running",
          bytesTotal,
          bytesCompleted: file.sizeBytes + bytesUploaded,
        });
      },
      onFinalizing: () => {
        task.update({ status: "finalizing" });
      },
    });
    task.complete();
  } catch (error) {
    task.fail({
      message: error instanceof Error ? error.message : undefined,
      key:
        error instanceof NoKeyError
          ? "encryptionVaultLocked"
          : error instanceof ClientEncryptionSizeLimitError
            ? "clientEncryptionFileTooLarge"
            : "decryptionFailed",
      params:
        error instanceof ClientEncryptionSizeLimitError
          ? { maxSize: formatBytes(error.maxBytes) }
          : undefined,
    });
    throw error;
  }
}
