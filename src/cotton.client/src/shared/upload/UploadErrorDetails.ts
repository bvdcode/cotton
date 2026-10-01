import { ClientEncryptionSizeLimitError, NoKeyError } from "../crypto";
import { formatBytes } from "../utils/formatBytes";

export function getEncryptionErrorKey(error: Error | null): string {
  if (error instanceof NoKeyError) {
    return "encryptionVaultLocked";
  }

  if (error instanceof ClientEncryptionSizeLimitError) {
    return "clientEncryptionFileTooLarge";
  }

  return "encryptionFailed";
}

export function getUploadErrorKey(error: Error | null): string {
  if (error instanceof NoKeyError) {
    return "encryptionVaultLocked";
  }

  if (error instanceof ClientEncryptionSizeLimitError) {
    return "clientEncryptionFileTooLarge";
  }

  return "uploadFailed";
}

export function getUploadErrorParams(
  error: Error | null,
): Record<string, string | number> | undefined {
  return error instanceof ClientEncryptionSizeLimitError
    ? { maxSize: formatBytes(error.maxBytes) }
    : undefined;
}
