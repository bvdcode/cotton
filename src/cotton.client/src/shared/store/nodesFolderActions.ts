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
import { nodesApi, type NodeContentDto } from "../api/nodesApi";
import type { NodeDto } from "../api/layoutsApi";
import { getFolderEncryptionPolicyStateFromParentResolver } from "../crypto";
import { translateError } from "../i18n/translateError";
import { useNodesStore } from "./nodesStore";
import { refreshNodeContent } from "./nodesContent";

const tFileError = (key: string): string => translateError("files", key);

type NodeLookupSnapshot = {
  currentNode: NodeDto | null;
  ancestors: NodeDto[];
  contentByNodeId: Record<string, NodeContentDto | undefined>;
};

function findCachedNodeById(
  state: NodeLookupSnapshot,
  nodeId: string,
): NodeDto | null {
  if (state.currentNode?.id === nodeId) {
    return state.currentNode;
  }

  const ancestor = state.ancestors.find((node) => node.id === nodeId);
  if (ancestor) {
    return ancestor;
  }

  for (const content of Object.values(state.contentByNodeId)) {
    const found = content?.nodes.find((node) => node.id === nodeId);
    if (found) {
      return found;
    }
  }

  return null;
}

async function resolveParentPolicy(parentId: string): Promise<boolean> {
  const state = useNodesStore.getState();
  const parent = findCachedNodeById(state, parentId);
  if (!parent) {
    return getChildEncryptionPolicy(parentId);
  }
  let complete = true;
  const policy = getFolderEncryptionPolicyStateFromParentResolver(
    parent,
    (id) => {
      const ancestor = findCachedNodeById(state, id);
      complete &&= ancestor !== null;
      return ancestor;
    },
  );
  if (policy.effectiveEnabled || complete) {
    return policy.effectiveEnabled;
  }
  return getChildEncryptionPolicy(parentId);
}

export const createFolder = async (
  parentNodeId: string,
  name: string,
): Promise<NodeDto | null> => {
  const trimmed = name.trim();
  if (trimmed.length === 0) return null;
  if (useNodesStore.getState().loading) return null;

  useNodesStore.setState({ loading: true, error: null });

  try {
    const parentPolicyEnabled = await resolveParentPolicy(parentNodeId);
    if (parentPolicyEnabled || useVault.getState().isUnlocked) {
      const lookup = await lookupDisplayNames(parentNodeId, [trimmed], true);
      if (lookup.takenNameKeys.includes(getFileNameKey(trimmed))) {
        useNodesStore.setState({
          loading: false,
          error: tFileError("errors.duplicateFolderName"),
        });
        return null;
      }
    }
    const folder = await createNamedFolder(
      parentNodeId,
      trimmed,
      parentPolicyEnabled,
    );

    useNodesStore.setState((prev) => {
      const existing = prev.contentByNodeId[parentNodeId];
      if (!existing) return { loading: false };

      return {
        contentByNodeId: {
          ...prev.contentByNodeId,
          [parentNodeId]: {
            ...existing,
            nodes: [...existing.nodes, folder],
          },
        },
        loading: false,
      };
    });

    void refreshNodeContent(parentNodeId);
    return folder;
  } catch (error) {
    reportClientError("Failed to create folder", error);
    useNodesStore.setState({
      loading: false,
      error: tFileError(
        isAxiosError(error) && error.response?.status === 409
          ? "errors.duplicateFolderName"
          : "errors.createFolderFailed",
      ),
    });
    return null;
  }
};

export const deleteFolder = async (
  nodeId: string,
  parentNodeId?: string,
  skipTrash: boolean = false,
): Promise<boolean> => {
  if (useNodesStore.getState().loading) return false;

  useNodesStore.setState({ loading: true, error: null });

  try {
    await nodesApi.deleteNode(nodeId, skipTrash);

    if (parentNodeId) {
      useNodesStore.setState((prev) => {
        const existing = prev.contentByNodeId[parentNodeId];
        if (!existing) return { loading: false };

        return {
          contentByNodeId: {
            ...prev.contentByNodeId,
            [parentNodeId]: {
              ...existing,
              nodes: existing.nodes.filter((n) => n.id !== nodeId),
            },
          },
          loading: false,
        };
      });

      void refreshNodeContent(parentNodeId);
    } else {
      useNodesStore.setState({ loading: false });
    }

    return true;
  } catch (error) {
    reportClientError("Failed to delete folder", error);
    useNodesStore.setState({
      loading: false,
      error: tFileError("errors.deleteFolderFailed"),
    });
    return false;
  }
};

export const renameFolder = async (
  nodeId: string,
  newName: string,
  parentNodeId?: string,
): Promise<boolean> => {
  const trimmed = newName.trim();
  if (trimmed.length === 0) return false;
  if (useNodesStore.getState().loading) return false;

  useNodesStore.setState({ loading: true, error: null });

  try {
    const node =
      findCachedNodeById(useNodesStore.getState(), nodeId) ??
      (await nodesApi.getNode(nodeId));
    const encrypted =
      hasEncryptedFolderName(node.metadata) ||
      Boolean(node.parentId && (await resolveParentPolicy(node.parentId)));
    if (node.parentId && (encrypted || useVault.getState().isUnlocked)) {
      const lookup = await lookupDisplayNames(node.parentId, [trimmed], true);
      const conflict = [...lookup.nodes, ...lookup.files].some(
        (item) =>
          item.id !== nodeId &&
          getFileNameKey(item.name) === getFileNameKey(trimmed),
      );
      if (conflict) {
        useNodesStore.setState({
          loading: false,
          error: tFileError("errors.duplicateFolderName"),
        });
        return false;
      }
    }
    const updated = await nodesApi.renameNode(
      nodeId,
      await prepareFolderName(node, trimmed, encrypted),
    );

    if (parentNodeId) {
      useNodesStore.setState((prev) => {
        const existing = prev.contentByNodeId[parentNodeId];
        if (!existing) return { loading: false };

        return {
          contentByNodeId: {
            ...prev.contentByNodeId,
            [parentNodeId]: {
              ...existing,
              nodes: existing.nodes.map((n) => (n.id === nodeId ? updated : n)),
            },
          },
          loading: false,
        };
      });

      void refreshNodeContent(parentNodeId);
    } else {
      useNodesStore.setState({ loading: false });
    }

    return true;
  } catch (error) {
    reportClientError("Failed to rename folder", error);
    useNodesStore.setState({
      loading: false,
      error: tFileError(
        isAxiosError(error) && error.response?.status === 409
          ? "errors.duplicateFolderName"
          : "errors.renameFolderFailed",
      ),
    });
    return false;
  }
};
