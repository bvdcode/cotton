import { nodesApi, type NodeContentDto } from "../api/nodesApi";
import type { NodeDto } from "../api/layoutsApi";
import {
  FOLDER_ENCRYPTION_POLICY_KEY,
  getFolderEncryptionPolicyStateFromParentResolver,
} from "../crypto";
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

export const createFolder = async (
  parentNodeId: string,
  name: string,
): Promise<NodeDto | null> => {
  const trimmed = name.trim();
  if (trimmed.length === 0) return null;
  if (useNodesStore.getState().loading) return null;

  const state = useNodesStore.getState();
  const currentContent = state.contentByNodeId[parentNodeId];

  if (currentContent) {
    const normalizedName = trimmed.toLowerCase();
    const duplicate = currentContent.nodes.find(
      (n) => n.name.toLowerCase() === normalizedName,
    );
    if (duplicate) {
      useNodesStore.setState({
        error: tFileError("errors.duplicateFolderName"),
      });
      return null;
    }
  }

  useNodesStore.setState({ loading: true, error: null });

  try {
    const created = await nodesApi.createNode({
      parentId: parentNodeId,
      name: trimmed,
    });
    const stateAfterCreate = useNodesStore.getState();
    const parentNode = findCachedNodeById(stateAfterCreate, parentNodeId);
    const parentPolicyEnabled = parentNode
      ? getFolderEncryptionPolicyStateFromParentResolver(parentNode, (id) =>
          findCachedNodeById(stateAfterCreate, id),
        ).effectiveEnabled
      : false;
    const folder = parentPolicyEnabled
      ? await nodesApi.updateNodeMetadata(created.id, {
          [FOLDER_ENCRYPTION_POLICY_KEY]: "true",
        })
      : created;

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
    console.error("Failed to create folder", error);
    useNodesStore.setState({
      loading: false,
      error: tFileError("errors.createFolderFailed"),
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
    console.error("Failed to delete folder", error);
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

  const state = useNodesStore.getState();
  const currentContent = parentNodeId
    ? state.contentByNodeId[parentNodeId]
    : undefined;

  if (currentContent) {
    const normalizedName = trimmed.toLowerCase();
    const duplicate = currentContent.nodes.find(
      (n) => n.id !== nodeId && n.name.toLowerCase() === normalizedName,
    );
    if (duplicate) {
      useNodesStore.setState({
        error: tFileError("errors.duplicateFolderName"),
      });
      return false;
    }
  }

  useNodesStore.setState({ loading: true, error: null });

  try {
    const updated = await nodesApi.renameNode(nodeId, { name: trimmed });

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
    console.error("Failed to rename folder", error);
    useNodesStore.setState({
      loading: false,
      error: tFileError("errors.renameFolderFailed"),
    });
    return false;
  }
};
