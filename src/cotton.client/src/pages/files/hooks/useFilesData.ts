import { useEffect, useCallback, useRef } from "react";
import type { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import { useNodesStore } from "../../../shared/store/nodesStore";
import { useAuthStore } from "../../../shared/store/authStore";

interface UseFilesDataParams {
  nodeId: string | null;
  layoutType: InterfaceLayoutType;
  loadNode: (nodeId: string, options?: { loadChildren?: boolean }) => Promise<void>;
  refreshNodeContent: (nodeId: string) => Promise<void>;
}

export const useFilesData = ({
  nodeId,
  loadNode,
  refreshNodeContent,
}: UseFilesDataParams) => {
  const loadedNodeIdRef = useRef<string | null>(null);

  // Derive children count reactively from cached/loaded content
  const currentUserId = useAuthStore((s) => s.user?.id ?? null);
  const cacheOwnerUserId = useNodesStore((s) => s.cacheOwnerUserId);
  const rawCachedContent = useNodesStore(
    (s) => (nodeId ? s.contentByNodeId[nodeId] : undefined),
  );
  const cachedContent =
    cacheOwnerUserId === currentUserId
      ? rawCachedContent
      : undefined;

  const optimisticSetFilePreviewHash = useNodesStore(
    (s) => s.optimisticSetFilePreviewHash,
  );
  const childrenTotalCount = cachedContent
    ? cachedContent.nodes.length + cachedContent.files.length
    : null;

  // Keep node metadata + children in a single shared source for all view modes.
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

  const handlePaginationChange = useCallback(() => {
    // No-op in Files page: list uses the same fully loaded content as tiles.
  }, []);

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

    void loadNode(nodeId, { loadChildren: true });
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
    listTotalCount: childrenTotalCount ?? 0,
    listLoading: false,
    listError: null,
    listContent: cachedContent ?? null,
    handlePaginationChange,
    handleFolderChanged,
    reloadCurrentNode,
    optimisticUpdateCurrentNodeFilePreviewHash,
  };
};
