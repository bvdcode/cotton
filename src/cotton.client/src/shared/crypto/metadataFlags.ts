import { ENCRYPTED_FLAG_KEY, ORIGINAL_CONTENT_TYPE_KEY } from "./fileCipher";

export const FOLDER_ENCRYPTION_POLICY_KEY = "isClientEncryptionEnabled";

export function isFileEncrypted(
  metadata: Record<string, string> | undefined | null,
): boolean {
  return metadata?.[ENCRYPTED_FLAG_KEY] === "true";
}

export function isFolderEncryptionPolicyEnabled(
  metadata: Record<string, string> | undefined | null,
): boolean {
  return metadata?.[FOLDER_ENCRYPTION_POLICY_KEY] === "true";
}

export function getOriginalContentType(
  metadata: Record<string, string> | undefined | null,
): string | undefined {
  return metadata?.[ORIGINAL_CONTENT_TYPE_KEY];
}
