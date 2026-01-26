import { useEffect, useState, useCallback } from "react";
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
  const [listPage, setListPage] = useState(0);
  const [listPageSize, setListPageSize] = useState<number | null>(null);
  const [listTotalCount, setListTotalCount] = useState(0);
  const [listLoading, setListLoading] = useState(false);
  const [listError, setListError] = useState<string | null>(null);
  const [listContent, setListContent] = useState<NodeContentDto | null>(null);

  useEffect(() => {
    setListPage(0);
  }, [nodeId, layoutType]);

  useEffect(() => {
    if (layoutType !== InterfaceLayoutType.List) return;
    setListTotalCount(0);
    setListError(null);
  }, [nodeId, layoutType]);

  const fetchListPage = useCallback(async () => {
    if (!nodeId || listPageSize === null) {
      return;
    }

    setListLoading(true);
    try {
      const response = await nodesApi.getChildren(nodeId, {
        page: listPage + 1,
        pageSize: listPageSize,
      });
      setListContent(response.content);
      setListTotalCount(response.totalCount);
    } catch (err) {
      console.error("Failed to load paged content", err);
    } finally {
      setListLoading(false);
    }
  }, [nodeId, listPage, listPageSize]);

  useEffect(() => {
    if (layoutType !== InterfaceLayoutType.List) {
      return;
    }
    if (!nodeId || listPageSize === null) {
      return;
    }
    void fetchListPage();
  }, [layoutType, nodeId, listPageSize, fetchListPage]);

  const handleFolderChanged = useCallback(() => {
    if (!nodeId) {
      return;
    }
    if (layoutType === InterfaceLayoutType.List) {
      void fetchListPage();
      return;
    }
    void refreshNodeContent(nodeId);
  }, [nodeId, layoutType, fetchListPage, refreshNodeContent]);

  const reloadCurrentNode = useCallback(() => {
    if (!nodeId) {
      return;
    }
    if (layoutType === InterfaceLayoutType.List) {
      void fetchListPage();
    } else {
      void loadNode(nodeId);
    }
  }, [nodeId, layoutType, fetchListPage, loadNode]);

  return {
    listPage,
    listPageSize,
    listTotalCount,
    listLoading,
    listError,
    listContent,
    setListPage,
    setListPageSize,
    handleFolderChanged,
    reloadCurrentNode,
  };
};
