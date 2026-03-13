import { useCallback, useEffect, useRef, useState } from "react";
import { nodesApi, type NodeContentDto } from "../../../shared/api/nodesApi";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";

type UseTrashListDataParams = {
  nodeId: string | null;
  routeNodeId?: string;
  layoutType: InterfaceLayoutType;
  fallbackContent?: NodeContentDto;
  loadErrorText: string;
};

const DEFAULT_PAGE_SIZE = 100;

export const useTrashListData = ({
  nodeId,
  routeNodeId,
  layoutType,
  fallbackContent,
  loadErrorText,
}: UseTrashListDataParams) => {
  const [listTotalCount, setListTotalCount] = useState(0);
  const [listLoading, setListLoading] = useState(false);
  const [listError, setListError] = useState<string | null>(null);
  const [listContent, setListContent] = useState<NodeContentDto | null>(null);
  const [currentPagination, setCurrentPagination] = useState<{
    page: number;
    pageSize: number;
  } | null>(null);

  const listRequestIdRef = useRef(0);

  const fetchListPage = useCallback(
    async (targetNodeId: string, page: number, pageSize: number) => {
      if (!targetNodeId) return;

      const requestId = ++listRequestIdRef.current;
      setListLoading(true);
      setListError(null);

      try {
        const response = await nodesApi.getChildren(targetNodeId, {
          nodeType: "trash",
          page: page + 1,
          pageSize,
          depth: routeNodeId ? 0 : 1,
        });

        if (requestId !== listRequestIdRef.current) {
          return;
        }

        setListContent(response.content);
        setListTotalCount(response.totalCount);
      } catch (error) {
        if (requestId !== listRequestIdRef.current) {
          return;
        }

        console.error("Failed to load paged trash content", error);
        setListError(loadErrorText);
      } finally {
        if (requestId === listRequestIdRef.current) {
          setListLoading(false);
        }
      }
    },
    [loadErrorText, routeNodeId],
  );

  useEffect(() => {
    if (layoutType !== InterfaceLayoutType.List) {
      setListContent(null);
      setListError(null);
      setListLoading(false);
      setListTotalCount(0);
      setCurrentPagination(null);
      return;
    }

    if (fallbackContent) {
      setListContent(fallbackContent);
      setListTotalCount(
        (fallbackContent.nodes?.length ?? 0) +
          (fallbackContent.files?.length ?? 0),
      );
    }

    if (nodeId && !currentPagination) {
      setCurrentPagination({ page: 0, pageSize: DEFAULT_PAGE_SIZE });
    }
  }, [currentPagination, fallbackContent, layoutType, nodeId]);

  useEffect(() => {
    if (
      layoutType !== InterfaceLayoutType.List ||
      !nodeId ||
      !currentPagination
    ) {
      return;
    }

    void fetchListPage(nodeId, currentPagination.page, currentPagination.pageSize);
  }, [currentPagination, fetchListPage, layoutType, nodeId]);

  const handlePaginationChange = useCallback((page: number, pageSize: number) => {
    setCurrentPagination({ page, pageSize });
  }, []);

  const reloadListPage = useCallback(() => {
    if (
      layoutType !== InterfaceLayoutType.List ||
      !nodeId ||
      !currentPagination
    ) {
      return;
    }

    void fetchListPage(nodeId, currentPagination.page, currentPagination.pageSize);
  }, [currentPagination, fetchListPage, layoutType, nodeId]);

  return {
    listTotalCount,
    listLoading,
    listError,
    listContent,
    handlePaginationChange,
    reloadListPage,
  };
};
