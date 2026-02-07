import { useEffect, useState, useCallback, useRef } from "react";
import { nodesApi, type NodeContentDto } from "../../../shared/api/nodesApi";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";

interface UseFilesDataParams {
  nodeId: string | null;
  layoutType: InterfaceLayoutType;
  loadRoot: (options?: { force?: boolean; loadChildren?: boolean }) => Promise<unknown>;
  loadNode: (nodeId: string, options?: { loadChildren?: boolean }) => Promise<unknown>;
  refreshNodeContent: (nodeId: string) => Promise<void>;
}

export const useFilesData = ({
  nodeId,
  layoutType,
  loadNode,
  refreshNodeContent,
}: UseFilesDataParams) => {
  const [listTotalCount, setListTotalCount] = useState(0);
  const [listLoading, setListLoading] = useState(false);
  const [listError, setListError] = useState<string | null>(null);
  const [listContent, setListContent] = useState<NodeContentDto | null>(null);
  const [currentPagination, setCurrentPagination] = useState<{ page: number; pageSize: number } | null>(null);
  const lastNodeIdRef = useRef<string | null>(null);

  const DEFAULT_PAGE_SIZE = 100;
  const clampPageSize = (pageSize: number) => Math.max(1, Math.min(100, pageSize));

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
    void loadNode(nodeId);
  }, [nodeId, loadNode]);

  return {
    listTotalCount,
    listLoading,
    listError,
    listContent,
    handlePaginationChange,
    handleFolderChanged,
    reloadCurrentNode,
  };
};
