import { useEffect, useCallback, useRef } from "react";
import { useNodesStore } from "../../../shared/store/nodesStore";
import { useAuthStore } from "../../../shared/store/authStore";

interface UseFilesDataParams {
  nodeId: string | null;
  loadNode: (
    nodeId: string,
    options?: { loadChildren?: boolean; force?: boolean },
  ) => Promise<void>;
  refreshNodeContent: (nodeId: string) => Promise<void>;
}

/**
 * Keeps the active folder content loaded and exposes helpers that operate on
 * the current node. List mode uses the same loaded content as tile mode.
 */

export const useFilesData = ({
  nodeId,
  loadNode,
  refreshNodeContent,
}: UseFilesDataParams) => {
  const loadedNodeIdRef = useRef<string | null>(null);

  const currentUserId = useAuthStore((s) => s.user?.id ?? null);
  const cacheOwnerUserId = useNodesStore((s) => s.cacheOwnerUserId);
  const rawCachedContent = useNodesStore((s) =>
    nodeId ? s.contentByNodeId[nodeId] : undefined,
  );
  const cachedContent =
    cacheOwnerUserId === currentUserId ? rawCachedContent : undefined;

  const optimisticSetFilePreviewHash = useNodesStore(
    (s) => s.optimisticSetFilePreviewHash,
  );
  const childrenTotalCount = cachedContent
    ? cachedContent.nodes.length + cachedContent.files.length
    : null;

  useEffect(() => {
    if (!nodeId) {
      loadedNodeIdRef.current = null;
      return;
    }

    const hasLoadedNode = loadedNodeIdRef.current === nodeId;
    if (hasLoadedNode && cachedContent) {
      return;
    }

    loadedNodeIdRef.current = nodeId;
    void loadNode(nodeId, { loadChildren: true });
  }, [cachedContent, nodeId, loadNode]);

  const handleFolderChanged = useCallback(() => {
    if (!nodeId) {
      return;
    }
    void refreshNodeContent(nodeId);
  }, [nodeId, refreshNodeContent]);

  const reloadCurrentNode = useCallback(() => {
    if (!nodeId) {
      return;
    }

    void loadNode(nodeId, { loadChildren: true, force: true });
  }, [nodeId, loadNode]);

  const optimisticUpdateCurrentNodeFilePreviewHash = useCallback(
    (nodeFileId: string, previewHashEncryptedHex: string) => {
      if (!nodeId) {
        return;
      }

      optimisticSetFilePreviewHash(
        nodeId,
        nodeFileId,
        previewHashEncryptedHex,
      );
    },
    [nodeId, optimisticSetFilePreviewHash],
  );

  return {
    childrenTotalCount,
    handleFolderChanged,
    reloadCurrentNode,
    optimisticUpdateCurrentNodeFilePreviewHash,
  };
};
