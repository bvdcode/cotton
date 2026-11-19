import { useFolderTiles } from "./useFolderTiles";
import { useQuery } from "@tanstack/react-query";
import { useCallback } from "react";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import type { NodeContentDto } from "@shared/api/nodesApi";
import {
  fetchFolderOverview,
  folderOverviewOptions,
  getFolderOverviewSnapshot,
} from "@shared/api/queries/folderOverview";
import { refreshNodeContent } from "../../../shared/store/nodesActions";
import { translateError } from "../../../shared/i18n/translateError";
import { useFolderListPage } from "./useFolderListPage";
import { FOLDER_PAGE_SIZE } from "@shared/api/queries/folderPageSize";
import { useFolderContentOrder } from "./useFolderContentOrder";

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
  const content = useFolderContentOrder(
    activeListing.content,
    getContentOrderOptions(
      activeListing.content,
      overview.data?.totalCount,
      isList ? list : undefined,
    ),
  );
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
    ...getFolderOverviewSnapshot(overview),
    content,
    loading: overview.isPending || activeListing.isPending,
    error,
    pagination: isList ? list.pagination : undefined,
    loadMore: !isList && tiles.hasNextPage ? tiles.loadMore : undefined,
    refresh,
  };
};

function getContentOrderOptions(
  content: NodeContentDto | undefined,
  overviewCount: number | undefined,
  list: ReturnType<typeof useFolderListPage> | undefined,
) {
  const totalCount = list ? list.pagination.totalCount : overviewCount;
  return {
    naturalOrder:
      overviewCount !== undefined &&
      overviewCount <= FOLDER_PAGE_SIZE &&
      content !== undefined &&
      content.nodes.length + content.files.length === totalCount &&
      (!list || !list.listing.sortBy || list.listing.sortBy === "Name"),
    descending: list?.listing.descending === true,
    pageOffset: list?.pageOffset,
  };
}
