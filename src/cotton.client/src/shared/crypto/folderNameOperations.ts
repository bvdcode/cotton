import type { NodeDto } from "../api/layoutsApi";
import { nodesApi, type RenameNodeRequest } from "../api/nodesApi";
import { DISPLAY_META_KEY } from "./displayMeta";
import { encryptFolderName, hasEncryptedFolderName } from "./folderDisplayMeta";
import {
  FOLDER_ENCRYPTION_POLICY_KEY,
  getFolderEncryptionPolicyState,
} from "./metadataFlags";
import { requireMasterKey } from "./vault";

export async function getChildEncryptionPolicy(
  parentId: string,
): Promise<boolean> {
  const [parent, ancestors] = await Promise.all([
    nodesApi.getNode(parentId),
    nodesApi.getAncestors(parentId),
  ]);
  return getFolderEncryptionPolicyState(parent, ancestors).effectiveEnabled;
}

export async function prepareFolderName(
  node: NodeDto,
  name: string,
  encrypted: boolean,
): Promise<RenameNodeRequest> {
  if (!encrypted) {
    return { name: name.trim() };
  }
  const key = requireMasterKey();
  const value = await encryptFolderName(name);
  if (requireMasterKey() !== key) {
    throw new Error(
      "The encryption key changed while preparing the folder name.",
    );
  }
  return {
    name: hasEncryptedFolderName(node.metadata)
      ? node.name
      : crypto.randomUUID(),
    metadata: { [DISPLAY_META_KEY]: value },
  };
}

export async function createNamedFolder(
  parentId: string,
  name: string,
  encrypted: boolean,
): Promise<NodeDto> {
  if (!encrypted) {
    return nodesApi.createNode({ parentId, name: name.trim() });
  }
  const key = requireMasterKey();
  const value = await encryptFolderName(name);
  if (requireMasterKey() !== key) {
    throw new Error(
      "The encryption key changed while preparing the folder name.",
    );
  }
  return nodesApi.createNode({
    parentId,
    name: crypto.randomUUID(),
    metadata: {
      [DISPLAY_META_KEY]: value,
      [FOLDER_ENCRYPTION_POLICY_KEY]: "true",
    },
  });
}
