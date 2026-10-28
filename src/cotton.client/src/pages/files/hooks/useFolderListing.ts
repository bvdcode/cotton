import { useFolderTiles } from "./useFolderTiles";
import { useQuery } from "@tanstack/react-query";
import { useCallback } from "react";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import type { FolderStatsDto } from "../../../shared/api/nodesApi";
import {
  fetchFolderOverview,
  folderOverviewOptions,
} from "@shared/api/queries/folderOverview";
import { refreshNodeContent } from "../../../shared/store/nodesActions";
import { translateError } from "../../../shared/i18n/translateError";
import { useDecryptedFolderContent } from "../../../shared/crypto/useDecryptedFolderContent";
import { useFolderListPage } from "./useFolderListPage";
import { FOLDER_PAGE_SIZE } from "@shared/api/queries/folderPageSize";

export const useFolderListing = (
  nodeId: string | null,
  userId: string | null,
  layoutType: InterfaceLayoutType,
  folderPolicyEnabled = false,
) => {
  const enabled = nodeId !== null && userId !== null;
  const isList = layoutType === InterfaceLayoutType.List;
  const pageSize = FOLDER_PAGE_SIZE;
  const queryNodeId = nodeId ?? "";
  const queryUserId = userId ?? "";

  const fetchOverview = useCallback(
    (requestedPageSize: number) =>
      fetchFolderOverview(queryNodeId, requestedPageSize),
    [queryNodeId],
  );

  const overview = useQuery({
    ...folderOverviewOptions(queryNodeId, queryUserId),
    enabled,
  });

  const list = useFolderListPage({
    nodeId,
    userId,
    enabled: enabled && isList,
    folderPolicyEnabled,
    overview,
    pageSize,
  });

  const tiles = useFolderTiles(
    queryNodeId,
    queryUserId,
    enabled && !isList,
    fetchOverview,
  );

  const activeListing = isList ? list : tiles;
  const content = useDecryptedFolderContent(activeListing.content);
  const stats: FolderStatsDto = overview.data?.content.stats ?? {
    folders: 0,
    files: 0,
    sizeBytes: 0,
  };
  const error =
    overview.isError || activeListing.isError
      ? translateError("files", "errors.loadContentsFailed")
      : null;

  const refresh = useCallback(() => {
    if (nodeId) {
      void refreshNodeContent(nodeId);
    }
  }, [nodeId]);

  return {
    node:
      overview.isSuccess && !overview.isFetching
        ? overview.data.content.node
        : undefined,
    content,
    stats,
    totalCount: overview.data?.totalCount ?? null,
    loading: overview.isPending || activeListing.isPending,
    error,
    pagination: isList ? list.pagination : undefined,
    loadMore: !isList && tiles.hasNextPage ? tiles.loadMore : undefined,
    refresh,
  };
};
