import { useEffect, useCallback, useRef } from "react";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import { useNodesStore } from "../../../shared/store/nodesStore";
import { useAuthStore } from "../../../shared/store/authStore";
import { useFolderListing } from "./useFolderListing";

interface UseFilesDataParams {
  nodeId: string | null;
  layoutType: InterfaceLayoutType;
  loadNode: (
    nodeId: string,
    options?: { loadChildren?: boolean; force?: boolean },
  ) => Promise<void>;
}

export const useFilesData = ({
  nodeId,
  loadNode,
  layoutType,
}: UseFilesDataParams) => {
  const loadedNodeKeyRef = useRef<string | null>(null);

  const currentUserId = useAuthStore((s) => s.user?.id ?? null);
  const cacheOwnerUserId = useNodesStore((s) => s.cacheOwnerUserId);
  const listing = useFolderListing(
    nodeId,
    cacheOwnerUserId === currentUserId ? currentUserId : null,
    layoutType,
  );
  const { refresh } = listing;

  const childrenTotalCount = listing.totalCount;

  useEffect(() => {
    if (!nodeId || !currentUserId) {
      loadedNodeKeyRef.current = null;
      return;
    }

    const key = `${currentUserId}:${nodeId}`;
    if (loadedNodeKeyRef.current === key) {
      return;
    }

    loadedNodeKeyRef.current = key;
    void loadNode(nodeId, { loadChildren: false });
  }, [currentUserId, nodeId, loadNode]);

  const handleFolderChanged = useCallback(() => {
    if (!nodeId) {
      return;
    }
    refresh();
  }, [nodeId, refresh]);

  const reloadCurrentNode = useCallback(() => {
    if (!nodeId) {
      return;
    }

    void loadNode(nodeId, { loadChildren: false, force: true });
    refresh();
  }, [nodeId, loadNode, refresh]);

  return {
    ...listing,
    childrenTotalCount,
    handleFolderChanged,
    reloadCurrentNode,
  };
};
