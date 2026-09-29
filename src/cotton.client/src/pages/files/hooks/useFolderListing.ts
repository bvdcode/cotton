import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import { useCallback, useMemo } from "react";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import {
  nodesApi,
  type FolderStatsDto,
  type NodeContentDto,
  type NodeResponse,
} from "../../../shared/api/nodesApi";
import { queryClient } from "../../../shared/api/queries/queryClient";
import { queryKeys } from "../../../shared/api/queries/queryKeys";
import { refreshNodeContent } from "../../../shared/store/nodesActions";
import { translateError } from "../../../shared/i18n/translateError";
import type { PaginationProps } from "../../../shared/types/FileListViewTypes";
import { useDecryptedFolderContent } from "../../../shared/crypto/useDecryptedFolderContent";
import { useFolderListControls } from "./useFolderListControls";

const LIST_PAGE_SIZE = 100;
const TILE_PAGE_SIZE = 1000;

export const useFolderListing = (
  nodeId: string | null,
  userId: string | null,
  layoutType: InterfaceLayoutType,
  folderPolicyEnabled = false,
) => {
  const enabled = nodeId !== null && userId !== null;
  const isList = layoutType === InterfaceLayoutType.List;
  const pageSize = isList ? LIST_PAGE_SIZE : TILE_PAGE_SIZE;
  const queryNodeId = nodeId ?? "";
  const queryUserId = userId ?? "";

  const fetchOverview = useCallback(
    async (requestedPageSize: number): Promise<NodeResponse> => {
      if (!nodeId) {
        throw new Error("Folder id is required");
      }
      const response = await nodesApi.getChildren(nodeId, {
        page: 1,
        pageSize: requestedPageSize,
        includeStats: true,
      });
      if (!response.content.stats) {
        throw new Error("Folder statistics are missing");
      }
      return response;
    },
    [nodeId],
  );

  const overview = useQuery({
    queryKey: queryKeys.nodeChildren.overview(
      queryNodeId,
      queryUserId,
      pageSize,
    ),
    queryFn: () => fetchOverview(pageSize),
    enabled,
  });

  const namesEnabled =
    !folderPolicyEnabled && overview.data?.content.stats?.encryptedFiles === 0;
  const controls = useFolderListControls(nodeId, namesEnabled);
  const { page, listing } = controls;
  const defaultListing = Object.keys(listing).length === 0;

  const listPage = useQuery({
    placeholderData: (previous, query) =>
      query?.queryKey[1] === queryNodeId && query.queryKey[2] === queryUserId
        ? previous
        : undefined,
    queryKey: queryKeys.nodeChildren.page(
      queryNodeId,
      queryUserId,
      page,
      listing,
    ),
    queryFn: () =>
      nodesApi.getChildren(queryNodeId, {
        page: page + 1,
        pageSize: LIST_PAGE_SIZE,
        ...(defaultListing ? {} : { listing }),
      }),
    enabled: enabled && isList && (page > 0 || !defaultListing),
  });

  const tiles = useInfiniteQuery({
    queryKey: queryKeys.nodeChildren.tiles(queryNodeId, queryUserId),
    queryFn: ({ pageParam }) =>
      pageParam === 1
        ? queryClient.fetchQuery({
            queryKey: queryKeys.nodeChildren.overview(
              queryNodeId,
              queryUserId,
              TILE_PAGE_SIZE,
            ),
            queryFn: () => fetchOverview(TILE_PAGE_SIZE),
          })
        : nodesApi.getChildren(queryNodeId, {
            page: pageParam,
            pageSize: TILE_PAGE_SIZE,
          }),
    initialPageParam: 1,
    getNextPageParam: (last, pages) => {
      const loaded = pages.reduce(
        (count, item) =>
          count + item.content.nodes.length + item.content.files.length,
        0,
      );
      if (
        loaded >= pages[0].totalCount ||
        last.content.nodes.length + last.content.files.length === 0
      ) {
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

  const activePage = page === 0 && defaultListing ? overview : listPage;
  const content = useDecryptedFolderContent(
    isList ? activePage.data?.content : tileContent,
  );
  const stats: FolderStatsDto = overview.data?.content.stats ?? {
    folders: 0,
    files: 0,
    encryptedFiles: 0,
    sizeBytes: 0,
  };
  const error =
    overview.isError || (isList ? activePage.isError : tiles.isError)
      ? translateError("files", "errors.loadContentsFailed")
      : null;

  const refresh = useCallback(() => {
    if (nodeId) {
      void refreshNodeContent(nodeId);
    }
  }, [nodeId]);

  const paginationProps: PaginationProps | undefined = isList
    ? {
        query: controls.query,
        model: { page, pageSize: LIST_PAGE_SIZE },
        totalCount:
          activePage.data?.totalCount ?? overview.data?.totalCount ?? 0,
        loading: activePage.isFetching,
        onPaginationModelChange: (model) => controls.setPage(model.page),
      }
    : undefined;

  const loadMore = useCallback(() => {
    if (tiles.hasNextPage && !tiles.isFetchingNextPage) {
      void tiles.fetchNextPage();
    }
  }, [tiles]);

  return {
    content,
    stats,
    totalCount: overview.data?.totalCount ?? null,
    loading:
      overview.isPending || (isList ? activePage.isPending : tiles.isPending),
    error,
    pagination: paginationProps,
    loadMore: !isList && tiles.hasNextPage ? loadMore : undefined,
    refresh,
  };
};
