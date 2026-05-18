import {
  ClientEncryptionSizeLimitError,
  type ClientEncryptionBlobOperation,
} from "./errors";

// Temporary guard for the current browser Blob-based CSE pipeline.
// Raise or remove this when upload/download switch to streaming encryption.
export const CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES = 512 * 1024 * 1024;

export function assertClientEncryptionBlobPipelineSize(
  sizeBytes: number,
  operation: ClientEncryptionBlobOperation,
): void {
  if (sizeBytes > CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES) {
    throw new ClientEncryptionSizeLimitError(
      operation,
      sizeBytes,
      CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES,
    );
  }
}
