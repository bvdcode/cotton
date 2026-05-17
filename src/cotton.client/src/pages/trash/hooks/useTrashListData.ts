import { useCallback, useMemo, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  invalidateTrashChildren,
  useTrashChildrenQuery,
} from "../../../shared/api/queries/trash";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";

type UseTrashListDataParams = {
  nodeId: string | null;
  routeNodeId?: string;
  layoutType: InterfaceLayoutType;
  loadErrorText: string;
};

const DEFAULT_PAGE_SIZE = 100;

export const useTrashListData = ({
  nodeId,
  routeNodeId,
  layoutType,
  loadErrorText,
}: UseTrashListDataParams) => {
  const isRoot = !routeNodeId;
  const isListLayout = layoutType === InterfaceLayoutType.List;
  const [pagination, setPagination] = useState<{
    nodeId: string | null;
    page: number;
    pageSize: number;
  }>({ nodeId, page: 0, pageSize: DEFAULT_PAGE_SIZE });

  const effectivePagination = useMemo(
    () =>
      pagination.nodeId === nodeId
        ? pagination
        : { nodeId, page: 0, pageSize: pagination.pageSize },
    [nodeId, pagination],
  );

  const queryClient = useQueryClient();
  const query = useTrashChildrenQuery({
    nodeId,
    isRoot,
    page: effectivePagination.page + 1,
    pageSize: effectivePagination.pageSize,
    enabled: isListLayout && !!nodeId,
  });

  const handlePaginationChange = useCallback((page: number, pageSize: number) => {
    setPagination({ nodeId, page, pageSize });
  }, [nodeId]);

  const reloadListPage = useCallback(() => {
    if (!isListLayout || !nodeId) {
      return;
    }

    void invalidateTrashChildren(queryClient, nodeId);
  }, [isListLayout, nodeId, queryClient]);

  return {
    listTotalCount: query.data?.totalCount ?? 0,
    listLoading: query.isFetching,
    listError: query.isError ? loadErrorText : null,
    listContent: query.data?.content ?? null,
    handlePaginationChange,
    reloadListPage,
  };
};
