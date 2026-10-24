import { useFolderTiles } from "./useFolderTiles";
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
