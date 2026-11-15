import {
  createNamedFolder,
  getChildEncryptionPolicy,
  prepareFolderName,
} from "../crypto/folderNameOperations";
import { hasEncryptedFolderName } from "../crypto/folderDisplayMeta";
import { lookupDisplayNames } from "../crypto/lookupDisplayNames";
import { useVault } from "../crypto/vault";
import { getFileNameKey } from "../utils/fileNameUtils";
import { reportClientError } from "@shared/utils/clientDiagnostics";
import { isAxiosError } from "../api/httpClient";
import { nodesApi } from "../api/nodesApi";
import { queryClient } from "../api/queries/queryClient";
import { findCachedNode } from "../api/queries/nodeNavigation";
import { removeCachedFolder } from "../api/queries/nodeCache";
import type { NodeDto } from "../api/layoutsApi";
import { getFolderEncryptionPolicyStateFromParentResolver } from "../crypto";
import { translateError } from "../i18n/translateError";
import { useNodesStore } from "./nodesStore";
import { refreshNodeContent } from "./nodesContent";

const tFileError = (key: string): string => translateError("files", key);

function beginFolderMutation(owner: string) {
  const version = useNodesStore.getState().navigationVersion;
  const isOwner = () => useNodesStore.getState().cacheOwnerUserId === owner;
  const finish = (error: string | null = null) => {
    if (isOwner() && useNodesStore.getState().navigationVersion === version) {
      useNodesStore.setState({ loading: false, error });
    }
  };
  useNodesStore.setState({ loading: true, error: null });
  return { isOwner, finish };
}

async function resolveParentPolicy(
  parentId: string,
  owner: string,
): Promise<boolean> {
  const parent = findCachedNode(queryClient, owner, parentId);
  if (!parent) {
    return getChildEncryptionPolicy(parentId);
  }
  let complete = true;
  const policy = getFolderEncryptionPolicyStateFromParentResolver(
    parent,
    (id) => {
      const ancestor = findCachedNode(queryClient, owner, id) ?? null;
      complete &&= ancestor !== null;
      return ancestor;
    },
  );
  if (policy.effectiveEnabled || complete) {
    return policy.effectiveEnabled;
  }
  return getChildEncryptionPolicy(parentId);
}

async function hasFolderRenameConflict(
  node: NodeDto,
  name: string,
  encrypted: boolean,
): Promise<boolean> {
  if (!node.parentId || (!encrypted && !useVault.getState().isUnlocked)) {
    return false;
  }
  const lookup = await lookupDisplayNames(node.parentId, [name], true);
  const nameKey = getFileNameKey(name);
  return [...lookup.nodes, ...lookup.files].some(
    (item) => item.id !== node.id && getFileNameKey(item.name) === nameKey,
  );
}

export const createFolder = async (
  parentNodeId: string,
  name: string,
): Promise<NodeDto | null> => {
  const trimmed = name.trim();
  if (trimmed.length === 0) {
    return null;
  }
  if (useNodesStore.getState().loading) {
    return null;
  }

  const owner = useNodesStore.getState().cacheOwnerUserId;
  if (!owner) {
    return null;
  }
  const { isOwner, finish } = beginFolderMutation(owner);

  try {
    const parentPolicyEnabled = await resolveParentPolicy(parentNodeId, owner);
    if (parentPolicyEnabled || useVault.getState().isUnlocked) {
      const lookup = await lookupDisplayNames(parentNodeId, [trimmed], true);
      if (lookup.takenNameKeys.includes(getFileNameKey(trimmed))) {
        finish(tFileError("errors.duplicateFolderName"));
        return null;
      }
    }
    if (!isOwner()) {
      return null;
    }
    const folder = await createNamedFolder(
      parentNodeId,
      trimmed,
      parentPolicyEnabled,
    );

    if (!isOwner()) {
      return null;
    }
    useNodesStore.getState().addFolderToCache(parentNodeId, folder);
    finish();

    void refreshNodeContent(parentNodeId);
    return folder;
  } catch (error) {
    reportClientError("Failed to create folder", error);
    finish(
      tFileError(
        isAxiosError(error) && error.response?.status === 409
          ? "errors.duplicateFolderName"
          : "errors.createFolderFailed",
      ),
    );
    return null;
  }
};

export const deleteFolder = async (
  nodeId: string,
  parentNodeId?: string,
  skipTrash: boolean = false,
): Promise<boolean> => {
  if (useNodesStore.getState().loading) {
    return false;
  }

  const owner = useNodesStore.getState().cacheOwnerUserId;
  if (!owner) {
    return false;
  }
  const { isOwner, finish } = beginFolderMutation(owner);

  try {
    await nodesApi.deleteNode(nodeId, skipTrash);

    if (!isOwner()) {
      return false;
    }
    removeCachedFolder(queryClient, owner, nodeId);
    finish();
    if (parentNodeId) {
      void refreshNodeContent(parentNodeId);
    }

    return true;
  } catch (error) {
    reportClientError("Failed to delete folder", error);
    finish(tFileError("errors.deleteFolderFailed"));
    return false;
  }
};

export const renameFolder = async (
  nodeId: string,
  newName: string,
  parentNodeId?: string,
): Promise<boolean> => {
  const trimmed = newName.trim();
  if (trimmed.length === 0) {
    return false;
  }
  if (useNodesStore.getState().loading) {
    return false;
  }

  const owner = useNodesStore.getState().cacheOwnerUserId;
  if (!owner) {
    return false;
  }
  const { isOwner, finish } = beginFolderMutation(owner);

  try {
    const node =
      findCachedNode(queryClient, owner, nodeId) ??
      (await nodesApi.getNode(nodeId));
    const encrypted =
      hasEncryptedFolderName(node.metadata) ||
      Boolean(
        node.parentId && (await resolveParentPolicy(node.parentId, owner)),
      );
    if (await hasFolderRenameConflict(node, trimmed, encrypted)) {
      finish(tFileError("errors.duplicateFolderName"));
      return false;
    }
    const preparedName = await prepareFolderName(node, trimmed, encrypted);
    if (!isOwner()) {
      return false;
    }
    const updated = await nodesApi.renameNode(nodeId, preparedName);

    if (!isOwner()) {
      return false;
    }
    useNodesStore.getState().updateNode(updated);
    finish();
    if (parentNodeId) {
      void refreshNodeContent(parentNodeId);
    }

    return true;
  } catch (error) {
    reportClientError("Failed to rename folder", error);
    finish(
      tFileError(
        isAxiosError(error) && error.response?.status === 409
          ? "errors.duplicateFolderName"
          : "errors.renameFolderFailed",
      ),
    );
    return false;
  }
};
