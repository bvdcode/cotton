import { useFolderEncryptionPresence } from "./useFolderEncryptionPresence";
import { useFolderTiles, TILE_PAGE_SIZE } from "./useFolderTiles";
import { useQuery } from "@tanstack/react-query";
import { useCallback } from "react";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import {
  nodesApi,
  type FolderStatsDto,
  type NodeResponse,
} from "../../../shared/api/nodesApi";
import { queryKeys } from "../../../shared/api/queries/queryKeys";
import { refreshNodeContent } from "../../../shared/store/nodesActions";
import { translateError } from "../../../shared/i18n/translateError";
import type { PaginationProps } from "../../../shared/types/FileListViewTypes";
import { useDecryptedFolderContent } from "../../../shared/crypto/useDecryptedFolderContent";
import { useFolderListControls } from "./useFolderListControls";

const LIST_PAGE_SIZE = 100;

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

  const encryptedNames = useFolderEncryptionPresence({
    nodeId,
    match: "encrypted-names",
    enabled:
      enabled && isList && !folderPolicyEnabled && overview.data !== undefined,
    firstPage: overview.data
      ? { response: overview.data, pageSize }
      : undefined,
  });
  const namesEnabled = !folderPolicyEnabled && encryptedNames === false;
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

  const tiles = useFolderTiles(
    queryNodeId,
    queryUserId,
    enabled && !isList,
    fetchOverview,
  );

  const activePage = page === 0 && defaultListing ? overview : listPage;
  const content = useDecryptedFolderContent(
    isList ? activePage.data?.content : tiles.content,
  );
  const stats: FolderStatsDto = overview.data?.content.stats ?? {
    folders: 0,
    files: 0,
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

  return {
    content,
    stats,
    totalCount: overview.data?.totalCount ?? null,
    loading:
      overview.isPending || (isList ? activePage.isPending : tiles.isPending),
    error,
    pagination: paginationProps,
    loadMore: !isList && tiles.hasNextPage ? tiles.loadMore : undefined,
    refresh,
  };
};
