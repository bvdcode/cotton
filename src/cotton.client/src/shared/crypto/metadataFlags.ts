import { ENCRYPTED_FLAG_KEY, ORIGINAL_CONTENT_TYPE_KEY } from "./fileCipher";

export const FOLDER_ENCRYPTION_POLICY_KEY = "isClientEncryptionEnabled";

export interface FolderEncryptionPolicyNode {
  id: string;
  parentId?: string | null;
  metadata?: Record<string, string> | null;
}

export interface FolderEncryptionPolicyState {
  explicitEnabled: boolean;
  inheritedEnabled: boolean;
  effectiveEnabled: boolean;
}

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

export function getFolderEncryptionPolicyState(
  node: FolderEncryptionPolicyNode | undefined | null,
  ancestors: readonly FolderEncryptionPolicyNode[] = [],
): FolderEncryptionPolicyState {
  const explicitEnabled = isFolderEncryptionPolicyEnabled(node?.metadata);
  const inheritedEnabled = ancestors.some((ancestor) =>
    isFolderEncryptionPolicyEnabled(ancestor.metadata),
  );

  return {
    explicitEnabled,
    inheritedEnabled,
    effectiveEnabled: explicitEnabled || inheritedEnabled,
  };
}

export function getFolderEncryptionPolicyStateFromParentResolver(
  node: FolderEncryptionPolicyNode | undefined | null,
  resolveNode: (
    nodeId: string,
  ) => FolderEncryptionPolicyNode | undefined | null,
): FolderEncryptionPolicyState {
  const explicitEnabled = isFolderEncryptionPolicyEnabled(node?.metadata);
  let inheritedEnabled = false;
  let parentId = node?.parentId ?? null;
  const visited = new Set<string>();

  while (parentId && !visited.has(parentId)) {
    visited.add(parentId);
    const parent = resolveNode(parentId);
    if (!parent) break;

    if (isFolderEncryptionPolicyEnabled(parent.metadata)) {
      inheritedEnabled = true;
      break;
    }

    parentId = parent.parentId ?? null;
  }

  return {
    explicitEnabled,
    inheritedEnabled,
    effectiveEnabled: explicitEnabled || inheritedEnabled,
  };
}

export function getOriginalContentType(
  metadata: Record<string, string> | undefined | null,
): string | undefined {
  return metadata?.[ORIGINAL_CONTENT_TYPE_KEY];
}
