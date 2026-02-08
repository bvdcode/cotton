import { useEffect, useState, useCallback, useRef } from "react";
import { nodesApi, type NodeContentDto } from "../../../shared/api/nodesApi";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import { useNodesStore } from "../../../shared/store/nodesStore";

interface UseFilesDataParams {
  nodeId: string | null;
  layoutType: InterfaceLayoutType;
  loadNode: (nodeId: string, options?: { loadChildren?: boolean }) => Promise<unknown>;
  refreshNodeContent: (nodeId: string) => Promise<void>;
  hugeFolderThreshold: number;
}

export const useFilesData = ({
  nodeId,
  layoutType,
  loadNode,
  refreshNodeContent,
  hugeFolderThreshold,
}: UseFilesDataParams) => {
  const [childrenTotalCount, setChildrenTotalCount] = useState<number | null>(
    null,
  );
  const [listTotalCount, setListTotalCount] = useState(0);
  const [listLoading, setListLoading] = useState(false);
  const [listError, setListError] = useState<string | null>(null);
  const [listContent, setListContent] = useState<NodeContentDto | null>(null);
  const [currentPagination, setCurrentPagination] = useState<{ page: number; pageSize: number } | null>(null);
  const lastNodeIdRef = useRef<string | null>(null);
  const tilesLoadedNodeIdRef = useRef<string | null>(null);

  const DEFAULT_PAGE_SIZE = 100;
  const clampPageSize = (pageSize: number) => Math.max(1, Math.min(100, pageSize));

  // Combined tiles loading: check cache → probe (if cold) → load children.
  // Replaces separate probe + tiles effects to eliminate duplicate requests.
  useEffect(() => {
    if (layoutType !== InterfaceLayoutType.Tiles) {
      tilesLoadedNodeIdRef.current = null;
      return;
    }
    if (!nodeId) {
      setChildrenTotalCount(null);
      return;
    }
    if (tilesLoadedNodeIdRef.current === nodeId) return;

    let cancelled = false;

    const loadTiles = async () => {
      // Check persisted cache — avoids network requests for visited folders
      const cached = useNodesStore.getState().contentByNodeId[nodeId];
      if (cached) {
        const count = cached.nodes.length + cached.files.length;
        if (!cancelled) {
          setChildrenTotalCount(count);
          tilesLoadedNodeIdRef.current = nodeId;
        }
        // SWR: show cached data, background refresh inside loadNode
        void loadNode(nodeId, { loadChildren: true });
        return;
      }

      // Cold cache: lightweight probe first to guard against huge folders
      try {
        const probe = await nodesApi.getChildren(nodeId, {
          page: 1,
          pageSize: 1,
        });
        if (cancelled) return;

        setChildrenTotalCount(probe.totalCount);

        if (probe.totalCount > hugeFolderThreshold) {
          // Will switch to list mode via isHugeFolder effect in FilesPage
          return;
        }

        tilesLoadedNodeIdRef.current = nodeId;
        void loadNode(nodeId, { loadChildren: true });
      } catch {
        if (cancelled) return;
        // Probe failed — try loading anyway
        tilesLoadedNodeIdRef.current = nodeId;
        void loadNode(nodeId, { loadChildren: true });
      }
    };

    void loadTiles();

    return () => {
      cancelled = true;
    };
  }, [nodeId, layoutType, loadNode, hugeFolderThreshold]);

  useEffect(() => {
    if (layoutType !== InterfaceLayoutType.List) {
      setListTotalCount(0);
      setListError(null);
      setListContent(null);
      setCurrentPagination(null);
      lastNodeIdRef.current = null;
      return;
    }

    // Ensure list view loads immediately after switching.
    // DataGrid does not necessarily trigger onPaginationModelChange on mount.
    if (nodeId && !currentPagination) {
      setCurrentPagination({ page: 0, pageSize: DEFAULT_PAGE_SIZE });
      lastNodeIdRef.current = nodeId;
      return;
    }

    if (nodeId && lastNodeIdRef.current && lastNodeIdRef.current !== nodeId) {
      setCurrentPagination((prev) => (prev ? { ...prev, page: 0 } : prev));
    }
    lastNodeIdRef.current = nodeId ?? null;
  }, [nodeId, layoutType, currentPagination]);

  const fetchListPage = useCallback(async (page: number, pageSize: number) => {
    if (!nodeId) {
      return;
    }

    setListLoading(true);
    try {
      const response = await nodesApi.getChildren(nodeId, {
        page: page + 1,
        pageSize: clampPageSize(pageSize),
      });
      setListContent(response.content);
      setListTotalCount(response.totalCount);
    } catch (err) {
      console.error("Failed to load paged content", err);
      setListError("Failed to load list");
    } finally {
      setListLoading(false);
    }
  }, [nodeId]);

  useEffect(() => {
    if (layoutType === InterfaceLayoutType.List && nodeId && currentPagination) {
      void fetchListPage(currentPagination.page, currentPagination.pageSize);
    }
  }, [nodeId, layoutType, currentPagination, fetchListPage]);

  const handlePaginationChange = useCallback((page: number, pageSize: number) => {
    setCurrentPagination({ page, pageSize: clampPageSize(pageSize) });
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

    if (layoutType === InterfaceLayoutType.List && currentPagination) {
      void fetchListPage(currentPagination.page, currentPagination.pageSize);
      return;
    }

    void loadNode(nodeId, { loadChildren: true });
  }, [nodeId, loadNode, layoutType, currentPagination, fetchListPage]);

  return {
    childrenTotalCount,
    listTotalCount,
    listLoading,
    listError,
    listContent,
    handlePaginationChange,
    handleFolderChanged,
    reloadCurrentNode,
  };
};
