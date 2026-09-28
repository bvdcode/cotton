import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import { useCallback, useMemo, useState } from "react";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import { nodesApi, type FolderStatsDto, type NodeContentDto, type NodeResponse } from "../../../shared/api/nodesApi";
import { queryClient } from "../../../shared/api/queries/queryClient";
import { queryKeys } from "../../../shared/api/queries/queryKeys";
import { refreshNodeContent } from "../../../shared/store/nodesActions";
import { translateError } from "../../../shared/i18n/translateError";
import type { PaginationProps } from "../../../shared/types/FileListViewTypes";

const PAGE_SIZE = 100;

type PaginationState = { nodeId: string | null; page: number };

export const useFolderListing = (
  nodeId: string | null,
  userId: string | null,
  layoutType: InterfaceLayoutType,
) => {
  const [pagination, setPagination] = useState<PaginationState>({ nodeId: null, page: 0 });
  const page = pagination.nodeId === nodeId ? pagination.page : 0;
  const enabled = nodeId !== null && userId !== null;
  const isList = layoutType === InterfaceLayoutType.List;
  const queryNodeId = nodeId ?? "";
  const queryUserId = userId ?? "";

  const fetchOverview = useCallback(async (): Promise<NodeResponse> => {
    if (!nodeId) {
      throw new Error("Folder id is required");
    }
    const response = await nodesApi.getChildren(nodeId, {
      page: 1,
      pageSize: PAGE_SIZE,
      includeStats: true,
    });
    if (!response.content.stats) {
      throw new Error("Folder statistics are missing");
    }
    return response;
  }, [nodeId]);

  const overview = useQuery({
    queryKey: queryKeys.nodeChildren.overview(queryNodeId, queryUserId),
    queryFn: fetchOverview,
    enabled,
  });

  const listPage = useQuery({
    queryKey: queryKeys.nodeChildren.page(queryNodeId, queryUserId, page),
    queryFn: () => nodesApi.getChildren(queryNodeId, { page: page + 1, pageSize: PAGE_SIZE }),
    enabled: enabled && isList && page > 0,
  });

  const tiles = useInfiniteQuery({
    queryKey: queryKeys.nodeChildren.tiles(queryNodeId, queryUserId),
    queryFn: ({ pageParam }) => pageParam === 1
      ? queryClient.fetchQuery({
          queryKey: queryKeys.nodeChildren.overview(queryNodeId, queryUserId),
          queryFn: fetchOverview,
        })
      : nodesApi.getChildren(queryNodeId, { page: pageParam, pageSize: PAGE_SIZE }),
    initialPageParam: 1,
    getNextPageParam: (last, pages) => {
      const loaded = pages.reduce(
        (count, item) => count + item.content.nodes.length + item.content.files.length,
        0,
      );
      if (loaded >= pages[0].totalCount || last.content.nodes.length + last.content.files.length === 0) {
        return undefined;
      }
      return pages.length + 1;
    },
    enabled: enabled && !isList,
  });

  const tileContent = useMemo<NodeContentDto | undefined>(() => {
    const pages = tiles.data?.pages;
    if (!pages || pages.length === 0) {
      return undefined;
    }
    return {
      ...pages[0].content,
      nodes: pages.flatMap((item) => item.content.nodes),
      files: pages.flatMap((item) => item.content.files),
    };
  }, [tiles.data]);

  const activePage = page === 0 ? overview : listPage;
  const content = isList ? activePage.data?.content : tileContent;
  const stats: FolderStatsDto = overview.data?.content.stats ?? { folders: 0, files: 0, encryptedFiles: 0, sizeBytes: 0 };
  const error = overview.isError || (isList ? activePage.isError : tiles.isError)
    ? translateError("files", "errors.loadContentsFailed")
    : null;

  const refresh = useCallback(() => {
    if (nodeId) {
      void refreshNodeContent(nodeId);
    }
  }, [nodeId]);

  const paginationProps: PaginationProps | undefined = isList ? {
    model: { page, pageSize: PAGE_SIZE },
    totalCount: overview.data?.totalCount ?? 0,
    loading: activePage.isFetching,
    onPaginationModelChange: (model) => setPagination({ nodeId, page: model.page }),
  } : undefined;

  const loadMore = useCallback(() => {
    if (tiles.hasNextPage && !tiles.isFetchingNextPage) {
      void tiles.fetchNextPage();
    }
  }, [tiles]);

  return {
    content,
    stats,
    totalCount: overview.data?.totalCount ?? null,
    loading: overview.isPending || (isList ? activePage.isPending : tiles.isPending),
    error,
    pagination: paginationProps,
    loadMore: !isList && tiles.hasNextPage ? loadMore : undefined,
    refresh,
  };
};
