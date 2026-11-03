import { useInfiniteQuery } from "@tanstack/react-query";
import { useCallback, useMemo } from "react";
import {
  nodesApi,
  type NodeContentDto,
  type NodeResponse,
} from "@shared/api/nodesApi";
import { queryClient } from "@shared/api/queries/queryClient";
import { queryKeys } from "@shared/api/queries/queryKeys";
import { FOLDER_PAGE_SIZE } from "@shared/api/queries/folderPageSize";

export const TILE_PAGE_SIZE = FOLDER_PAGE_SIZE;

export function useFolderTiles(
  queryNodeId: string,
  queryUserId: string,
  enabled: boolean,
  fetchOverview: (pageSize: number) => Promise<NodeResponse>,
) {
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
    enabled,
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

  const loadMore = useCallback(() => {
    if (tiles.hasNextPage && !tiles.isFetchingNextPage) {
      void tiles.fetchNextPage();
    }
  }, [tiles]);

  return {
    content: tileContent,
    loadMore,
    hasNextPage: tiles.hasNextPage,
    isError: tiles.isError,
    isPending: tiles.isPending,
  };
}
